using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// #336. The test step with a memory: what the suite said, written where both a person and the
/// gate can read it. `dotnet test` prints one verdict line per assembly (`Passed! - Failed: 0,
/// Passed: 479... - Name.dll (net10.0)`); the step captures those lines as they drain, and after
/// a green run it resolves the full inventory (`--list-tests` per project, on the tree the run
/// just built) and writes `artifacts/ci-summary.json` beside the solution. Written only on
/// success: a summary from a half-run would be a summary lying about what ran.
/// <para>
/// The gate (the Server's controlled launcher) and the pipeline (this AppHost) are different
/// processes with no shared transport — a file both read is the seam, and a person opening the
/// worktree reads the same file the gate read. This is why the summary is not parsed from logs by
/// the gate: it is produced by the step that ran the tests, in the assembly's own words.
/// </para>
/// </summary>
static partial class TestStep
{
    /// <summary>
    /// The verdict line's own shape, copied from a real run: `Passed!  - Failed:     0, Passed:
    /// 488, Skipped:     0, Total:   488, Duration: 52 s - Name.dll (net10.0)`. Whitespace varies
    /// between runs; the group order does not.
    /// </summary>
    [GeneratedRegex(@"^(Passed|Failed)!\s+- Failed:\s*(\d+), Passed:\s*(\d+), Skipped:\s*(\d+), Total:\s*(\d+), Duration:.* - (.+?)\.dll \(net[\d.]+\)$")]
    private static partial Regex VerdictLine();

    /// <summary>What `--list-tests` prints: the fully-qualified name, indented under a header.</summary>
    [GeneratedRegex(@"^\s{4,}([A-Za-z_].*)$")]
    private static partial Regex ListedTest();

    public static async Task Run(
        string solution,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        var lines = new List<string>();
        // The coverage step merges every Cobertura file under here, so one from an earlier run —
        // an assembly since deleted, a GUID directory nobody cleans — would be merged as if this
        // run had produced it.
        var results = Path.Combine(root, CoverageStep.ResultsRelativePath);
        // The Playwright media too: the end-to-end fixture clears it when it starts, but a run in
        // which that suite does not start would otherwise hand the evidence last run's videos.
        foreach (var stale in new[] { results, Path.Combine(root, "artifacts", "playwright") })
        {
            if (Directory.Exists(stale))
            {
                Directory.Delete(stale, recursive: true);
            }
        }

        await StepCommand.Run(
            "dotnet",
            [
                "test",
                solution,
                "--no-restore",
                // The stage graph already ordered this project's build before this step
                // (DotNetSteps.WithTestStep), so the assemblies exist; rebuilding them here put a
                // second incremental build of the solution inside the test step's own clock,
                // which is what #388 measured as 377.5s of a 399.9s gate.
                "--no-build",
                // coverlet.collector is already referenced by every test project; this is the
                // switch that turns it on, and coverage.runsettings is what it leaves out.
                "--collect:XPlat Code Coverage",
                "--settings",
                Path.Combine(root, "coverage.runsettings"),
                "--results-directory",
                results,
            ],
            root,
            logger,
            cancellationToken,
            captureLine: lines.Add
        );

        var parsed = ParseVerdicts(lines);
        if (parsed.Count == 0)
        {
            // A green run with no parseable verdict lines is a summary nobody should trust — the
            // output format changed, or the capture broke. Writing nothing is strictly better.
            Missing(logger, solution, "no verdict lines were captured");
            return;
        }

        // The verdict lines name the assembly file, not the project. The project that built it is
        // found by name under the solution directory — one glob per assembly, on a tree the run
        // already made warm.
        var projectsByName = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .GroupBy(file => Path.GetFileNameWithoutExtension(file) ?? file, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        // All at once: each listing is its own `dotnet test --no-build` against an assembly that is
        // already built, so nothing is shared but the CPU, and one after another they were ten
        // seconds on the gate's critical path. The order of the summary is the verdicts' order.
        var assemblies = await Task.WhenAll(parsed.Select(async verdict =>
        {
            var (_, passed, failed, passedTests, skipped, projectName) = verdict;
            var tests = Array.Empty<string>();
            if (projectsByName.TryGetValue(projectName, out var project))
            {
                var listed = new List<string>();
                await StepCommand.Run(
                    "dotnet",
                    ["test", project, "--no-build", "--list-tests", "--nologo"],
                    root,
                    logger,
                    cancellationToken,
                    captureLine: listed.Add
                );
                tests = [.. listed.Select(Listed).Where(name => name is not null)!];
            }

            return new TestAssembly(projectName, passed, failed, passedTests, skipped, tests);
        }));

        var path = Path.Combine(root, "artifacts", "ci-summary.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(new TestSummary(assemblies, DateTimeOffset.UtcNow), Json.Options),
            cancellationToken
        );
        Written(logger, path, assemblies.Length);
    }

    internal static IReadOnlyList<(string Assembly, bool Passed, int Failed, int PassedTests, int Skipped, string Project)> ParseVerdicts(
        IReadOnlyList<string> lines
    )
    {
        var parsed = new List<(string, bool, int, int, int, string)>();
        foreach (var line in lines)
        {
            if (VerdictLine().Match(line) is not { Success: true } match)
            {
                continue;
            }

            parsed.Add(
                (
                    match.Groups[6].Value,
                    match.Groups[1].Value == "Passed",
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value),
                    int.Parse(match.Groups[4].Value),
                    match.Groups[6].Value
                )
            );
        }

        return parsed;
    }

    internal static string? Listed(string line) =>
        ListedTest().Match(line) is { Success: true } match ? match.Groups[1].Value : null;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Test summary written to {Path} covering {Assemblies} assemblies")]
    static partial void Written(ILogger logger, string path, int assemblies);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No ci-summary.json written for {Solution}: {Reason}")]
    static partial void Missing(ILogger logger, string solution, string reason);
}

/// <summary>One assembly: its verdict counts and, when the listing ran, every test's full name.</summary>
sealed record TestAssembly(
    string Project,
    bool Passed,
    int Failed,
    int PassedTests,
    int Skipped,
    IReadOnlyList<string> Tests
);

sealed record TestSummary(IReadOnlyList<TestAssembly> Assemblies, DateTimeOffset At);

static class Json
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
