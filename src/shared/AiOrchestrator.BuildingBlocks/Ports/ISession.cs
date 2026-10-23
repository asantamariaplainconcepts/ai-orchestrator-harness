namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// The terminal of one run: a pseudo-terminal the control plane writes to, reads from, resizes,
/// kills and records. One instance is one session, so a caller resolves a fresh one per run.
/// <para>
/// Every environment hands over the same thing, a PTY, so this port does not know where the agent
/// runs — a remote environment is a local PTY around <c>sbx exec</c>, <c>ssh -tt</c> or
/// <c>kubectl exec -it</c>. Adapters: Unix (<c>forkpty</c>) and Windows (ConPTY).
/// </para>
/// </summary>
public interface ISession
{
    /// <summary>Spawns the process in a new PTY and, when asked, starts recording it.</summary>
    Task StartAsync(SessionOptions options, CancellationToken ct);

    /// <summary>Writes bytes to the PTY exactly as given: keystrokes, not lines.</summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>The PTY's output as it arrives. The sequence ends when the process exits.</summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);

    /// <summary>Tells the PTY, and so the process, its new size.</summary>
    Task ResizeAsync(int columns, int rows, CancellationToken ct);

    /// <summary>Ends the process. Idempotent: killing a session that already exited does nothing.</summary>
    Task KillAsync(CancellationToken ct);
}

/// <summary>What to spawn in a session, and where its recording goes.</summary>
/// <param name="FileName">The executable, resolved on the PATH the session gives the process.</param>
/// <param name="Arguments">Its arguments, one per element and unquoted.</param>
/// <param name="WorkingDirectory">Where the process starts, as <see cref="IEnvironment"/> prepared it.</param>
/// <param name="Environment">Variables added to the process's environment.</param>
/// <param name="Columns">The initial width of the terminal.</param>
/// <param name="Rows">The initial height of the terminal.</param>
/// <param name="RecordingPath">
/// Where the asciicast v2 recording is written, or null for a session nobody replays.
/// </param>
public sealed record SessionOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    int Columns,
    int Rows,
    string? RecordingPath
);
