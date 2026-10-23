using AiOrchestrator.Modules.Runs.Features.PbiRuns;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #764. The screen a run ended for its silence quotes, rebuilt from what Claude Code printed: the
/// sequences here are the ones the review of 15 October drew its last screen with.
/// </summary>
public sealed class TerminalScreenTests
{
    [Fact]
    public void A_spinner_repainted_in_place_leaves_the_line_it_ended_on()
    {
        var screen = new TerminalScreen(60, 8);

        screen.Write("  To finish, either:\r\n  - allow the commit and push, or\r\n  - run it yourself.\r\n\r\n");
        screen.Write("\u001b[38;2;215;119;87m✢\u001b[3GNebulizing…\u001b[39m\r\r\n\u001b[2C\u001b[1A");
        screen.Write("\u001b[?2026h\u001b[?25l\r\u001b[38;2;215;119;87m✳\u001b[3GNebulizing… 7\u001b[39m\u001b[?2026l");
        screen.Write("\u001b]0;✳ Claude Code\u0007\r\u001b[38;2;153;153;153m✻\u001b[3GBrewed for 57s · done 20:11\u001b[39m\u001b[K\r\r\n");
        screen.Write("────────────\r\n❯ \r\n\u001b]9;Claude is waiting for your input\u0007");

        screen.LastLines(3).ShouldBe(["- allow the commit and push, or", "- run it yourself.", "✻ Brewed for 57s · done 20:11"]);
    }

    [Fact]
    public void A_screen_that_scrolls_keeps_its_last_rows_and_wraps_at_its_width()
    {
        var screen = new TerminalScreen(10, 3);

        screen.Write("one\r\ntwo\r\nthree\r\nfour and more\r\n");

        screen.LastLines(5).ShouldBe(["four and m", "ore"]);
    }

    [Fact]
    public void A_sequence_split_between_two_chunks_is_read_whole()
    {
        var screen = new TerminalScreen(20, 2);

        screen.Write("abcdef\u001b[");
        screen.Write("3Gxy\u001b[");
        screen.Write("K");

        screen.LastLines(1).ShouldBe(["abxy"]);
    }
}
