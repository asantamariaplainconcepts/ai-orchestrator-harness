using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// Marks a run's directory as trusted by Copilot CLI before it starts there, and takes it back after.
/// <para>
/// Interactive <c>copilot</c> 1.0.95 in a directory it has not been told about opens on "Confirm folder
/// trust" and waits for a key; <c>--add-dir</c> does not skip it, and <c>COPILOT_ALLOW_ALL=true</c> does
/// only by also approving every tool. The answer it keeps is the <c>trustedFolders</c> array of
/// <c>~/.copilot/config.json</c> (<c>$COPILOT_HOME/config.json</c> when that is set), and an entry holds
/// for every directory below it. Measured: an entry for a directory took the menu away there and in its
/// subdirectory, and the same entry in <c>settings.json</c> did not.
/// </para>
/// <para>
/// The file starts with <c>//</c> comment lines, which are kept as they are. The array holds bare
/// paths, so what this wrote is listed beside it in <see cref="Ledger"/>: <see cref="Retire"/> removes
/// only a path it added, and a restart between the run and its retirement loses nothing.
/// </para>
/// </summary>
static class CopilotTrust
{
    const string Folders = "trustedFolders";

    /// <summary>The paths this control plane added to the array, beside the file it added them to.</summary>
    internal const string Ledger = "aio-trusted-folders.json";

    /// <summary>Where Copilot CLI keeps its configuration and state for this user.</summary>
    public static string DefaultHome =>
        Environment.GetEnvironmentVariable("COPILOT_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");

    /// <summary>Adds <paramref name="directory"/> to the array unless it or a parent is there. Returns whether it wrote.</summary>
    public static bool Ensure(string home, string directory)
    {
        var file = Path.Combine(home, "config.json");
        var path = Real(directory);
        var (header, root) = Read(file, path);

        if (root[Folders] is not JsonArray folders)
        {
            folders = [];
            root[Folders] = folders;
        }

        if (Paths(folders).Any(trusted => Covers(trusted, path)))
        {
            return false;
        }

        folders.Add(path);
        Write(file, header, root);

        var ledger = Owned(home);
        if (!ledger.Contains(path))
        {
            ledger.Add(path);
        }

        WriteLedger(home, ledger);
        return true;
    }

    /// <summary>
    /// Takes <paramref name="directory"/> out of the array when <see cref="Ensure"/> put it there, and
    /// leaves every other entry. Call it while the directory is still on disk, so its path resolves as
    /// it did when it was trusted. Returns whether it wrote.
    /// </summary>
    public static bool Retire(string home, string directory)
    {
        var path = Real(directory);
        var ledger = Owned(home);
        if (!ledger.Remove(path))
        {
            return false;
        }

        var file = Path.Combine(home, "config.json");
        if (File.Exists(file))
        {
            var (header, root) = Read(file, path);
            if (root[Folders] is JsonArray folders)
            {
                foreach (var entry in folders.Where(entry => entry is JsonValue value && value.TryGetValue(out string? text) && text == path).ToList())
                {
                    folders.Remove(entry);
                }

                Write(file, header, root);
            }
        }

        WriteLedger(home, ledger);
        return true;
    }

    static string Real(string directory) => Path.TrimEndingDirectorySeparator(NativePaths.RealPath(directory) ?? directory);

    static bool Covers(string trusted, string path)
    {
        var parent = Path.TrimEndingDirectorySeparator(trusted);
        return path == parent || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    static IEnumerable<string> Paths(JsonArray folders) =>
        folders.OfType<JsonValue>().Select(value => value.TryGetValue(out string? text) ? text : null).OfType<string>();

    /// <summary>The leading comment lines, and the object after them.</summary>
    static (string Header, JsonObject Root) Read(string file, string path)
    {
        if (!File.Exists(file))
        {
            return (string.Empty, []);
        }

        var lines = File.ReadAllLines(file);
        var comments = lines.TakeWhile(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal)).Count();
        var header = string.Concat(lines[..comments].Select(line => line + "\n"));

        try
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            return (header, JsonNode.Parse(string.Join('\n', lines[comments..]), documentOptions: options) as JsonObject ?? []);
        }
        catch (JsonException malformed)
        {
            throw new InvalidOperationException(
                $"{file} is not JSON, so {path} could not be marked trusted for Copilot CLI: {malformed.Message}",
                malformed
            );
        }
    }

    static List<string> Owned(string home)
    {
        var file = Path.Combine(home, Ledger);
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? [] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static void WriteLedger(string home, List<string> owned)
    {
        var file = Path.Combine(home, Ledger);
        if (owned.Count == 0)
        {
            File.Delete(file);
            return;
        }

        Replace(file, JsonSerializer.Serialize(owned));
    }

    static void Write(string file, string header, JsonObject root) =>
        Replace(file, header + root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

    /// <summary>Through a rename, so Copilot never reads half a file; the copy keeps the original's mode.</summary>
    static void Replace(string file, string text)
    {
        var temporary = $"{file}.aio-{Guid.NewGuid():N}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(temporary, text);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                temporary,
                File.Exists(file) ? File.GetUnixFileMode(file) : UnixFileMode.UserRead | UnixFileMode.UserWrite
            );
        }

        File.Move(temporary, file, overwrite: true);
    }
}
