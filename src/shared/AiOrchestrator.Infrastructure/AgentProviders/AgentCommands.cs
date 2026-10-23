namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// A loop command, <c>/aio:&lt;name&gt; &lt;arguments&gt;</c>, as the prompt for a CLI that does not expand it.
/// <para>
/// Claude Code reads <c>.claude/commands/aio/&lt;name&gt;.md</c> and calls it <c>/aio:&lt;name&gt;</c>.
/// OpenCode 2.0.23 reads the same files through <c>.opencode/command</c> but names a nested one by its
/// path, <c>/aio/&lt;name&gt;</c>, so <c>/aio:implement</c> is sent to the model as text; Copilot CLI
/// 1.0.95 reads no command files at all. Both are given the command file itself, with its arguments in
/// place of <c>$ARGUMENTS</c>, which is what Claude Code would have expanded it to.
/// </para>
/// </summary>
static class AgentCommands
{
    const string Prefix = "/aio:";
    const string Arguments = "$ARGUMENTS";
    const string Fence = "---";

    /// <summary>
    /// The command file of <paramref name="command"/> under <paramref name="workingDirectory"/>, rendered;
    /// <paramref name="command"/> unchanged when it is not a loop command or the checkout has no such file.
    /// </summary>
    public static string Prompt(string command, string workingDirectory)
    {
        if (!command.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return command;
        }

        var rest = command[Prefix.Length..];
        var space = rest.IndexOfAny([' ', '\t', '\n']);
        var name = space < 0 ? rest : rest[..space];
        var arguments = space < 0 ? string.Empty : rest[(space + 1)..].Trim();

        // A name is one file in the commands directory, never a path out of it.
        if (name.Length == 0 || name.IndexOfAny(['/', '\\', '.']) >= 0)
        {
            return command;
        }

        var file = Path.Combine(workingDirectory, ".ai", "commands", "aio", name + ".md");
        if (!File.Exists(file))
        {
            return command;
        }

        var body = Body(File.ReadAllText(file));

        // A file that does not say where its arguments go gets them after it, as OpenCode does.
        return body.Contains(Arguments, StringComparison.Ordinal) || arguments.Length == 0
            ? body.Replace(Arguments, arguments, StringComparison.Ordinal)
            : $"{body}\n\n{arguments}";
    }

    /// <summary>The file without its front matter, which describes the command to a picker and not to the model.</summary>
    internal static string Body(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != Fence)
        {
            return text.Trim();
        }

        var end = Array.FindIndex(lines, 1, line => line.Trim() == Fence);
        return end < 0 ? text.Trim() : string.Join('\n', lines[(end + 1)..]).Trim();
    }
}
