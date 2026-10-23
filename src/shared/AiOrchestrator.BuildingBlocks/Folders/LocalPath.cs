namespace AiOrchestrator.BuildingBlocks.Folders;

/// <summary>
/// The one rule for turning a path a person typed into one the filesystem accepts.
/// <para>
/// It lives here, beside the seams rather than inside one, because two sides need the same answer
/// and must not be able to disagree: the implementations that shell out to git, and the module
/// code that decides whether a folder is worth storing. A second copy of this rule is a folder
/// that one of them can see and the other cannot.
/// </para>
/// </summary>
public static class LocalPath
{
    /// <summary>
    /// Expands a leading <c>~</c>. Everything else is returned trimmed and unchanged — this is
    /// deliberately not a normaliser, because a relative path is a caller's mistake and should
    /// stay visible as one rather than being resolved against whatever the current directory
    /// happens to be.
    /// </summary>
    public static string Expand(string path)
    {
        var trimmed = path.Trim();

        if (trimmed == "~")
        {
            return Home;
        }

        // Only a leading "~/" — a file legitimately named "~weird" is not a home directory.
        return trimmed.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Home, trimmed[2..])
            : trimmed;
    }

    /// <summary>Whether this path is a directory that exists right now.</summary>
    public static bool DirectoryExists(string path) => Directory.Exists(Expand(path));

    /// <summary>
    /// Whether two paths name the same place once symbolic links are followed.
    /// <para>
    /// Git answers with the real path, and a person types the one they see: on macOS the temp
    /// folder is <c>/var/…</c> to the user and <c>/private/var/…</c> to git, so a plain string
    /// comparison listed a repository's own checkout as one of its worktrees.
    /// </para>
    /// </summary>
    public static bool Same(string left, string right) =>
        string.Equals(
            Real(left),
            Real(right),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
        );

    /// <summary>
    /// The full path with every linked directory along it resolved. <see cref="FileSystemInfo.ResolveLinkTarget"/>
    /// only follows the last segment, so each one is resolved in turn; segments that do not exist
    /// are kept as written.
    /// </summary>
    static string Real(string path)
    {
        var full = Path.GetFullPath(Expand(path));
        var current = Path.GetPathRoot(full) ?? string.Empty;

        foreach (
            var segment in full[current.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            current = Path.Combine(current, segment);

            try
            {
                // The target is written as the link stored it, so it can run through links of its
                // own — a link to /var/… is still not /private/var/… until it is resolved too.
                if (new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    current = Real(target.FullName);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Unreadable is not a reason to call two paths different; keep the segment as written.
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
