using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// Runs one script of the harness, found by <see cref="HarnessScripts.Find"/>, and reads the JSON line it answers with. The gate,
/// the claim, the merge and every score are scripts, because what a command can decide a model
/// does not. Adapter: Local.
/// </summary>
public interface IScriptRunner
{
    /// <summary>Runs the script to its end. A non-zero exit is a result, not an exception.</summary>
    Task<ScriptResult> RunAsync(ScriptRequest request, CancellationToken ct);
}

/// <summary>A script to run.</summary>
/// <param name="Script">Its name, such as <c>gate</c>, as <c>loop.yaml</c> names it.</param>
/// <param name="WorkingDirectory">The checkout the script runs against.</param>
/// <param name="Arguments">Its arguments, one per element and unquoted.</param>
public sealed record ScriptRequest(string Script, string WorkingDirectory, IReadOnlyList<string> Arguments);

/// <summary>
/// Where a script named in <c>loop.yaml</c> is in a checkout (#729, #779). The repository's own
/// scripts are at the root of <c>.aio/</c>, <c>gate</c> and <c>archive</c> among them, and the
/// package's steps in <c>.aio/steps/</c>, so <c>loop.yaml</c> says <c>claim</c>, not where it lives,
/// and a script of the repository's wins over a step of the package's of the same name. A tool a
/// step uses underneath, not a step, has a folder of its own under <see cref="Scorers"/> (#759); it
/// is still asked for by its name.
/// <para>
/// <see cref="Legacy"/> folders are last: a repository installed before harness 37 still has every
/// script in <c>.harness/</c> until its next <i>Install harness</i> moves it to <c>.aio/</c>.
/// </para>
/// </summary>
public static class HarnessScripts
{
    /// <summary>The repository's own scripts: the gate, and any it adds.</summary>
    public const string Repository = ".aio";

    /// <summary>The package's steps, one file each.</summary>
    public const string Package = ".aio/steps";

    /// <summary>The package's model servers, each in a folder of its own: <c>laya</c> in <c>scorers/laya/</c>.</summary>
    public const string Scorers = ".aio/scorers";

    /// <summary>
    /// Where each script was before harness 37, and before harness 36 for <c>laya</c>: the root of
    /// <c>.harness/</c> was the repository's, <c>.harness/repo/</c> what only it ran,
    /// <c>.harness/aio/</c> the package's, and <c>.harness/scripts/</c> held every script until harness 24.
    /// </summary>
    public static readonly IReadOnlyList<string> Legacy = [".harness", ".harness/repo", ".harness/aio", ".harness/scripts"];

    /// <summary>The folders searched, in order, from the checkout's top.</summary>
    public static readonly IReadOnlyList<string> Folders = [Repository, Package, .. Legacy];

    /// <summary>The tools that are not steps, and the folder each lives in.</summary>
    static readonly IReadOnlyDictionary<string, string> Tools = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["laya"] = $"{Scorers}/laya",
    };

    /// <summary>The folders with a command agents type bare, <c>aio</c>: where it is, then where it was.</summary>
    public static readonly IReadOnlyList<string> CommandFolders = [Repository, ".harness/aio", ".harness/scripts"];

    /// <summary>
    /// The script's path in <paramref name="checkout"/>, or null when no folder has it. A tool is
    /// looked for in its own folder first, then where any script is: a repository that has not
    /// installed harness 37 still has <c>laya</c> in <c>.harness/aio/scorers/laya/</c>, or at the root
    /// of <c>.harness/aio/</c> before harness 36, and scores until its next <i>Install harness</i>.
    /// </summary>
    public static string? Find(string checkout, string name) =>
        (Tools.TryGetValue(name, out var own) ? [own, ".harness/aio/scorers/laya", .. Folders] : Folders)
            .Select(folder => Path.Combine(checkout, folder, name))
            .FirstOrDefault(File.Exists);

    /// <summary>
    /// A file the repository makes its own, such as <c>loop.yaml</c> or <c>version</c>: in <c>.aio/</c>,
    /// or in <c>.harness/</c> for a repository that has not reinstalled since harness 36. The path in
    /// <c>.aio/</c> when neither exists.
    /// </summary>
    public static string Own(string checkout, string name) =>
        new[] { Repository, Legacy[0] }
            .Select(folder => Path.Combine(checkout, folder, name))
            .FirstOrDefault(File.Exists)
        ?? Path.Combine(checkout, Repository, name);

    /// <summary>The folders' directories in <paramref name="checkout"/>, for a PATH that finds <c>aio</c>.</summary>
    public static IEnumerable<string> Commands(string checkout) =>
        CommandFolders.Select(folder => Path.Combine(checkout, folder));
}

/// <summary>How a script ended.</summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="Line">
/// The last line of standard output parsed as JSON, or null when it is missing or does not parse —
/// the caller says which, because a script without its line has failed.
/// </param>
/// <param name="Output">Everything the script printed, for the person reading why it failed.</param>
public sealed record ScriptResult(int ExitCode, JsonElement? Line, string Output);

/// <summary>
/// What a script printed, read the same way wherever it is read. A gate that quotes a coloured log
/// line in its why prints the terminal's escape codes inside its JSON string, which JSON does not
/// allow: on aio-dogfood#30 that red gate read as one without its line (#710).
/// </summary>
public static partial class ScriptLine
{
    /// <summary>
    /// The last non-empty line of standard output as JSON, once <see cref="Plain"/>; null when there
    /// is none or it still does not parse.
    /// </summary>
    public static JsonElement? Last(string stdout)
    {
        var line = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (line is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(Plain(line));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The text with the terminal's colour codes left out and any other control character a space.</summary>
    public static string Plain(string text)
    {
        var plain = Escapes().Replace(text, "");
        return plain.Any(char.IsControl)
            ? string.Concat(plain.Select(character => char.IsControl(character) ? ' ' : character))
            : plain;
    }

    [GeneratedRegex(@"\x1b\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex Escapes();
}
