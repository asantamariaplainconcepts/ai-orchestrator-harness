using System.Threading.Channels;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// A live run's terminal output, handed to everyone watching it (#508). The run's background task is
/// still the only reader of the session; it publishes each chunk here, and each viewer attached from
/// the browser gets its own copy.
/// <para>
/// A viewer that attaches late starts from the tail of what was printed, up to
/// <see cref="ReplayBytes"/>, so a terminal opened on a running agent draws its screen instead of
/// waiting for the next repaint. The shape is the first tree's <c>TerminalHub.Attach</c>: subscribe
/// and take the replay in one step, so no chunk falls between them.
/// </para>
/// <para>
/// <b>Nobody waits for a viewer.</b> Publishing never blocks the run: a viewer that falls
/// <see cref="Backlog"/> chunks behind is cut off and attaches again, rather than stalling the agent
/// or growing without bound. Detaching takes the viewer out and leaves the run as it was.
/// </para>
/// </summary>
sealed class SessionOutput
{
    /// <summary>How much of the tail a late viewer is given: a few screens of a busy TUI.</summary>
    public const int ReplayBytes = 256 * 1024;

    /// <summary>How many chunks a viewer may have unread before it is cut off.</summary>
    public const int Backlog = 4096;

    readonly Lock _gate = new();
    readonly Queue<ReadOnlyMemory<byte>> _tail = new();
    readonly List<Channel<ReadOnlyMemory<byte>>> _viewers = [];
    int _tailBytes;
    bool _ended;

    /// <summary>Hands one chunk to every viewer and keeps it for the next one to attach.</summary>
    public void Publish(ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            _tail.Enqueue(chunk);
            _tailBytes += chunk.Length;
            while (_tailBytes > ReplayBytes && _tail.Count > 1)
            {
                _tailBytes -= _tail.Dequeue().Length;
            }

            for (var index = _viewers.Count - 1; index >= 0; index--)
            {
                if (!_viewers[index].Writer.TryWrite(chunk))
                {
                    _viewers[index].Writer.TryComplete(new SessionViewerBehindException());
                    _viewers.RemoveAt(index);
                }
            }
        }
    }

    /// <summary>The session ended: every viewer's stream ends, and a viewer attaching now gets the tail and the end.</summary>
    public void End()
    {
        lock (_gate)
        {
            _ended = true;
            foreach (var viewer in _viewers)
            {
                viewer.Writer.TryComplete();
            }
            _viewers.Clear();
        }
    }

    /// <summary>A new viewer: the tail printed so far, and the chunks from here on.</summary>
    public SessionViewer Attach()
    {
        var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(Backlog) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait }
        );

        lock (_gate)
        {
            var replay = new byte[_tailBytes];
            var at = 0;
            foreach (var chunk in _tail)
            {
                chunk.Span.CopyTo(replay.AsSpan(at));
                at += chunk.Length;
            }

            if (_ended)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _viewers.Add(channel);
            }

            return new SessionViewer(replay, channel.Reader, () => Detach(channel));
        }
    }

    void Detach(Channel<ReadOnlyMemory<byte>> channel)
    {
        lock (_gate)
        {
            _viewers.Remove(channel);
        }
        channel.Writer.TryComplete();
    }
}

/// <summary>One viewer of a live run's terminal. Disposing it detaches the viewer and nothing else.</summary>
/// <param name="Replay">The tail printed before the viewer attached.</param>
/// <param name="Output">What the session prints from then on. Completes when the session ends, or
/// with <see cref="SessionViewerBehindException"/> when the viewer fell too far behind.</param>
sealed record SessionViewer(byte[] Replay, ChannelReader<ReadOnlyMemory<byte>> Output, Action Detach) : IDisposable
{
    public void Dispose() => Detach();
}

/// <summary>The viewer did not keep up and was cut off; attaching again starts it from the tail.</summary>
sealed class SessionViewerBehindException : Exception
{
    public SessionViewerBehindException()
        : base($"The viewer fell {SessionOutput.Backlog} chunks behind the session and was detached. Attach again.") { }

    public SessionViewerBehindException(string message)
        : base(message) { }

    public SessionViewerBehindException(string message, Exception innerException)
        : base(message, innerException) { }
}
