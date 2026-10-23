using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// Reads whether Claude Code trusts a folder, and takes back the trust this control plane used to write.
/// <para>
/// Interactive <c>claude</c> in a directory it has never seen opens on "Is this a project you
/// created or one you trust?" and waits for a key. The answer Claude Code keeps is
/// <c>projects[&lt;path&gt;].hasTrustDialogAccepted</c> in <c>~/.claude.json</c>
/// (<c>$CLAUDE_CONFIG_DIR/.claude.json</c> when that is set). That answer is the person's to give, so
/// this class never adds or changes an entry: the engine reads <see cref="IsTrusted"/> and waits on an
/// untrusted folder (#606), and a session a person opens shows the question to that person (#608).
/// </para>
/// <para>
/// <b>What was written before is still taken back (#546).</b> Until #608 every run wrote its
/// directory, and its main checkout, as trusted, each entry carrying <see cref="Mark"/>: the directory
/// it was written for, and what the entry was before. When that directory's worktree is retired,
/// <see cref="Retire"/> undoes exactly those entries, so the file of someone who used the app then
/// keeps no orphans.
/// </para>
/// </summary>
static class ClaudeTrust
{
    const string Projects = "projects";
    const string Accepted = "hasTrustDialogAccepted";

    /// <summary>The key beside <see cref="Accepted"/> that says this control plane wrote it before #608, and for which directory.</summary>
    internal const string Mark = "aiOrchestratorTrust";

    const string For = "for";
    const string Created = "created";
    const string Was = "was";

    /// <summary>Where Claude Code keeps its global configuration for this user.</summary>
    public static string DefaultFile =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } directory
            ? Path.Combine(directory, ".claude.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <summary>
    /// Whether Claude Code would start in <paramref name="directory"/> without asking to trust it,
    /// read from <paramref name="file"/> and never written (#606).
    /// <para>
    /// <b>A parent counts only up to the repository's top.</b> Read from 2.1.288's own check: it walks
    /// up from the directory to its git root and stops there, and besides takes the main checkout of a
    /// linked worktree. So <c>~/projects</c> trusted does not make <c>~/projects/repo</c> trusted, and
    /// that is the case the old write's wider walk missed: on 10 October the first run in
    /// <c>aio-dogfood</c> sat 80 minutes on the question under a trusted <c>~/projects</c>. Outside a
    /// repository every parent counts. A file missing or not JSON trusts nothing.
    /// </para>
    /// </summary>
    public static bool IsTrusted(string file, string directory)
    {
        JsonObject? projects;
        try
        {
            projects = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject root ? root[Projects] as JsonObject : null;
        }
        catch (JsonException)
        {
            return false;
        }

        if (projects is null)
        {
            return false;
        }

        var path = Path.TrimEndingDirectorySeparator(NativePaths.RealPath(directory) ?? directory);
        if (MainCheckout(path) is { } main && Accepts(projects, main))
        {
            return true;
        }

        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (Accepts(projects, current))
            {
                return true;
            }

            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Takes back the entries marked as written for <paramref name="directory"/>: one created by the
    /// write goes, and one it only marked trusted gets its earlier answer back. Entries without the
    /// mark, and those marked for another directory, stay. Call it while the directory is still on
    /// disk, so its path resolves as it did when it was trusted. Returns whether it wrote.
    /// </summary>
    public static bool Retire(string file, string directory)
    {
        // A dotfiles checkout links the file; the rename in Write replaces what the link points to.
        file = File.Exists(file) ? new FileInfo(file).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file : file;
        if (!File.Exists(file))
        {
            return false;
        }

        var path = Path.TrimEndingDirectorySeparator(NativePaths.RealPath(directory) ?? directory);
        JsonObject root;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject read)
            {
                return false;
            }
            root = read;
        }
        catch (JsonException malformed)
        {
            throw new InvalidOperationException(
                $"{file} is not JSON, so the trust written for {path} could not be taken back: {malformed.Message}",
                malformed
            );
        }

        if (root[Projects] is not JsonObject projects)
        {
            return false;
        }

        var written = projects
            .Where(project => project.Value is JsonObject entry && entry[Mark] is JsonObject mark
                && mark[For] is JsonValue owner && owner.TryGetValue(out string? value) && value == path)
            .Select(project => project.Key)
            .ToList();

        if (written.Count == 0)
        {
            return false;
        }

        foreach (var key in written)
        {
            var project = (JsonObject)projects[key]!;
            var mark = (JsonObject)project[Mark]!;

            if (mark[Created] is JsonValue created && created.TryGetValue(out bool wasCreated) && wasCreated)
            {
                projects.Remove(key);
                continue;
            }

            project.Remove(Mark);
            if (mark[Was] is { } was)
            {
                project[Accepted] = was.DeepClone();
            }
            else
            {
                project.Remove(Accepted);
            }
        }

        Write(file, root);
        return true;
    }

    /// <summary>
    /// The main checkout of a linked worktree, read from the <c>.git</c> file git leaves in it
    /// (<c>gitdir: &lt;main&gt;/.git/worktrees/&lt;name&gt;</c>), or null for anything else.
    /// </summary>
    internal static string? MainCheckout(string directory)
    {
        var marker = Path.Combine(directory, ".git");

        if (!File.Exists(marker))
        {
            return null;
        }

        var line = File.ReadLines(marker).FirstOrDefault() ?? string.Empty;
        const string Prefix = "gitdir:";

        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var gitdir = Path.GetFullPath(line[Prefix.Length..].Trim(), directory);
        var worktrees = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(gitdir));
        var common = Path.GetDirectoryName(worktrees ?? string.Empty);

        return Path.GetFileName(worktrees) == "worktrees" && Path.GetFileName(common) == ".git"
            ? Path.GetDirectoryName(common)
            : null;
    }

    static bool Accepts(JsonObject projects, string path) =>
        projects[path] is JsonObject project && project[Accepted] is JsonValue value
            && value.TryGetValue(out bool accepted) && accepted;

    static void Write(string file, JsonObject root)
    {
        // A whole new file through a rename, so Claude Code never reads half of it.
        var temporary = $"{file}.aio-{Guid.NewGuid():N}";
        File.WriteAllText(temporary, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // The file is the user's alone (0600), and the copy that replaces it stays that way.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, File.GetUnixFileMode(file));
        }

        File.Move(temporary, file, overwrite: true);
    }
}
