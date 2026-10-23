namespace AiOrchestrator.Infrastructure;

/// <summary>
/// Resolves a path to what the filesystem calls it, the way orca's `realpathSync.native` behind an
/// `existsSync` guard does: every symlink in the chain followed, and the raw string back when any
/// step cannot be read. `DirectoryInfo.ResolveLinkTarget` alone is not this — it resolves only
/// when the final component is itself a link, and on macOS the link that matters is usually higher
/// up: <c>/var</c> → <c>/private/var</c>, <c>/tmp</c> → <c>/private/tmp</c>. A CLI that realpaths
/// its working directory before writing it down (OpenCode does, into its session record) will
/// never compare equal to the path a run was handed until both sides are walked the same way.
/// </summary>
static class NativePaths
{
    /// <summary>
    /// The path with every symlink in it resolved, segment by segment, or <c>null</c> when the
    /// path does not exist or a link cannot be followed — the caller falls back to what it had,
    /// which still compares equal to itself.
    /// </summary>
    public static string? RealPath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return null;
        }

        var root = Path.GetPathRoot(path) ?? string.Empty;
        var current = root;
        var rest = path[root.Length..];

        foreach (var segment in rest.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);

            var info = (FileSystemInfo?)(Directory.Exists(current)
                ? new DirectoryInfo(current)
                : File.Exists(current)
                    ? new FileInfo(current)
                    : null);

            if (info is null)
            {
                return null;
            }

            if (info.LinkTarget is not null)
            {
                var resolved = info.ResolveLinkTarget(returnFinalTarget: true);

                if (resolved is null)
                {
                    return null;
                }

                current = resolved.FullName;
            }
        }

        return current;
    }
}
