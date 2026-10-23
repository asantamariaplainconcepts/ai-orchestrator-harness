namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// Where this checkout is, for the tests that are about this checkout's own files rather than about
/// a fixture. A test against a copy passes while the file it is about says something else.
/// </summary>
static class ThisRepository
{
    /// <summary>
    /// Walks up from the test binary to the checkout root, which is where <c>.aio</c> lives.
    /// The suite runs from <c>bin/Debug/net10.0</c>, and from a worktree as readily as from the main
    /// checkout — so the marker is looked for rather than a fixed number of parents counted.
    /// </summary>
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".aio")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No .aio directory above the test binary. These tests read this repository's own file."
        );
    }
}
