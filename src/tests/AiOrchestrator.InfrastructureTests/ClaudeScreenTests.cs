using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.AgentProviders;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #706. What Claude Code's terminal says when it waits for a person, read from the two sessions of
/// 13 October that waited 27 minutes for a login and an hour for a permission, and from a run of
/// 2.1.288 in acceptEdits on 14 October asked for <c>python3 -c</c>. The fixtures are their recordings,
/// trimmed to the chunks around the prompt.
/// </summary>
public sealed class ClaudeScreenTests
{
    static readonly ClaudeCodeProvider Claude = new();

    /// <summary>What the session printed, in order, as the run's tail holds it.</summary>
    static string Printed(string cast)
    {
        var printed = new StringBuilder();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Screens", cast)).Skip(1))
        {
            using var chunk = JsonDocument.Parse(line);
            printed.Append(chunk.RootElement[2].GetString());
        }

        return printed.ToString();
    }

    /// <summary>A working agent's spinner, repainted once a second, as 2.1.288 draws it.</summary>
    static string Working(int seconds) =>
        string.Concat(
            Enumerable.Range(1, seconds).Select(second =>
                $"\u001b[?25l\u001b[2D\u001b[3B\r\u001b[6A\u001b[38;2;215;119;87m✻\u001b[3G\u001b[38;2;230;149;117mGerminating…\u001b[38;2;153;153;153m (running {second}s · ↓ 863 tokens · thought for 1s)\u001b[39m\u001b[K\r\r\n"
            )
        );

    [Fact]
    public void An_expired_login_is_a_login_to_give()
    {
        var wait = Claude.WaitsForPerson(Printed("login-expired.cast"));

        wait.ShouldNotBeNull();
        wait.Kind.ShouldBe(PersonWaitKind.Login);
        wait.Why.ShouldBe("Claude Code is not logged in. Run `claude /login` on this machine, then release.");
    }

    [Fact]
    public void A_permission_prompt_names_the_command_it_asked_for()
    {
        var wait = Claude.WaitsForPerson(Printed("permission-prompt.cast"));

        wait.ShouldNotBeNull();
        wait.Kind.ShouldBe(PersonWaitKind.Permission);
        wait.Why.ShouldStartWith(
            "The agent asked to run `.aio/aio issue view 30 | python3 -c \"import json,sys; print(json.load(sys.stdin)['body'])\" > .aio/body.md && cat .aio/body.md`."
        );
        wait.Why.ShouldEndWith("Switch permissions to auto in Settings or add it to the allow list of .claude/settings.json, then release.");
    }

    /// <summary>A run of 2.1.288 in acceptEdits on 14 October drew the prompt with no tool call above it.</summary>
    [Fact]
    public void A_permission_prompt_with_no_tool_call_above_it_still_names_the_command()
    {
        var wait = Claude.WaitsForPerson(Printed("permission-prompt-without-the-call.cast"));

        wait.ShouldNotBeNull();
        wait.Kind.ShouldBe(PersonWaitKind.Permission);
        wait.Why.ShouldStartWith("The agent asked to run `python3 -c \"print(706)\"`.");
    }

    [Theory]
    [InlineData("Not logged in · Please run /login")]
    [InlineData("Login expired · Please run /login")]
    public void Each_login_phrase_is_a_login(string phrase) =>
        Claude.WaitsForPerson($"\u001b[38;2;255;193;7m⏺\u001b[3G{phrase}\u001b[39m\u001b[K\r\n❯ ").ShouldNotBeNull().Kind.ShouldBe(PersonWaitKind.Login);

    [Fact]
    public void A_prompt_the_agent_went_on_from_is_not_a_wait()
    {
        Claude.WaitsForPerson(Printed("permission-prompt.cast") + Working(20)).ShouldBeNull();
        Claude.WaitsForPerson(Printed("login-expired.cast") + Working(20)).ShouldBeNull();
    }

    [Fact]
    public void A_screen_without_a_phrase_is_not_a_wait() => Claude.WaitsForPerson(Working(3)).ShouldBeNull();

    [Fact]
    public void Cursor_moves_read_as_spaces() =>
        ClaudeScreen.Plain("\u001b[2GDo\u001b[5Gyou\u001b[9Gwant\u001b[14Gto\u001b[17Gproceed?\r\r\n").ShouldBe(" Do you want to proceed? ");
}
