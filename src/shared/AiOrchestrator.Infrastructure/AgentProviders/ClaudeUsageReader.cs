using System.Text.Json;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// Reads what Claude Code consumed, from the transcripts it writes for itself.
/// <para>
/// Claude Code appends one JSON object per line to
/// <c>~/.claude/projects/&lt;encoded-cwd&gt;/&lt;session&gt;.jsonl</c>, and every assistant row carries a
/// <c>usage</c> object with four counts. That file is the only place the number exists: in
/// non-interactive mode nothing is printed, and in interactive mode what you see is a TUI repaint.
/// Prior art: orca's <c>src/main/claude-usage/scanner.ts</c>, which scans the same tree.
/// </para>
/// <para>
/// <b>Rows repeat, and the repeats are not duplicates to discard blindly.</b> Claude Code streams
/// several assistant rows carrying the same message and request ids, and the later ones can hold
/// more complete usage than the first — so the same key keeps the <i>maximum</i> of each count
/// rather than the first or the last. Summing them instead would multiply a run's cost by however
/// many times it streamed. That rule is orca's, and it is the single non-obvious thing here.
/// </para>
/// </summary>
/// <para>
/// <b>Finding the file is <see cref="ClaudeTranscripts"/>' job, not this one's.</b> It was this
/// class's until #80 needed the same file for the conversation rather than for the counts; the
/// rules did not change, they moved so there is one of them.
/// </para>
/// <param name="root">
/// Where Claude Code keeps its transcripts. Defaults to the real location and is a parameter only
/// so a test can point at a directory it wrote itself — the dedupe rule below is the kind of thing
/// that is either exercised against known input or not exercised at all.
/// </param>
sealed class ClaudeUsageReader(string? root = null)
{
    readonly string _root = root ?? ClaudeTranscripts.DefaultRoot;

    /// <summary>
    /// Rows are stamped when the model answered, which is a moment or two after we spawned the
    /// process and can be a moment after we saw it exit. The window is widened at both ends so a
    /// run does not lose its last answer to a clock.
    /// </summary>
    static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);

    public async Task<MeasuredUsage?> Read(
        UsageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var root = _root;

        if (!Directory.Exists(root))
        {
            // Claude Code has never run as this user. Not an error — the run may not have been
            // Claude at all, and "unknown" is the honest answer either way.
            return null;
        }

        var from = query.From - Margin;
        var to = query.To + Margin;
        var directory = ClaudeTranscripts.Normalize(query.WorkingDirectory);

        // The run pins Claude Code's session id to its own (`--session-id`), so the transcript is
        // named after it. When that file exists it is the whole answer, and an exact one: no other
        // session can be inside it. The directory scan below is the fallback for agents that name
        // their own sessions.
        var exact = query.SessionId == Guid.Empty
            ? null
            : ClaudeTranscripts.Exact(root, query.SessionId.ToString("D"));

        var totals = new Dictionary<string, Row>(StringComparer.Ordinal);
        var loose = new Row();
        string? model = null;

        foreach (var file in exact is null ? ClaudeTranscripts.Since(root, from) : [exact])
        {
            cancellationToken.ThrowIfCancellationRequested();

            await foreach (var line in ClaudeTranscripts.Lines(file, cancellationToken))
            {
                if (!TryParse(line, directory, from, to, out var parsed))
                {
                    continue;
                }

                model ??= parsed.Model;

                if (parsed.Key is null)
                {
                    // No stable identity to deduplicate on. Counted once, because a row we cannot
                    // key is a row we will not see again.
                    loose.Add(parsed);
                    continue;
                }

                if (totals.TryGetValue(parsed.Key, out var existing))
                {
                    existing.Max(parsed);
                    continue;
                }

                var row = new Row();
                row.Add(parsed);
                totals[parsed.Key] = row;
            }
        }

        var input = loose.Input + totals.Values.Sum(row => row.Input);
        var output = loose.Output + totals.Values.Sum(row => row.Output);
        var cacheRead = loose.CacheRead + totals.Values.Sum(row => row.CacheRead);
        var cacheWrite = loose.CacheWrite + totals.Values.Sum(row => row.CacheWrite);

        if (input + output + cacheRead + cacheWrite == 0)
        {
            return null;
        }

        return ModelPrices.Priced(
            new MeasuredUsage(model, input, output, cacheRead, cacheWrite, null)
        );
    }

    /// <summary>
    /// Pulls one assistant row's usage out, or declines. A malformed line is skipped rather than
    /// fatal: these files are appended to by another process, so reading a half-written last line
    /// is expected rather than exceptional.
    /// </summary>
    static bool TryParse(
        string line,
        string directory,
        DateTimeOffset from,
        DateTimeOffset to,
        out Parsed parsed
    )
    {
        parsed = default;

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var element = document.RootElement;

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (Text(element, "type") != "assistant")
            {
                return false;
            }

            // Attribution is by directory, not session: we spawn the CLI and it chooses its own
            // session id, so where it was working is the only thing both sides know. Containment
            // rather than equality, because an agent may cd into a subdirectory of its worktree.
            if (
                Text(element, "cwd") is not { } cwd
                || !ClaudeTranscripts.Contains(directory, ClaudeTranscripts.Normalize(cwd))
            )
            {
                return false;
            }

            if (
                Text(element, "timestamp") is not { } stamp
                || !DateTimeOffset.TryParse(
                    stamp,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var at
                )
                || at < from
                || at > to
            )
            {
                return false;
            }

            if (
                !element.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("usage", out var usage)
                || usage.ValueKind != JsonValueKind.Object
            )
            {
                return false;
            }

            var input = Number(usage, "input_tokens");
            var output = Number(usage, "output_tokens");
            var cacheRead = Number(usage, "cache_read_input_tokens");
            var cacheWrite = Number(usage, "cache_creation_input_tokens");

            if (input + output + cacheRead + cacheWrite == 0)
            {
                return false;
            }

            parsed = new Parsed(
                Key(element, message),
                Text(message, "model"),
                input,
                output,
                cacheRead,
                cacheWrite
            );

            return true;
        }
    }

    /// <summary>
    /// The strongest stable identity the row offers. Message and request id together are the
    /// tightest; a fork rewrites the session id but keeps these, so keying on the session would
    /// double-count a forked conversation.
    /// </summary>
    static string? Key(JsonElement element, JsonElement message)
    {
        var messageId = Text(message, "id");
        var requestId = Text(element, "requestId");

        return (messageId, requestId) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{messageId}:{requestId}",
            ({ Length: > 0 }, _) => $"msg:{messageId}",
            _ => Text(element, "uuid") is { Length: > 0 } uuid ? $"uuid:{uuid}" : null,
        };
    }

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : 0;

    readonly record struct Parsed(
        string? Key,
        string? Model,
        long Input,
        long Output,
        long CacheRead,
        long CacheWrite
    );

    sealed class Row
    {
        public long Input { get; private set; }

        public long Output { get; private set; }

        public long CacheRead { get; private set; }

        public long CacheWrite { get; private set; }

        public void Add(Parsed parsed)
        {
            Input += parsed.Input;
            Output += parsed.Output;
            CacheRead += parsed.CacheRead;
            CacheWrite += parsed.CacheWrite;
        }

        /// <summary>
        /// Keeps the larger of each count. A repeated row is the same answer reported again, and a
        /// later report can be more complete than the first — so this is the merge, not addition.
        /// </summary>
        public void Max(Parsed parsed)
        {
            Input = Math.Max(Input, parsed.Input);
            Output = Math.Max(Output, parsed.Output);
            CacheRead = Math.Max(CacheRead, parsed.CacheRead);
            CacheWrite = Math.Max(CacheWrite, parsed.CacheWrite);
        }
    }
}
