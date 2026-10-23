using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>What one script's line means to the state that ran it.</summary>
enum VerdictKind
{
    /// <summary>The step did its part; the next script, the agent or the move comes after it.</summary>
    Pass,

    /// <summary>The script named the state's outcome, such as <c>green</c> or <c>high</c>.</summary>
    Outcome,

    /// <summary>Not yet, and not a failure: the next pass runs the script again.</summary>
    Wait,

    /// <summary>The step failed. Nothing retries, so the PBI holds with the reason.</summary>
    Fail,
}

/// <param name="Script">The script's name, as <c>loop.yaml</c> names it.</param>
/// <param name="Outcome">The outcome, when <paramref name="Kind"/> is <see cref="VerdictKind.Outcome"/>.</param>
/// <param name="Why">The script's own <c>why</c>, or what it printed last when it answered none.</param>
/// <param name="Output">The file the engine kept everything the script printed in (#638), once it has.</param>
/// <param name="Exit">The script's exit code, which decides an outcome the state's <c>on</c> does not name (#733).</param>
sealed record ScriptVerdict(string Script, VerdictKind Kind, string? Outcome, string Why, string? Output = null, int Exit = 0);

/// <summary>
/// Reads a script's last JSON line into a verdict, the same way for every script
/// (openspec/specs/harness-scripts): <c>outcome</c> names the state's outcome whatever the exit code,
/// because a red gate exits 1 for the person who runs it by hand; <c>wait: true</c> is not yet;
/// otherwise exit 0 passes and anything else fails. <c>why</c> is the reason in each case. No line
/// at all has failed, whatever the exit code; a line with the terminal's colour codes in it is still
/// the line (<see cref="ScriptLine.Last"/>, #710).
/// </summary>
static class ScriptVerdicts
{
    public static ScriptVerdict Read(string script, ScriptResult result)
    {
        if (result.Line is not { ValueKind: JsonValueKind.Object } line)
        {
            var printed = LastLine(result.Output);
            return new(
                script,
                VerdictKind.Fail,
                null,
                $"{script} exited {result.ExitCode} without its JSON line{(printed is null ? "" : $": {printed}")}"
            );
        }

        var why = Text(line, "why") ?? LastLine(result.Output) ?? $"{script} exited {result.ExitCode}";
        var outcome = Text(line, "outcome");
        var kind = outcome is not null ? VerdictKind.Outcome
            : line.TryGetProperty("wait", out var wait) && wait.ValueKind == JsonValueKind.True ? VerdictKind.Wait
            : result.ExitCode == 0 ? VerdictKind.Pass
            : VerdictKind.Fail;
        return new(script, kind, outcome, why, Exit: result.ExitCode);
    }

    static string? Text(JsonElement line, string name) =>
        line.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    static string? LastLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() is { } line
            ? ScriptLine.Plain(line)
            : null;
}
