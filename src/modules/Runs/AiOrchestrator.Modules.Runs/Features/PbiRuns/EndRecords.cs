using System.Globalization;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.RunRequests;
using Json.Schema;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// How a run ended, read from the end record its agent left with <c>.aio/aio done</c>.
/// <para>
/// <b>Untrusted input.</b> The agent can skip the command, write the file by hand or name an
/// outcome its state has no transition for, and each of those ends the run as a failure whose
/// <see cref="RunFailure"/> says which. Nothing here throws on what the file holds: a run that
/// cannot be read is a run that failed, never a PBI left hanging.
/// </para>
/// <para>
/// The shape is <c>.aio/schemas/done.v1.json</c>, embedded in this assembly from that one file,
/// so the script, the schema and this reader cannot be three versions of the same rule.
/// </para>
/// </summary>
static class EndRecords
{
    /// <summary>Where <c>aio done</c> writes, relative to the run's working directory.</summary>
    public static readonly string RelativePath = Path.Combine(".aio", "done.json");

    /// <summary>The outcome that says the agent could not go on. It fails the run with its reason.</summary>
    public const string Blocked = "blocked";

    static readonly Lazy<JsonSchema> Schema = new(Load);

    /// <summary>
    /// The end of a run that worked in <paramref name="workingDirectory"/> and started at
    /// <paramref name="startedAt"/>, for a state whose <c>on</c> knows <paramref name="outcomes"/>.
    /// </summary>
    public static RunEnd Read(string workingDirectory, IReadOnlyCollection<string> outcomes, DateTimeOffset startedAt)
    {
        var file = Path.Combine(workingDirectory, RelativePath);

        string? text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            text = null;
        }

        return Parse(text, file, outcomes, startedAt);
    }

    /// <summary>
    /// The end of a run from the record its provider read back, <paramref name="text"/>, or null
    /// when there was none. <paramref name="file"/> is where it was written, for the reason.
    /// </summary>
    public static RunEnd Parse(
        string? text,
        string file,
        IReadOnlyCollection<string> outcomes,
        DateTimeOffset startedAt
    )
    {
        if (text is null)
        {
            return Failed(RunFailure.NoEndRecord, $"The agent ended without calling aio done: {file} does not exist.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException malformed)
        {
            return Failed(RunFailure.InvalidEndRecord, $"{file} is not JSON: {malformed.Message}");
        }

        using (document)
        {
            var results = Schema.Value.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true }
            );

            if (!results.IsValid)
            {
                return Failed(RunFailure.InvalidEndRecord, $"{file} does not match done.v1.json: {FirstError(results)}");
            }

            var root = document.RootElement;
            var outcome = root.GetProperty("outcome").GetString()!;
            var reason = root.TryGetProperty("reason", out var said) ? said.GetString() : null;
            var problems = root.TryGetProperty("problems", out var listed)
                ? listed.EnumerateArray().Select(problem => problem.GetString()!).ToList()
                : [];
            var at = DateTimeOffset.Parse(root.GetProperty("at").GetString()!, CultureInfo.InvariantCulture);

            // The file lives in a worktree that outlives a run, so the record there may be the last
            // run's. Its time is the only thing that tells the two apart.
            if (at < startedAt)
            {
                return Failed(
                    RunFailure.NoEndRecord,
                    $"The agent ended without calling aio done: the record in {file} was written at "
                        + $"{at:O}, before this run started at {startedAt:O}."
                );
            }

            if (outcome == Blocked)
            {
                return new RunEnd(null, reason, RunFailure.Blocked, problems);
            }

            if (!outcomes.Contains(outcome, StringComparer.Ordinal))
            {
                var known = outcomes.Count == 0 ? "none" : string.Join(", ", outcomes);
                return new RunEnd(
                    null,
                    $"The end record names '{outcome}', and the state knows {known}.",
                    RunFailure.UnknownOutcome,
                    problems
                );
            }

            return new RunEnd(outcome, reason, null, problems);
        }
    }

    static RunEnd Failed(RunFailure cause, string reason) => new(null, reason, cause, []);

    static string FirstError(EvaluationResults results) =>
        (results.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .Select(detail => $"{detail.InstanceLocation} {detail.Errors!.Values.First()}")
            .FirstOrDefault()
        ?? "it is not valid";

    static JsonSchema Load()
    {
        using var stream =
            typeof(EndRecords).Assembly.GetManifestResourceStream("done.v1.json")
            ?? throw new InvalidOperationException("done.v1.json is not embedded in Modules.Runs.");
        using var reader = new StreamReader(stream);

        // A registry of its own: the global one refuses a second schema with the same $id.
        return JsonSchema.FromText(reader.ReadToEnd(), new BuildOptions { SchemaRegistry = new() });
    }
}
