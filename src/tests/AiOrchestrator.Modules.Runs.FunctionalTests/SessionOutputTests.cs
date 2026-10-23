using System.Text;
using System.Threading.Channels;
using AiOrchestrator.Modules.Runs.Features.PbiRuns;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>#508. The fan-out of a live run's output: every viewer gets every chunk, and none of them can hold the run up.</summary>
public sealed class SessionOutputTests
{
    [Fact]
    public async Task Every_viewer_gets_every_chunk_and_a_late_one_starts_from_the_tail()
    {
        var output = new SessionOutput();
        using var first = output.Attach();
        output.Publish("one "u8.ToArray());

        using var second = output.Attach();
        Encoding.UTF8.GetString(second.Replay).ShouldBe("one ");

        output.Publish("two"u8.ToArray());
        output.End();

        Text(await Drain(first.Output)).ShouldBe("one two");
        Text(await Drain(second.Output)).ShouldBe("two");

        using var after = output.Attach();
        Encoding.UTF8.GetString(after.Replay).ShouldBe("one two");
        (await Drain(after.Output)).ShouldBeEmpty("the session already ended");
    }

    [Fact]
    public void The_tail_keeps_the_newest_bytes_up_to_its_bound()
    {
        var output = new SessionOutput();
        for (var chunk = 0; chunk < 10; chunk++)
        {
            output.Publish(new byte[SessionOutput.ReplayBytes / 4]);
        }

        using var viewer = output.Attach();
        viewer.Replay.Length.ShouldBe(SessionOutput.ReplayBytes);
    }

    [Fact]
    public async Task A_viewer_that_falls_behind_is_cut_off_and_the_others_are_not()
    {
        var output = new SessionOutput();
        using var slow = output.Attach();
        using var keeping = output.Attach();

        for (var chunk = 0; chunk < SessionOutput.Backlog; chunk++)
        {
            output.Publish(new byte[1]);
        }

        // One viewer reads a chunk; the other has read nothing when the next one comes.
        keeping.Output.TryRead(out _).ShouldBeTrue();
        output.Publish(new byte[1]);
        output.End();

        (await Drain(keeping.Output)).Count.ShouldBe(SessionOutput.Backlog);
        await Should.ThrowAsync<SessionViewerBehindException>(async () => await Drain(slow.Output));
    }

    [Fact]
    public async Task Detaching_ends_that_viewer_and_nothing_else()
    {
        var output = new SessionOutput();
        var gone = output.Attach();
        using var staying = output.Attach();

        gone.Dispose();
        output.Publish("still here"u8.ToArray());
        output.End();

        (await Drain(gone.Output)).ShouldBeEmpty();
        Text(await Drain(staying.Output)).ShouldBe("still here");
    }

    static async Task<List<ReadOnlyMemory<byte>>> Drain(ChannelReader<ReadOnlyMemory<byte>> reader)
    {
        var chunks = new List<ReadOnlyMemory<byte>>();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            chunks.Add(chunk);
        }
        return chunks;
    }

    static string Text(IEnumerable<ReadOnlyMemory<byte>> chunks) => string.Concat(chunks.Select(chunk => Encoding.UTF8.GetString(chunk.Span)));
}
