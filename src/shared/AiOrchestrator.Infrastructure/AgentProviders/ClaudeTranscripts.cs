namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// Where Claude Code keeps what it wrote about itself, and how to find one run's file in it.
/// <para>
/// <b>Extracted rather than copied.</b> <see cref="ClaudeUsageReader"/> paid for every rule here —
/// the exact lookup by pinned session id, the modification-time narrowing that keeps a scan off a
/// tree with thousands of files, the shared-read open that does not fail on a file the agent is
/// still appending to, and the case rule that makes two spellings of one directory compare equal.
/// <see cref="ClaudeTranscriptReader"/> needs the same file for a different question, and a second
/// implementation of "find it" is a second thing to get wrong in a way nothing looks wrong about.
/// </para>
/// </summary>
static class ClaudeTranscripts
{
    /// <summary>
    /// The default location. A parameter everywhere it is used, so a test can point at a directory
    /// it wrote itself — these readers are either exercised against known input or not at all.
    /// </summary>
    public static string DefaultRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "projects"
        );

    /// <summary>
    /// The transcript named after a pinned session, when the vendor honoured the pin. Searched by
    /// filename rather than by reading, so a miss costs a directory listing and not a parse.
    /// </summary>
    /// <param name="sessionId">
    /// As the run recorded it. Rejected unless it is a bare name: this becomes a search pattern, and
    /// a value carrying a separator would walk out of the root.
    /// </param>
    public static string? Exact(string root, string? sessionId)
    {
        if (
            string.IsNullOrWhiteSpace(sessionId)
            || sessionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || sessionId.Contains(Path.DirectorySeparatorChar)
            || sessionId.Contains(Path.AltDirectorySeparatorChar)
        )
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(root, $"{sessionId}.jsonl", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Candidate transcript files, narrowed by modification time before anything is opened. A file
    /// last written before the run began cannot contain one of its rows, and a developer's
    /// transcript tree runs to thousands of files — so this is the difference between a scan that
    /// costs milliseconds and one that costs seconds on every finished run.
    /// </summary>
    public static IEnumerable<string> Since(string root, DateTimeOffset from)
    {
        IEnumerable<string> files;

        try
        {
            files = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            DateTimeOffset written;

            try
            {
                written = File.GetLastWriteTimeUtc(file);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (written >= from)
            {
                yield return file;
            }
        }
    }

    public static async IAsyncEnumerable<string> Lines(
        string file,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        StreamReader reader;

        try
        {
            // Shared read: Claude Code may well be appending to this file right now, and opening
            // it exclusively would fail exactly when the run just finished.
            reader = new StreamReader(
                new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                )
            );
        }
        catch (Exception)
        {
            yield break;
        }

        using (reader)
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length > 0)
                {
                    yield return line;
                }
            }
        }
    }

    public static string Normalize(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    /// <summary>True when <paramref name="child"/> is the directory itself or sits beneath it.</summary>
    public static bool Contains(string parent, string child) =>
        child.Equals(parent, Comparison)
        || child.StartsWith(parent + Path.DirectorySeparatorChar, Comparison);

    /// <summary>
    /// macOS and Windows both have case-insensitive filesystems by default, and a path that differs
    /// only in case is the same directory. Linux is the exception.
    /// </summary>
    public static readonly StringComparison Comparison = OperatingSystem.IsLinux()
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;
}
