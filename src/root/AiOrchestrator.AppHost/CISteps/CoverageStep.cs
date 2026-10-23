using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREPIPELINES001 // Pipelines are experimental in 13.4.6; the reason for taking that on is in AppHost.cs.

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// What the suite exercised, read after it passed: the coverage every test assembly collected,
/// merged into one report a person opens, and the CRAP score of every method in it.
/// <para>
/// <b>Reported, never enforced.</b> Nothing here fails <c>ci</c> for a number. A coverage floor
/// born on a codebase that has never measured itself is a gate born red, and AGENTS.md already
/// says what that teaches. The step fails only when it cannot produce the report, because a
/// report that silently stops being written is one the review goes on trusting.
/// </para>
/// <para>
/// CRAP (Change Risk Anti-Patterns, Savoia and Evans) is <c>c² · (1 − cov)³ + c</c>: cyclomatic
/// complexity, punished by how much of it no test runs. A simple method stays low however
/// untested; a complex one comes down only as its coverage goes up. 30 is the conventional line.
/// ReportGenerator shows the same numbers as <i>Risk Hotspots</i> in the HTML; they are computed
/// again here so the review has them as a file it can read without a browser —
/// <c>.aio/checks/crap.sh</c> reads <c>crap.tsv</c>.
/// </para>
/// </summary>
static partial class CoverageStep
{
    /// <summary>Where <c>dotnet test</c> leaves each assembly's raw Cobertura file.</summary>
    internal const string ResultsRelativePath = "artifacts/test-results";

    /// <summary>The merged report: <c>index.html</c>, <c>Cobertura.xml</c>, <c>Summary.txt</c>, <c>crap.tsv</c>.</summary>
    internal const string ReportRelativePath = "artifacts/coverage";

    /// <summary>The conventional CRAP line: above it a method is too complex for how little of it is tested.</summary>
    internal const double CrapThreshold = 30;

    /// <summary>
    /// Adds <c>dotnet tool restore</c> to the <c>install</c> stage: ReportGenerator and Stryker are
    /// local tools pinned in the repository's <c>dotnet-tools.json</c>, so the version a report
    /// was made with is the one in the commit, not whichever one this machine installed globally.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithToolRestoreStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = ToolRestoreStep(factory.Resource.Name),
            Description = "dotnet tool restore",
            Action = context =>
                StepCommand.Run(
                    "dotnet",
                    ["tool", "restore"],
                    Root(solution),
                    context.Logger,
                    context.CancellationToken
                ),
            RequiredBySteps = [WellKnownCIStepNames.Install],
        });

    /// <summary>
    /// Adds the coverage report to the <c>test</c> stage, after the test step that collected it.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithCoverageStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = WellKnownCIStepNames.For("coverage", factory.Resource.Name),
            Description = "reportgenerator: coverage and CRAP",
            Action = context => Report(solution, context.Logger, context.CancellationToken),
            DependsOnSteps =
            [
                WellKnownCIStepNames.For("test", factory.Resource.Name),
                ToolRestoreStep(factory.Resource.Name),
            ],
            RequiredBySteps = [WellKnownCIStepNames.Test],
        });

    static async Task Report(string solution, ILogger logger, CancellationToken cancellationToken)
    {
        var root = Root(solution);
        var target = Path.Combine(root, ReportRelativePath);
        // A report directory is regenerated whole: a class deleted from the tree must not keep a
        // page from the run before.
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        await StepCommand.Run(
            "dotnet",
            [
                "tool",
                "run",
                "reportgenerator",
                $"-reports:{Path.Combine(root, ResultsRelativePath)}/**/coverage.cobertura.xml",
                $"-targetdir:{target}",
                "-reporttypes:Html;Cobertura;TextSummary",
                // Generated sources (LoggerMessage, GeneratedRegex) and migrations are code nobody
                // wrote by hand and nobody tests by hand; counting them only moves the number.
                "-filefilters:-**/obj/**;-**/Migrations/**",
                "-classfilters:-System.Text.RegularExpressions.Generated*",
                "-verbosity:Warning",
            ],
            root,
            logger,
            cancellationToken
        );

        var merged = Path.Combine(target, "Cobertura.xml");
        if (!File.Exists(merged))
        {
            throw new InvalidOperationException(
                $"reportgenerator exited 0 but wrote no {merged} — was coverage collected?"
            );
        }

        var summary = Path.Combine(target, "Summary.txt");
        if (File.Exists(summary))
        {
            foreach (var line in (await File.ReadAllLinesAsync(summary, cancellationToken)).Take(20))
            {
                if (line.Contains("coverage:", StringComparison.Ordinal))
                {
                    Summary(logger, line.Trim());
                }
            }
        }

        // The TSV's paths are relative to the checkout because that is what `git diff` prints and
        // the crap check intersects the two; its header names the commit so a report left over from
        // an earlier one is refused rather than read.
        var git = new List<string>();
        await StepCommand.Run(
            "git",
            ["rev-parse", "--show-toplevel", "HEAD"],
            root,
            logger,
            cancellationToken,
            captureLine: git.Add
        );
        if (git is not [var repository, var commit])
        {
            throw new InvalidOperationException("`git rev-parse --show-toplevel HEAD` did not print two lines");
        }

        var methods = Crap.Read(XDocument.Load(merged), repository);
        var tsv = Path.Combine(target, "crap.tsv");
        await File.WriteAllTextAsync(tsv, Crap.Tsv(methods, commit), cancellationToken);

        var risky = methods.Where(method => method.Score > CrapThreshold).ToList();
        Hotspots(logger, risky.Count, CrapThreshold, methods.Count);
        foreach (var method in risky.Take(10))
        {
            Hotspot(
                logger,
                method.Score,
                method.Complexity,
                method.Coverage * 100,
                $"{method.File}:{method.FirstLine}",
                method.Name
            );
        }

        Written(logger, Path.Combine(target, "index.html"), tsv);
    }

    static string ToolRestoreStep(string resource) =>
        WellKnownCIStepNames.For("tool-restore", resource);

    static string Root(string solution) =>
        Path.GetDirectoryName(Path.GetFullPath(solution))
        ?? throw new InvalidOperationException($"{solution} has no directory to run in");

    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    static partial void Summary(ILogger logger, string line);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} methods score CRAP above {Threshold} of {Total} measured")]
    static partial void Hotspots(ILogger logger, int count, double threshold, int total);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "  CRAP {Score,7:F1}  complexity {Complexity,3}  coverage {Coverage,5:F1}%  {Location}  {Method}")]
    static partial void Hotspot(ILogger logger, double score, int complexity, double coverage, string location, string method);

    [LoggerMessage(Level = LogLevel.Information, Message = "Coverage report written to {Html}; CRAP per method in {Tsv}")]
    static partial void Written(ILogger logger, string html, string tsv);
}

/// <summary>One method as the merged Cobertura report sees it, with its CRAP score.</summary>
sealed record CrapMethod(
    string File,
    int FirstLine,
    int LastLine,
    string Name,
    int Complexity,
    double Coverage,
    double Score
);

/// <summary>The CRAP arithmetic, over ReportGenerator's merged Cobertura file.</summary>
static class Crap
{
    /// <summary><c>c² · (1 − cov)³ + c</c>, with coverage as a fraction of the method's lines.</summary>
    internal static double Score(int complexity, double coverage) =>
        (complexity * complexity * Math.Pow(1 - coverage, 3)) + complexity;

    /// <summary>
    /// Every method with at least one coverable line, worst first. Line coverage, not branch: it
    /// is what the original metric used and what ReportGenerator's hotspots use, so the TSV and
    /// the HTML a person opens next to it agree.
    /// </summary>
    internal static IReadOnlyList<CrapMethod> Read(XDocument cobertura, string repository)
    {
        var methods = new List<CrapMethod>();
        foreach (var @class in cobertura.Descendants("class"))
        {
            var file = (string?)@class.Attribute("filename") ?? "";
            var relative = Path.IsPathRooted(file)
                ? Path.GetRelativePath(repository, file).Replace('\\', '/')
                : file.Replace('\\', '/');
            var type = (string?)@class.Attribute("name") ?? "";
            foreach (var method in @class.Descendants("method"))
            {
                var lines = method
                    .Descendants("line")
                    .Select(line => ((int?)line.Attribute("number") ?? 0, (int?)line.Attribute("hits") ?? 0))
                    .ToList();
                if (lines.Count == 0)
                {
                    continue;
                }

                var complexity = Math.Max(1, (int?)method.Attribute("complexity") ?? 1);
                var coverage = lines.Count(line => line.Item2 > 0) / (double)lines.Count;
                methods.Add(new CrapMethod(
                    relative,
                    lines.Min(line => line.Item1),
                    lines.Max(line => line.Item1),
                    $"{type}.{(string?)method.Attribute("name")}{(string?)method.Attribute("signature")}",
                    complexity,
                    coverage,
                    Score(complexity, coverage)
                ));
            }
        }

        // One method can arrive twice: an async method's state machine and its lambdas are classes of
        // their own in the IL, and ReportGenerator names more than one of them after the method.
        // The same file and the same lines are the same method, whatever the name says.
        return [.. methods
            .OrderByDescending(method => method.Score)
            .ThenByDescending(method => method.Name.Length)
            .ThenBy(method => method.File, StringComparer.Ordinal)
            .DistinctBy(method => (method.File, method.FirstLine, method.LastLine))];
    }

    /// <summary>
    /// Tab-separated so a shell script reads it with <c>IFS=$'\t' read</c>, and headed by the
    /// commit it describes so nothing reads it as the answer for another one.
    /// </summary>
    internal static string Tsv(IReadOnlyList<CrapMethod> methods, string commit)
    {
        var text = new StringBuilder();
        text.Append("# commit\t").Append(commit).Append('\n');
        text.Append("# crap\tcomplexity\tcoverage\tfile\tfirst\tlast\tmethod\n");
        foreach (var method in methods)
        {
            text.Append(method.Score.ToString("F1", CultureInfo.InvariantCulture)).Append('\t')
                .Append(method.Complexity.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append((method.Coverage * 100).ToString("F1", CultureInfo.InvariantCulture)).Append('\t')
                .Append(method.File).Append('\t')
                .Append(method.FirstLine.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(method.LastLine.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(method.Name.Replace('\t', ' ')).Append('\n');
        }

        return text.ToString();
    }
}
