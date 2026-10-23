using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREPIPELINES001 // Pipelines are experimental in 13.4.6; the reason for taking that on is in AppHost.cs.

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// The mutation half of <c>ci</c>: Stryker.NET over the files the change touched, so the review
/// learns not only whether the new lines run under a test but whether any test would notice them
/// being wrong. Coverage says a line executed; a surviving mutant says it executed and nothing
/// checked.
/// <para>
/// <b>In the gate, reported and never enforced.</b> A mutation score floor on a codebase that has
/// never measured one is a gate born red. The step writes <c>artifacts/mutation/summary.json</c>
/// and says what survived; what it cannot measure — Stryker failing, the budget running out, no
/// trunk to diff against — it records as <c>measured: false</c> with the reason, and the gate goes
/// on. Only cancelling the gate itself stops it.
/// </para>
/// <para>
/// <b>Beside the tests, not after them, and every project at once.</b> It starts when
/// <c>install</c> is done and runs while the gate builds and tests. That is only possible because
/// each project is mutated in <b>its own copy</b> of the checkout: Stryker
/// compiles its mutants into the projects' <c>bin/</c>, and doing that in the checkout would hand
/// the gate's own test run a mutated assembly. The copies share nothing, so the runs share nothing
/// but the CPU, which is split between them.
/// </para>
/// <para>
/// <b>Scoped by naming the changed files, not by <c>--since</c>, and per project, not per
/// solution.</b> Both were measured first. Plain <c>--solution</c> gave every project an initial
/// test run before looking at the diff — about 75 seconds each, a dozen times. <c>--since</c>, in
/// this layout, filed the Server's sources under "changed test file" (SharedFunctionalTests
/// references the Server) and removed every mutant in them: a green report with nothing tested.
/// What works is the solution for pairing tests with code, <c>--project</c> for the scope, and
/// <c>--mutate</c> for each changed file.
/// </para>
/// </summary>
static partial class MutationStep
{
    /// <summary>Each project's report lands under here, and <c>summary.json</c> beside them.</summary>
    internal const string ReportRelativePath = "artifacts/mutation";

    /// <summary>
    /// How long the whole step may take from the moment it starts. It runs beside the tests, so up
    /// to the length of the test run it costs nothing; past this it is recorded as unmeasured rather
    /// than holding the gate. <c>MUTATION_BUDGET_MINUTES</c> overrides it.
    /// </summary>
    const int DefaultBudgetMinutes = 10;

    /// <summary>
    /// Test projects that do not count as covering a project. A source project only they reach is
    /// recorded as untested rather than mutated: the end-to-end suite starts a real AppHost and a
    /// browser, and a mutant run is hundreds of those.
    /// </summary>
    static readonly string[] NotForMutants = ["AiOrchestrator.EndToEndTests"];

    /// <summary>What is not copied: build output, dependencies, and what earlier runs left.</summary>
    static readonly string[] NotCopied = [".git", ".worktrees", ".aspire", "bin", "obj", "node_modules", "artifacts", "TestResults"];

    public static IResourceBuilder<ProjectResource> WithMutationStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = WellKnownCIStepNames.For("mutation", factory.Resource.Name),
            Description = "Stryker.NET over the files the change touched (reported, never enforced)",
            Action = context => Run(solution, context.Logger, context.CancellationToken),
            // `install` and nothing else: the restore and the pinned tools. Not `compile` — the
            // copies build themselves — and not `test`, which this runs beside.
            DependsOnSteps = [WellKnownCIStepNames.Install],
            RequiredBySteps = [WellKnownCIStepNames.Test],
        });

    internal static async Task Run(string solution, ILogger logger, CancellationToken cancellationToken)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        var output = Path.Combine(root, ReportRelativePath);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        var budget = int.TryParse(Environment.GetEnvironmentVariable("MUTATION_BUDGET_MINUTES"), out var minutes) && minutes > 0
            ? minutes
            : DefaultBudgetMinutes;

        MutationSummary summary;
        try
        {
            summary = await Measure(solution, root, output, budget, logger, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            summary = new MutationSummary(false, exception.Message, null, budget, []);
        }

        if (!summary.Measured)
        {
            Unmeasured(logger, summary.Reason ?? "unknown");
        }

        await File.WriteAllTextAsync(
            Path.Combine(output, "summary.json"),
            JsonSerializer.Serialize(summary, Json.Options),
            cancellationToken
        );
    }

    static async Task<MutationSummary> Measure(
        string solution,
        string root,
        string output,
        int budget,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var repository = (await Git(["rev-parse", "--show-toplevel"], root, logger, cancellationToken)).Single();
        // The same base the checks in .aio/checks/ judge against, and overridable the same way.
        var trunk = Environment.GetEnvironmentVariable("CHECK_TRUNK") ?? "origin/master";
        var since = Environment.GetEnvironmentVariable("MUTATION_SINCE") is { Length: > 0 } given
            // Verified here, so a typo fails in a second rather than as an empty diff.
            ? (await Git(["rev-parse", "--verify", $"{given}^{{commit}}"], root, logger, cancellationToken)).Single()
            : (await Git(["merge-base", "HEAD", trunk], root, logger, cancellationToken)).Single();

        // Against the working tree, like the gate: what is about to be pushed, committed or not.
        var changed = (await Git(["diff", "--name-only", "--diff-filter=AMR", since, "--", "*.cs"], repository, logger, cancellationToken))
            .Select(path => Path.GetFullPath(Path.Combine(repository, path)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var tests = TestProjects(solution);
        var targets = changed
            .Select(file => (File: file, Project: OwningProject(file)))
            .Where(pair => pair.Project is not null && !IsTestOnly(pair.Project))
            .GroupBy(pair => pair.Project!, pair => pair.File, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        if (targets.Count == 0)
        {
            Nothing(logger, since);
            return new MutationSummary(true, null, since, budget, []);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(budget));
        // Every run at once, each with its share of the cores; one more share is left for the
        // gate's own build and tests, which are running on the same machine.
        var concurrency = Math.Max(1, Environment.ProcessorCount / (targets.Count + 1));
        // Resolved, because Stryker resolves it: on macOS the temp directory is under `/var`, a link
        // to `/private/var`, and Stryker looked for the project under the resolved spelling of a
        // solution it had loaded under the other one. It found none and exited 1 on every project
        // from the first wave to #547, and the restore beside it met the same project twice.
        var scratch = Path.Combine(RealPath(Path.GetTempPath()), $"aio-mutation-{Guid.NewGuid():N}");
        try
        {
            var projects = await Task.WhenAll(targets.Select(target =>
                tests.Values.Any(references => references.Contains(target.Key))
                    ? Mutate(new MutationRun(target.Key, [.. target], repository, solution, root, output, Path.Combine(scratch, Path.GetFileNameWithoutExtension(target.Key)), concurrency, budget), logger, timeout.Token, cancellationToken)
                    : Task.FromResult(Untested(logger, target.Key))));
            return new MutationSummary(true, null, since, budget, projects);
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    static async Task<MutatedProject> Mutate(
        MutationRun run,
        ILogger logger,
        CancellationToken timeout,
        CancellationToken cancellationToken
    )
    {
        var name = Path.GetFileNameWithoutExtension(run.Project);
        try
        {
            // The whole checkout, less what is rebuilt or regenerated: a build reads more than the
            // slnx names — the Tasks tests embed `.github/ISSUE_TEMPLATE` — and Stryker only
            // analyses projects under the directory it starts in, so it starts in the copy. The
            // copy is restored before Stryker sees it; without `project.assets.json` its analysis
            // finds no project and it exits 1 without a word.
            Copy(run.Repository, run.Copy);
            var inCopy = Path.Combine(run.Copy, Path.GetRelativePath(run.Repository, run.Root));
            var solution = Path.Combine(run.Copy, Path.GetRelativePath(run.Repository, run.Solution));
            await StepCommand.Run("dotnet", ["restore", solution, "--verbosity", "quiet"], inCopy, logger, timeout);

            var directory = Path.GetDirectoryName(run.Project)!;
            var report = Path.Combine(run.Output, name);
            List<string> arguments =
            [
                "dotnet-stryker",
                "--solution", solution,
                "--project", Path.GetFileName(run.Project),
                // Reporters and the never-breaking threshold.
                "--config-file", Path.Combine(run.Root, "stryker-config.json"),
                "--output", report,
                "--concurrency", run.Concurrency.ToString(CultureInfo.InvariantCulture),
                // The runs are interleaved in one step log; their own warnings — hundreds of
                // "unidentified mutation" lines — would bury the summary below, which is the part
                // anybody reads. Not `error`: at that level a run that finds nothing to analyse
                // exits 1 having printed nothing at all.
                "--verbosity", "info",
                "--skip-version-check",
            ];
            foreach (var file in run.Files)
            {
                arguments.AddRange(["--mutate", $"**/{Path.GetRelativePath(directory, file).Replace('\\', '/')}"]);
            }

            var exit = await StepCommand.Run("dotnet", arguments, inCopy, logger, timeout, throwOnFailure: false);
            return exit is 0
                ? Summarise(logger, name, Path.Combine(report, "reports", "mutation-report.json"))
                : Failed(logger, name, $"Stryker exited {exit}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(logger, name, $"over the {run.BudgetMinutes}-minute budget (MUTATION_BUDGET_MINUTES)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failed(logger, name, exception.Message);
        }
    }

    /// <summary>
    /// <paramref name="path"/> with every link in it followed, the way <c>realpath</c> spells it.
    /// .NET resolves a link only when it is the last segment, so each parent is resolved first.
    /// </summary>
    internal static string RealPath(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Path.GetDirectoryName(full) is not { } parent)
        {
            return full;
        }

        var resolved = Path.Combine(RealPath(parent), Path.GetFileName(full));
        return new FileInfo(resolved).LinkTarget is { } target
            ? RealPath(Path.Combine(Path.GetDirectoryName(resolved)!, target))
            : resolved;
    }

    static void Copy(string from, string to)
    {
        if (!Directory.Exists(from))
        {
            return;
        }

        Directory.CreateDirectory(to);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            // A link is not followed: a copy that follows one out of the tree is a copy of whatever
            // it points at.
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo)
            {
                if (!NotCopied.Contains(entry.Name, StringComparer.Ordinal))
                {
                    Copy(entry.FullName, target);
                }
            }
            else
            {
                File.Copy(entry.FullName, target);
            }
        }
    }

    /// <summary>
    /// Every test project the solution declares, with the closure of the projects it references —
    /// a module's functional tests reach the module through <c>SharedFunctionalTests</c>, so the
    /// direct references are not enough to pair them.
    /// </summary>
    static Dictionary<string, HashSet<string>> TestProjects(string solution)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        return XDocument.Load(solution)
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .OfType<string>()
            .Select(path => Path.GetFullPath(Path.Combine(root, path)))
            .Where(IsTestOnly)
            .Where(path => !NotForMutants.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal))
            .ToDictionary(path => path, References, StringComparer.Ordinal);
    }

    static HashSet<string> References(string project)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([project]);
        while (pending.TryPop(out var current))
        {
            if (!File.Exists(current))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(current)!;
            foreach (var reference in XDocument.Load(current).Descendants("ProjectReference"))
            {
                // Analyzers are referenced as projects too, but nothing runs them as code.
                if ((string?)reference.Attribute("OutputItemType") == "Analyzer"
                    || (string?)reference.Attribute("Include") is not { } include)
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
                if (seen.Add(path))
                {
                    pending.Push(path);
                }
            }
        }

        return seen;
    }

    /// <summary>The nearest <c>.csproj</c> above a file, which is the project that compiles it.</summary>
    static string? OwningProject(string file)
    {
        for (var directory = Path.GetDirectoryName(file); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault() is { } project)
            {
                return project;
            }
        }

        return null;
    }

    static bool IsTestOnly(string project) =>
        Path.GetFileNameWithoutExtension(project).EndsWith("Tests", StringComparison.Ordinal);

    /// <summary>
    /// The survivors, in the log where the review reads them and in the summary a scorer reads:
    /// file, line and what was changed. The HTML beside the JSON is the same list with the code
    /// around each one.
    /// </summary>
    static MutatedProject Summarise(ILogger logger, string project, string report)
    {
        if (!File.Exists(report))
        {
            return Failed(logger, project, $"Stryker exited 0 but left no {report}");
        }

        using var json = JsonDocument.Parse(File.ReadAllText(report));
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var survivors = new List<Survivor>();
        foreach (var file in json.RootElement.GetProperty("files").EnumerateObject())
        {
            // Reported from the copy, so the path names the copy; from `src/` on it is the checkout's.
            var path = file.Name.Replace('\\', '/');
            if (path.IndexOf("/src/", StringComparison.Ordinal) is var at and >= 0)
            {
                path = path[(at + 1)..];
            }

            foreach (var mutant in file.Value.GetProperty("mutants").EnumerateArray())
            {
                var status = mutant.GetProperty("status").GetString() ?? "";
                counts[status] = counts.GetValueOrDefault(status) + 1;
                if (status is "Survived" or "NoCoverage")
                {
                    survivors.Add(new Survivor(
                        path,
                        mutant.GetProperty("location").GetProperty("start").GetProperty("line").GetInt32(),
                        status,
                        mutant.GetProperty("mutatorName").GetString() ?? "",
                        mutant.TryGetProperty("replacement", out var replacement) ? replacement.GetString() : null
                    ));
                }
            }
        }

        // Stryker's own definition: detected over valid. Ignored and compile errors are neither.
        var detected = counts.GetValueOrDefault("Killed") + counts.GetValueOrDefault("Timeout");
        var valid = detected + counts.GetValueOrDefault("Survived") + counts.GetValueOrDefault("NoCoverage");
        double? score = valid == 0 ? null : Math.Round(100.0 * detected / valid, 1);
        var html = report.Replace(".json", ".html", StringComparison.Ordinal);
        // Invariant: on a machine set to a comma-decimal culture the line read "50,0%" (#675).
        Score(logger, project, score is null ? "n/a" : string.Create(CultureInfo.InvariantCulture, $"{score:F1}%"), detected, valid, html);
        foreach (var survivor in survivors.Take(30))
        {
            SurvivorLine(logger, $"{survivor.File}:{survivor.Line}  {survivor.Status}  {survivor.Mutator}");
        }

        return new MutatedProject(project, true, null, score, detected, valid, counts, survivors, html);
    }

    static MutatedProject Untested(ILogger logger, string project)
    {
        var name = Path.GetFileNameWithoutExtension(project);
        UntestedLine(logger, name);
        return Unmeasured(name, "no test project Stryker may run references it");
    }

    static MutatedProject Failed(ILogger logger, string project, string reason)
    {
        FailedLine(logger, project, reason);
        return Unmeasured(project, reason);
    }

    static MutatedProject Unmeasured(string project, string reason) =>
        new(project, false, reason, null, 0, 0, new Dictionary<string, int>(), [], null);

    static async Task<List<string>> Git(string[] arguments, string directory, ILogger logger, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await StepCommand.Run("git", arguments, directory, logger, cancellationToken, captureLine: lines.Add);
        return lines;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No source project changed since {Since}; nothing to mutate")]
    static partial void Nothing(ILogger logger, string since);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mutation not measured: {Reason}")]
    static partial void Unmeasured(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Project} changed but no test project Stryker may run references it; not mutated")]
    static partial void UntestedLine(ILogger logger, string project);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Project}: mutation not measured — {Reason}")]
    static partial void FailedLine(ILogger logger, string project, string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Project}: mutation score {Score} ({Detected} of {Valid} mutants detected) — {Html}")]
    static partial void Score(ILogger logger, string project, string score, int detected, int valid, string html);

    [LoggerMessage(Level = LogLevel.Information, Message = "  {Survivor}")]
    static partial void SurvivorLine(ILogger logger, string survivor);
}

/// <summary>One Stryker run: the project, the files in it that changed, and where it runs.</summary>
sealed record MutationRun(
    string Project,
    IReadOnlyList<string> Files,
    string Repository,
    string Solution,
    string Root,
    string Output,
    string Copy,
    int Concurrency,
    int BudgetMinutes
);

/// <summary><c>artifacts/mutation/summary.json</c>: what was measured, and what was not and why.</summary>
sealed record MutationSummary(
    bool Measured,
    string? Reason,
    string? Since,
    int BudgetMinutes,
    IReadOnlyList<MutatedProject> Projects
);

sealed record MutatedProject(
    string Project,
    bool Measured,
    string? Reason,
    double? Score,
    int Detected,
    int Valid,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<Survivor> Survivors,
    string? Report
);

sealed record Survivor(string File, int Line, string Status, string Mutator, string? Replacement);
