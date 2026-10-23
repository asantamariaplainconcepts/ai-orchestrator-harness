using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Engine;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #593. One protocol for every script: <c>outcome</c>, <c>wait</c>, <c>why</c> and the exit code,
/// read from lines of a script that does not exist, so no name of ours means anything.
/// </summary>
public sealed class ScriptVerdictTests
{
    const string Script = "my-own-check";

    [Theory]
    [InlineData(0, """{"why": "all fine"}""", "Pass")]
    [InlineData(1, """{"why": "it broke"}""", "Fail")]
    [InlineData(0, """{"outcome": "shipped", "why": "it went out"}""", "Outcome shipped")]
    [InlineData(1, """{"outcome": "red", "why": "a test failed"}""", "Outcome red")]
    [InlineData(0, """{"wait": true, "why": "another one holds the file"}""", "Wait")]
    [InlineData(0, """{"wait": false, "why": "nothing to wait for"}""", "Pass")]
    [InlineData(1, """{"wait": true, "why": "a wait is not a failure"}""", "Wait")]
    [InlineData(0, """{"outcome": "low", "wait": true, "why": "the outcome wins"}""", "Outcome low")]
    [InlineData(0, """{"outcome": "", "why": "an empty outcome is none"}""", "Pass")]
    [InlineData(1, """{"go": true, "clear": true, "band": "low", "why": "extra fields mean nothing"}""", "Fail")]
    public void A_line_reads_by_its_outcome_its_wait_and_its_exit_code(int exit, string line, string expected)
    {
        var verdict = ScriptVerdicts.Read(Script, Result(exit, line));

        verdict.Script.ShouldBe(Script);
        $"{verdict.Kind}{(verdict.Outcome is null ? "" : $" {verdict.Outcome}")}".ShouldBe(expected);
        verdict.Why.ShouldBe(JsonDocument.Parse(line).RootElement.GetProperty("why").GetString());
    }

    [Fact]
    public void A_line_without_why_gives_what_the_script_printed() =>
        ScriptVerdicts.Read(Script, Result(0, """{"ok": true}""")).Why.ShouldBe("""{"ok": true}""");

    [Fact]
    public void A_script_without_its_line_has_failed_whatever_it_exited() =>
        ScriptVerdicts.Read(Script, new ScriptResult(0, null, "fatal: not a git repository\n"))
            .ShouldBe(new ScriptVerdict(Script, VerdictKind.Fail, null, "my-own-check exited 0 without its JSON line: fatal: not a git repository"));

    /// <summary>
    /// #710. The gate of aio-dogfood#30 went red quoting a coloured line of its log, and the journal
    /// said it had exited 1 without its JSON line. The line is read with the colour codes left out.
    /// </summary>
    [Fact]
    public void A_red_gate_that_exits_1_with_colour_codes_in_its_why_is_red_with_that_why()
    {
        const string Printed =
            "{\"outcome\": \"red\", \"go\": false, \"why\": \"aspire do ci failed (exit 6): \u001b[2m00:47:13\u001b[0m \u001b[38;5;11m(npm-lint)\u001b[0m \u001b[38;5;9m✗ [ERR] Step 'npm-lint' failed.\u001b[0m (6/9 steps succeeded).\"}\n";

        var verdict = ScriptVerdicts.Read("gate", new ScriptResult(1, ScriptLine.Last(Printed), Printed));

        verdict.ShouldBe(
            new ScriptVerdict("gate", VerdictKind.Outcome, "red", "aspire do ci failed (exit 6): 00:47:13 (npm-lint) ✗ [ERR] Step 'npm-lint' failed. (6/9 steps succeeded).", Exit: 1)
        );
    }

    static ScriptResult Result(int exit, string line) => new(exit, JsonDocument.Parse(line).RootElement.Clone(), line);
}
