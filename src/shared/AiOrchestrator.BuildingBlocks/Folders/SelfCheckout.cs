namespace AiOrchestrator.BuildingBlocks.Folders;

/// <summary>
/// The git checkout this process is running from, when it is running from one.
/// <para>
/// <b>This exists so a first boot can be a demonstration without inventing anything.</b> The seeds
/// used to insert three checkouts under <c>~/code</c> that were never on any machine, and every
/// action offered on them failed. The repository the server was started from is the one checkout
/// that is certainly there, certainly a git repository, and certainly the kind of thing this
/// product operates on — so it is what gets seeded.
/// </para>
/// <para>
/// <b>Answering null is a normal outcome, not a failure.</b> A published build lives beside its
/// binaries in an install directory with no repository above it, and there the honest answer is
/// "nothing to seed" — the same rule <c>WorkspaceSeed</c> already applied to a path that is not on
/// disk.
/// </para>
/// </summary>
public static class SelfCheckout
{
    /// <summary>
    /// Walks up from the running assembly's directory to the first ancestor holding a
    /// <c>.git</c>, and returns that ancestor's absolute path — or null when there is none.
    /// <para>
    /// <b>A directory or a file.</b> In a linked worktree <c>.git</c> is a file pointing at the
    /// main repository's admin directory, and a worktree is exactly as much a checkout as the
    /// repository it came from — testing only for a directory would seed nothing for anybody
    /// working in one, which is most of this product's own users.
    /// </para>
    /// <para>
    /// Started from <see cref="AppContext.BaseDirectory"/> rather than the current directory or a
    /// host's content root: the working directory belongs to whoever launched the process, and the
    /// question being asked is "where does this code live", which only the assembly's own location
    /// answers the same way in every habitat.
    /// </para>
    /// </summary>
    public static string? Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
