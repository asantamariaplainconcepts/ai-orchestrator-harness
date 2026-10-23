namespace AiOrchestrator.Infrastructure;

/// <summary>
/// The login shell's <c>PATH</c> for the host. The installed Server adopts it at start-up (#517),
/// so that a process launched from Finder finds <c>gh</c> and the agent CLIs (#476).
/// </summary>
public static class LoginShell
{
    /// <summary>The recovered <c>PATH</c>, merged ahead of the inherited one, or null when the shell did not answer.</summary>
    public static string? Path() => LoginShellEnvironment.Path();
}
