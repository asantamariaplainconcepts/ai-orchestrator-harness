using AiOrchestrator.Infrastructure.AgentProviders;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The transcript reader, against transcripts written here.
/// <para>
/// These exist because the rule they cover is invisible in the data. Claude Code streams several
/// assistant rows carrying the same message and request ids, and the later ones can hold more
/// complete usage than the first — so the same key keeps the <b>maximum</b> of each count rather
/// than a sum. On this repository's own transcripts, summing instead reported 376M tokens where the
/// truth was 225M, and nothing about the output looked wrong. A test is the only thing that keeps
/// that rule from being "simplified" back into a sum.
/// </para>
/// </summary>
public sealed class ClaudeUsageReaderTests : IDisposable
{
    readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"harness-usage-{Guid.NewGuid():N}"
    );

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Repeated_rows_with_the_same_key_are_merged_by_maximum_not_summed()
    {
        var directory = Working();

        // The same answer reported three times, each more complete than the last — which is what a
        // streamed assistant turn actually looks like on disk.
        Transcript(
            directory,
            Row(directory, output: 10, cacheRead: 500, messageId: "msg_1", requestId: "req_1"),
            Row(directory, output: 40, cacheRead: 900, messageId: "msg_1", requestId: "req_1"),
            Row(directory, output: 25, cacheRead: 900, messageId: "msg_1", requestId: "req_1")
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        // Summing would give 75 and 2,300 — three times the truth.
        usage.OutputTokens.ShouldBe(40);
        usage.CacheReadTokens.ShouldBe(900);
    }

    [Fact]
    public async Task Rows_with_different_keys_are_summed()
    {
        var directory = Working();

        Transcript(
            directory,
            Row(directory, output: 10, messageId: "msg_1", requestId: "req_1"),
            Row(directory, output: 30, messageId: "msg_2", requestId: "req_2")
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(40);
    }

    /// <summary>
    /// A fork rewrites the session id but keeps the message and request ids. Keying on the session
    /// would count a forked conversation twice; keying on the pair does not.
    /// </summary>
    [Fact]
    public async Task A_forked_conversation_is_not_counted_twice()
    {
        var directory = Working();

        Transcript(
            directory,
            Row(directory, output: 60, messageId: "msg_1", requestId: "req_1", session: "one")
        );
        Named(
            directory,
            "fork",
            Row(directory, output: 60, messageId: "msg_1", requestId: "req_1", session: "two")
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(60);
    }

    /// <summary>
    /// Attribution is by working directory, because the CLI chooses its own session id and the
    /// directory is the only thing both sides know.
    /// </summary>
    [Fact]
    public async Task Rows_from_another_directory_are_ignored()
    {
        var mine = Working();
        var theirs = Working();

        Transcript(mine, Row(mine, output: 10, messageId: "a", requestId: "a"));
        Named(theirs, "other", Row(theirs, output: 999, messageId: "b", requestId: "b"));

        var usage = await Read(mine);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(10);
    }

    /// <summary>An agent may work in a subdirectory of the worktree it was started in.</summary>
    [Fact]
    public async Task Rows_from_a_subdirectory_of_the_run_are_counted()
    {
        var directory = Working();
        var nested = Path.Combine(directory, "packages", "api");

        Transcript(directory, Row(nested, output: 15, messageId: "a", requestId: "a"));

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(15);
    }

    [Fact]
    public async Task Rows_outside_the_window_are_ignored()
    {
        var directory = Working();

        Transcript(
            directory,
            Row(directory, output: 10, messageId: "a", requestId: "a", at: DateTimeOffset.UtcNow),
            Row(
                directory,
                output: 500,
                messageId: "b",
                requestId: "b",
                at: DateTimeOffset.UtcNow.AddDays(-3)
            )
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(10);
    }

    /// <summary>
    /// Nothing measured is null, not zero. A run whose vendor said nothing did not read no tokens.
    /// </summary>
    [Fact]
    public async Task Nothing_measured_reads_as_null()
    {
        var directory = Working();
        Transcript(directory, Row(directory, output: 0, messageId: "a", requestId: "a"));

        (await Read(directory)).ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_transcript_directory_reads_as_null()
    {
        var reader = new ClaudeUsageReader(Path.Combine(_root, "never-created"));

        var usage = await reader.Read(
            new UsageQuery("claude-code", Working(), Yesterday, Tomorrow)
        );

        usage.ShouldBeNull();
    }

    /// <summary>
    /// These files are appended to by another process, so reading a half-written last line is
    /// ordinary rather than exceptional. One bad line must not lose the rows around it.
    /// </summary>
    [Fact]
    public async Task A_half_written_line_is_skipped_rather_than_fatal()
    {
        var directory = Working();
        var path = Path.Combine(Project(directory), "session.jsonl");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(
            path,
            [
                Row(directory, output: 10, messageId: "a", requestId: "a"),
                "{\"type\":\"assistant\",\"message\":{\"usa",
                Row(directory, output: 20, messageId: "b", requestId: "b"),
            ]
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(30);
    }

    [Fact]
    public async Task The_cost_is_priced_from_the_reported_model()
    {
        var directory = Working();

        Transcript(
            directory,
            Row(directory, input: 1_000_000, messageId: "a", requestId: "a", model: "claude-opus-5")
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.Model.ShouldBe("claude-opus-5");
        usage.CostUsd.ShouldBe(5.00m);
    }

    /// <summary>
    /// An unknown model leaves the counts and drops the cost. The measurement is the durable half.
    /// </summary>
    [Fact]
    public async Task An_unknown_model_keeps_the_tokens_and_reports_no_cost()
    {
        var directory = Working();

        Transcript(
            directory,
            Row(directory, output: 42, messageId: "a", requestId: "a", model: "some-future-model")
        );

        var usage = await Read(directory);

        usage.ShouldNotBeNull();
        usage.OutputTokens.ShouldBe(42);
        usage.CostUsd.ShouldBeNull();
    }

    // ---- helpers -------------------------------------------------------------------------

    static DateTimeOffset Yesterday => DateTimeOffset.UtcNow.AddDays(-1);

    static DateTimeOffset Tomorrow => DateTimeOffset.UtcNow.AddDays(1);

    Task<MeasuredUsage?> Read(string directory) =>
        new ClaudeUsageReader(_root).Read(
            new UsageQuery("claude-code", directory, Yesterday, Tomorrow)
        );

    /// <summary>A working directory that exists, so path normalization has something to resolve.</summary>
    string Working()
    {
        var directory = Path.Combine(_root, "work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Claude Code names the project directory after the cwd. The name is not read.</summary>
    string Project(string directory) =>
        Path.Combine(_root, directory.Replace(Path.DirectorySeparatorChar, '-').TrimStart('-'));

    void Transcript(string directory, params string[] rows) => Named(directory, "session", rows);

    /// <summary>A second transcript file for the same directory — a fork, or another session.</summary>
    void Named(string directory, string name, params string[] rows)
    {
        var project = Project(directory);
        Directory.CreateDirectory(project);
        File.WriteAllLines(Path.Combine(project, $"{name}.jsonl"), rows);
    }

    static string Row(
        string cwd,
        long input = 0,
        long output = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        string messageId = "msg",
        string requestId = "req",
        string session = "session",
        string model = "claude-opus-5",
        DateTimeOffset? at = null
    ) =>
        System.Text.Json.JsonSerializer.Serialize(
            new
            {
                type = "assistant",
                sessionId = session,
                requestId,
                cwd,
                timestamp = (at ?? DateTimeOffset.UtcNow).ToString("O"),
                message = new
                {
                    id = messageId,
                    model,
                    usage = new
                    {
                        input_tokens = input,
                        output_tokens = output,
                        cache_read_input_tokens = cacheRead,
                        cache_creation_input_tokens = cacheWrite,
                    },
                },
            }
        );
}
