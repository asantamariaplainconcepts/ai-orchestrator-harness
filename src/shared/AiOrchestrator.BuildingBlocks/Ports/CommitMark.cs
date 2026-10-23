namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// #741. How a commit says it is the loop's. The engine, its runs and the person commit with one git
/// identity on one machine, so the author tells nobody apart; the committer does. A run's commits are
/// committed by <c>aio run &lt;id&gt;</c> and a script's by <c>aio script &lt;name&gt;</c>, both at
/// <see cref="Email"/>, through <c>GIT_COMMITTER_NAME</c> and <c>GIT_COMMITTER_EMAIL</c> in what each is
/// started with. The author is left alone, so <c>git log</c> reads as before, and GitHub keeps the
/// committer of a pull request's commits. A path that cannot keep the committer says the same in a
/// trailer, <c>Aio-Run: &lt;id&gt;</c> or <c>Aio-Script: &lt;name&gt;</c>, and a reader takes either.
/// </summary>
public static class CommitMark
{
    /// <summary>An address nobody receives mail at: <c>.invalid</c> is reserved for that.</summary>
    public const string Email = "noreply@aio.invalid";

    public const string NameVariable = "GIT_COMMITTER_NAME";

    public const string EmailVariable = "GIT_COMMITTER_EMAIL";

    const string RunPrefix = "aio run ";

    const string ScriptPrefix = "aio script ";

    static readonly string[] TrailerKeys = ["Aio-Run:", "Aio-Script:"];

    /// <summary>The variables a run's agent is started with.</summary>
    public static IReadOnlyDictionary<string, string> Run(Guid id) => Committer(RunPrefix + id.ToString("D"));

    /// <summary>The variables a script of the engine is started with.</summary>
    public static IReadOnlyDictionary<string, string> Script(string name) => Committer(ScriptPrefix + name);

    /// <summary>Whether a commit carries the loop's mark, in its committer or in a trailer.</summary>
    public static bool Marked(string? committer, IEnumerable<string> trailers) =>
        committer is not null && (committer.StartsWith(RunPrefix, StringComparison.Ordinal) || committer.StartsWith(ScriptPrefix, StringComparison.Ordinal))
        || trailers.Any(trailer => TrailerKeys.Any(key => trailer.StartsWith(key, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// The trailers of a commit message as git writes them: the <c>Key: value</c> lines of its last
    /// paragraph, when that paragraph is not the subject.
    /// </summary>
    public static IReadOnlyList<string> TrailersOf(string? message)
    {
        var paragraphs = (message ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Split("\n\n");
        if (paragraphs.Length < 2)
        {
            return [];
        }

        return [.. paragraphs[^1].Split('\n').Select(line => line.Trim()).Where(IsTrailer)];
    }

    static bool IsTrailer(string line)
    {
        var colon = line.IndexOf(": ", StringComparison.Ordinal);
        return colon > 0 && line[..colon].All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    static Dictionary<string, string> Committer(string name) =>
        new(StringComparer.Ordinal) { [NameVariable] = name, [EmailVariable] = Email };
}
