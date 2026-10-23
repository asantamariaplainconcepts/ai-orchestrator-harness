namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// Hands a folder or a file to the editor on this machine: <i>Open in editor</i> in Settings and in
/// a PBI's Files tab (#514, #515).
/// <para>
/// It returns a <see cref="DesktopResult"/> rather than throwing, and the reasons are named. A path
/// can go stale — a worktree removed underneath the screen, an editor uninstalled since Settings
/// were written — and "not found" and "launch failed" need different sentences.
/// </para>
/// </summary>
public interface IDesktopIntegration
{
    /// <summary>
    /// Opens the path in an editor: the one named, or the configured one when nothing is.
    /// <para>
    /// Two rules, both enforced by the implementation. Cursor needs <c>--new-window</c>, because a
    /// bare folder launch is routed through its last active workbench and silently opens a different
    /// worktree. And a configured command may be either an absolute executable path or a compound
    /// shell line; only the latter goes through <c>/bin/sh -c</c>, and existence on disk is the test.
    /// </para>
    /// <para>
    /// <b>What may be passed as <paramref name="command"/> is narrower than what may be
    /// configured</b>: the shell branch above is reachable from a compound configured line, so the
    /// Workspace module's <c>EditorName</c> restricts anything a person chose to a bare command name.
    /// </para>
    /// </summary>
    Task<DesktopResult> OpenInEditor(
        string path,
        string? command = null,
        CancellationToken cancellationToken = default
    );
}

/// <param name="Reason">Null on success.</param>
public sealed record DesktopResult(DesktopFailure? Reason)
{
    public bool Succeeded => Reason is null;

    public static DesktopResult Ok { get; } = new((DesktopFailure?)null);

    public static DesktopResult Failed(DesktopFailure reason) => new(reason);
}

public enum DesktopFailure
{
    /// <summary>The path was relative. A bug in the caller, not something the user did.</summary>
    NotAbsolute = 1,

    /// <summary>It is gone. Usually a worktree removed while the screen still listed it.</summary>
    NotFound = 2,

    /// <summary>The target exists but the launch failed — no editor installed, or it refused.</summary>
    LaunchFailed = 3,

    /// <summary>This host cannot reach the user's desktop at all, e.g. it runs in a container.</summary>
    Unsupported = 4,
}
