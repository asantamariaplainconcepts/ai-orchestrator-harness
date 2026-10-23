using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// What Claude Code's terminal says when it waits for a person (#706). On 13 October two unattended
/// runs sat 27 minutes at <c>Login expired · Please run /login</c> and an hour at <c>Do you want to
/// proceed?</c> for a <c>python3 -c</c> outside the allow list, and only went stale.
/// <para>
/// The TUI places words with cursor moves rather than spaces (<c>Do\e[5Gyou\e[9Gwant</c>), so a cursor
/// move reads as a space and a colour as nothing before a phrase is looked for. The phrase must be in
/// the last <see cref="Recent"/> characters of that text: a working agent repaints its spinner every
/// second, so a phrase it printed and went on from scrolls out of them long before the session falls
/// silent.
/// </para>
/// </summary>
static partial class ClaudeScreen
{
    /// <summary>The prompt with its four answers and a long command fits; ten seconds of spinner do not.</summary>
    const int Recent = 1000;

    /// <summary>The longest command the hold's reason quotes; past it, an ellipsis.</summary>
    const int Quoted = 200;

    /// <summary>The dashed rule Claude Code draws above and below the command it asks about.</summary>
    const char Rule = '╌';

    /// <summary>How far before the question the rule under the command may be: room for "This command requires approval".</summary>
    const int Approval = 80;

    static readonly string[] Permission = ["Do you want to proceed?"];

    static readonly string[] Login = ["Not logged in", "Login expired", "Please run /login"];

    public static PersonWait? Waits(string screen)
    {
        var plain = Plain(screen);
        var from = Math.Max(0, plain.Length - Recent);

        if (Login.Any(phrase => plain.IndexOf(phrase, from, StringComparison.Ordinal) >= 0))
        {
            return new(PersonWaitKind.Login, "Claude Code is not logged in. Run `claude /login` on this machine, then release.");
        }

        var asked = Permission.Max(phrase => plain.LastIndexOf(phrase, StringComparison.Ordinal));
        if (asked < from)
        {
            return null;
        }

        return new(
            PersonWaitKind.Permission,
            Asked(plain[..asked]) is { } command
                ? $"The agent asked to run `{command}`. Switch permissions to auto in Settings or add it to the allow list of .claude/settings.json, then release."
                : "The agent asked for a permission. Switch permissions to auto in Settings, then release."
        );
    }

    /// <summary>
    /// The command the prompt is about. Claude Code draws it between two dashed rules right above
    /// "This command requires approval", with <c>│</c> before each line when it wraps, and draws the
    /// tool call above the prompt only sometimes: the run of 13 October had it, a run of 2.1.288 in
    /// <c>acceptEdits</c> on 14 October did not.
    /// </summary>
    static string? Asked(string before)
    {
        var closing = before.LastIndexOf(Rule);
        if (closing < 0 || before.Length - closing > Approval)
        {
            return null;
        }

        while (closing > 0 && before[closing - 1] == Rule)
        {
            closing--;
        }

        var opening = before.LastIndexOf(Rule, Math.Max(0, closing - 1));
        if (opening < 0)
        {
            return null;
        }

        var what = Whitespace().Replace(before[(opening + 1)..closing].Replace('│', ' '), " ").Trim();
        if (what.Length == 0)
        {
            return null;
        }

        return what.Length > Quoted ? what[..Quoted] + "…" : what;
    }

    /// <summary>The screen as words: escapes out, a cursor move as a space, whitespace collapsed.</summary>
    internal static string Plain(string screen)
    {
        var text = Osc().Replace(screen, string.Empty);
        text = Sgr().Replace(text, string.Empty);
        text = Csi().Replace(text, " ");
        text = OtherEscape().Replace(text, string.Empty);
        return Whitespace().Replace(text, " ");
    }

    [GeneratedRegex(@"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)?")]
    private static partial Regex Osc();

    [GeneratedRegex(@"\x1b\[[0-9;:]*m")]
    private static partial Regex Sgr();

    [GeneratedRegex(@"\x1b\[[0-9;?<>=]*[ -/]*[@-~]")]
    private static partial Regex Csi();

    [GeneratedRegex(@"\x1b.?", RegexOptions.Singleline)]
    private static partial Regex OtherEscape();

    [GeneratedRegex(@"[\s ]+")]
    private static partial Regex Whitespace();
}
