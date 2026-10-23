using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiOrchestrator.BuildingBlocks.Ports;
using Porta.Pty;

namespace AiOrchestrator.Infrastructure.Sessions;

/// <summary>
/// <see cref="ISession"/> over a real pseudo-terminal: <c>forkpty</c> on Unix and ConPTY on Windows,
/// both through Porta.Pty, the library the first tree's <c>PtyAgentRunner</c> and
/// <c>ProcessCommandRunner</c> already spawn with. Their rules come across with them: one PATH
/// recovered from the login shell, <c>TERM=xterm-256color</c>, implausible sizes refused, and the
/// process group killed by disposing the connection.
/// <para>
/// <b>The PTY is read by this class, not by the caller.</b> A pump starts with the process and
/// copies every chunk to the recording and to a channel <see cref="ReadAsync"/> drains. The
/// recording therefore holds the whole run whether or not anybody is watching, and a slow reader
/// does not stall the process. The channel has one reader: a second enumeration would split the
/// chunks between the two.
/// </para>
/// </summary>
sealed class PtySession(TimeProvider clock) : ISession, IAsyncDisposable
{
    /// <summary>What the process is told its terminal is, and what the recording's header says.</summary>
    public const string Terminal = "xterm-256color";

    /// <summary>
    /// Below this a reported size is a failed measurement, not a small terminal: a pane mid-layout
    /// measures one column, and a TUI told so hard-wraps everything it draws at that width into the
    /// scrollback. Same bounds as <c>PtyAgentRunner</c>.
    /// </summary>
    const int MinimumColumns = 40;
    const int MinimumRows = 10;
    const int MaximumColumns = 500;
    const int MaximumRows = 200;

    readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true }
    );

    IPtyConnection? _connection;
    AsciicastRecording? _recording;
    Task _pump = Task.CompletedTask;
    int _killed;

    public async Task StartAsync(SessionOptions options, CancellationToken ct)
    {
        if (_connection is not null)
        {
            throw new InvalidOperationException("This session has already started. Resolve a new one per run.");
        }

        var environment = Environment(options.Environment);
        var columns = Math.Clamp(options.Columns, MinimumColumns, MaximumColumns);
        var rows = Math.Clamp(options.Rows, MinimumRows, MaximumRows);

        var pty = new PtyOptions
        {
            Name = Terminal,
            Cwd = options.WorkingDirectory,
            App = Resolve(options.FileName, environment["PATH"]),
            CommandLine = [.. options.Arguments],
            Cols = columns,
            Rows = rows,
            Environment = environment,
        };

        // The recording exists before the process does, so its first byte is in it.
        _recording = options.RecordingPath is { } path
            ? AsciicastRecording.Create(path, columns, rows, clock)
            : null;

        try
        {
            _connection = await PtyProvider.SpawnAsync(pty, ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _recording?.Dispose();
            throw new InvalidOperationException(
                $"Could not start {options.FileName} in {options.WorkingDirectory}: {failure.Message}",
                failure
            );
        }

        _pump = Task.Run(() => Pump(_connection), CancellationToken.None);
    }

    /// <summary>
    /// A write that lands as the process exits is dropped, as in the first tree's runners: the person pressed
    /// a key as the agent finished, an ordinary race and nothing a caller can act on.
    /// </summary>
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var connection = Started();

        try
        {
            await connection.WriterStream.WriteAsync(data, ct);
            await connection.WriterStream.FlushAsync(ct);
        }
        catch (Exception failure) when (failure is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        Started();

        await foreach (var chunk in _output.Reader.ReadAllAsync(ct))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// An implausible size is ignored rather than rounded up, and so is not recorded either: the
    /// terminal keeps the size it has, and the recording says what the process really saw.
    /// </summary>
    public Task ResizeAsync(int columns, int rows, CancellationToken ct)
    {
        var connection = Started();

        if (columns < MinimumColumns || rows < MinimumRows)
        {
            return Task.CompletedTask;
        }

        columns = Math.Min(columns, MaximumColumns);
        rows = Math.Min(rows, MaximumRows);

        try
        {
            connection.Resize(columns, rows);
            _recording?.Resize(columns, rows);
        }
        catch (Exception failure) when (failure is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The session exited a moment ago. Nothing to resize and nothing to record.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Kills the process group and waits for the pump to close the recording, so the file is
    /// complete when this returns.
    /// </summary>
    public async Task KillAsync(CancellationToken ct)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        if (Interlocked.Exchange(ref _killed, 1) == 0)
        {
            Dispose(connection);
        }

        await _pump.WaitAsync(ct);
    }

    /// <summary>The server is going down: the process goes with it rather than being left to init.</summary>
    public async ValueTask DisposeAsync() => await KillAsync(CancellationToken.None);

    async Task Pump(IPtyConnection connection)
    {
        var buffer = new byte[16 * 1024];

        try
        {
            while (true)
            {
                int read;

                try
                {
                    read = await connection.ReaderStream.ReadAsync(buffer);
                }
                catch (Exception)
                {
                    // The far end of a PTY closing reads as an IO error on most platforms, and a
                    // killed session as a disposed stream. Both mean the process is gone.
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                var chunk = buffer.AsSpan(0, read).ToArray();
                _recording?.Output(chunk);
                _output.Writer.TryWrite(chunk);
            }
        }
        finally
        {
            _recording?.Dispose();
            _output.Writer.TryComplete();

            if (Interlocked.Exchange(ref _killed, 1) == 0)
            {
                Dispose(connection);
            }
        }
    }

    IPtyConnection Started() =>
        _connection ?? throw new InvalidOperationException("The session has not started.");

    static void Dispose(IPtyConnection connection)
    {
        try
        {
            // Porta.Pty kills the child's process group on POSIX, which stops a build the agent
            // itself started from outliving it.
            connection.Dispose();
        }
        catch (Exception failure) when (failure is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Already gone, which is what was asked for.
        }
    }

    /// <summary>
    /// What the process gets on top of this one's environment — Porta.Pty merges, it does not
    /// replace: the login shell's PATH, the terminal type, then the caller's own variables, which
    /// win.
    /// </summary>
    static Dictionary<string, string> Environment(IReadOnlyDictionary<string, string> added)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] =
                LoginShellEnvironment.Path() ?? System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            ["TERM"] = Terminal,
            ["COLORTERM"] = "truecolor",
        };

        foreach (var (key, value) in added)
        {
            environment[key] = value;
        }

        return environment;
    }

    /// <summary>
    /// The executable walked over the PATH the process is given, so a session resolves what its own
    /// process would. A name found nowhere is passed as given and the spawn reports it.
    /// </summary>
    internal static string Resolve(string fileName, string path)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains('/', StringComparison.Ordinal))
        {
            return fileName;
        }

        string[] extensions = OperatingSystem.IsWindows() ? ["", ".exe", ".cmd", ".bat"] : [""];

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, fileName + extension);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return fileName;
    }
}
