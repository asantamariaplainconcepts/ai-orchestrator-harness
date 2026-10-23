using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// The shell commands a repository lets an agent run without asking, read from the one list it keeps
/// for Claude Code: the <c>Bash(…)</c> entries of <c>.claude/settings.json</c> (#544). OpenCode and
/// Copilot are given the same list in their own spelling, so a command that runs unasked under
/// <c>claude</c> runs unasked under them, and one that asks under <c>claude</c> asks under them too.
/// <para>
/// A repository with no such file, or one that is not JSON, allows nothing: every command asks, the
/// run waits for a person, and it is marked stale like a Claude Code run in the same place.
/// </para>
/// </summary>
/// <param name="Allowed">Commands that run without asking.</param>
/// <param name="Denied">Commands that never run.</param>
sealed partial record ShellAllowList(IReadOnlyList<ShellRule> Allowed, IReadOnlyList<ShellRule> Denied)
{
    public static readonly ShellAllowList Empty = new([], []);

    /// <summary>The list of the checkout at <paramref name="workingDirectory"/>.</summary>
    public static ShellAllowList Read(string workingDirectory)
    {
        var file = Path.Combine(workingDirectory, ".claude", "settings.json");
        if (!File.Exists(file))
        {
            return Empty;
        }

        try
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!settings.RootElement.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            return new ShellAllowList(Rules(permissions, "allow"), Rules(permissions, "deny"));
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    static List<ShellRule> Rules(JsonElement permissions, string key) =>
        permissions.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(entry => entry.ValueKind == JsonValueKind.String ? Rule(entry.GetString()!) : null).OfType<ShellRule>()]
            : [];

    /// <summary><c>Bash(git:*)</c> is every <c>git</c> command; <c>Bash(npm test)</c> is that one command.</summary>
    internal static ShellRule? Rule(string entry)
    {
        var match = BashEntry().Match(entry);
        if (!match.Success)
        {
            return null;
        }

        var command = match.Groups["command"].Value.Trim();
        var prefix = command.EndsWith(":*", StringComparison.Ordinal);
        command = prefix ? command[..^2].TrimEnd() : command;

        return command.Length == 0 ? null : new ShellRule(command, prefix);
    }

    [GeneratedRegex(@"^Bash\((?<command>.+)\)$")]
    private static partial Regex BashEntry();
}

/// <summary>One shell command a repository allows or denies.</summary>
/// <param name="Command">The command as typed, such as <c>gh pr view</c>.</param>
/// <param name="Prefix">Whether any command that starts with it matches, rather than only that one.</param>
sealed record ShellRule(string Command, bool Prefix);
