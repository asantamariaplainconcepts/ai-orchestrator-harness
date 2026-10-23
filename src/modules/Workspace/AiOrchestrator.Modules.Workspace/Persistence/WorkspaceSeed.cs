using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Domain;

namespace AiOrchestrator.Modules.Workspace.Persistence;

/// <summary>
/// One folder on a first boot: <b>this checkout</b>, the repository the server is running from.
/// <para>
/// It used to be four invented paths under <c>~/code</c>, paired with an in-memory probe that
/// agreed they existed. When the probe became real the paths did not, so this file grew a rule —
/// insert nothing that is not on disk — and on every machine it then seeded <b>nothing</b>. An
/// empty Settings screen is honest, but it is also a product that demonstrates none of itself.
/// </para>
/// <para>
/// <b>The checkout this process is running from is the one folder that always qualifies.</b> It is
/// on disk, it is a git repository, and it is exactly the kind of thing the product operates on —
/// so every button offered on it works, which is the property no invented row can have. See
/// <see cref="SelfCheckout"/>. A build running outside a repository seeds nothing, and that remains
/// the correct answer rather than a gap.
/// </para>
/// <para>
/// <b>The branch and the remote are git's answer, never a guess.</b> They are asked of
/// <see cref="ICheckoutProbe"/>, the same question <c>AddFolder</c> asks, when the process that
/// migrates has one composed; without one the row arrives blank, and a blank remote is a folder the
/// loop does not walk until it is added again. A guessed branch is the same fiction in a smaller font.
/// </para>
/// <para>
/// <b>Only on the database's first migration</b>, which <see cref="WorkspaceModule.Migrate"/>
/// decides: a person who removes this folder does not get it back on the next start. Until #548 it
/// ran on every start, keyed on the path, so a removed checkout came back each time the console did.
/// </para>
/// </summary>
static class WorkspaceSeed
{
    /// <summary>Which agent a run in this folder starts with: the one the loop of this repository names.</summary>
    const string DefaultAgent = "claude-code";

    public static async Task Apply(WorkspaceDbContext database, ICheckoutProbe? probe, CancellationToken cancellationToken)
    {
        var root = SelfCheckout.Root();

        if (root is null)
        {
            return;
        }

        // Belt and braces with SelfCheckout, which found a `.git` a moment ago. The rule it
        // enforces is the one this file exists to state: a row for a folder that is not on disk
        // would be worse than no row — the sidebar would name a branch the filesystem denies, and
        // every action on it would fail.
        if (!LocalPath.DirectoryExists(root))
        {
            return;
        }

        var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));

        var folder = Folder.Create(name, root, DefaultAgent);

        if (probe is not null && await probe.ReadAsync(root, cancellationToken) is { } state)
        {
            folder.RecordProbe(state.Branch, state.RemoteUrl);
        }

        database.Folders.Add(folder);

        await database.SaveChangesAsync(cancellationToken);
    }
}
