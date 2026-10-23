using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Features.Folders;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Files;

/// <summary>
/// What the three slices of the Files tab (#514) and its Open in editor (<c>OpenInEditor</c> with <c>pbi</c>) share: the folder's checkout and the PBI's branch.
/// The loop names a PBI's branch <c>change/&lt;id&gt;</c> (<c>LoopEngine.Branch</c>), and the tab
/// reads the same one. <c>asIs</c> is the checkout as it is, which the tab offers when the branch
/// is gone (DESIGN.md#files).
/// </summary>
static class PbiCheckout
{
    public static string? Branch(string pbi, bool asIs) => asIs ? null : $"change/{pbi}";

    public static async Task<ErrorOr<string>> FolderPath(
        WorkspaceDbContext database,
        Guid folderId,
        CancellationToken cancellationToken
    )
    {
        var path = await database
            .Folders.AsNoTracking()
            .Where(folder => folder.Id == folderId)
            .Select(folder => folder.Path)
            .FirstOrDefaultAsync(cancellationToken);

        return path is null ? FolderErrors.NotFound(folderId) : LocalPath.Expand(path);
    }

    public static PbiFiles View(BranchTree tree, string folderPath) =>
        new(
            tree.Branch,
            tree.Exists,
            tree.Checkout,
            tree.Checkout is not null && !LocalPath.Same(tree.Checkout, folderPath),
            tree.Files,
            tree.Modified,
            tree.Truncated
        );

    public static Error Refused(string reason) => Error.Conflict("Files.Refused", reason);
}

/// <summary>The tree of a PBI's branch as the Files tab draws it.</summary>
/// <param name="Branch">The branch read; for the checkout as it is, its own branch, or null on a detached head.</param>
/// <param name="Exists">False when the repository has no such branch, locally or at <c>origin</c>.</param>
/// <param name="Checkout">Where the branch is on disk, or null when nothing holds it and nothing can be saved.</param>
/// <param name="InWorktree">The checkout is a worktree of the folder rather than the folder itself.</param>
/// <param name="Files">Every file, sorted by path.</param>
/// <param name="Modified">The files that differ on disk from the branch's last commit.</param>
/// <param name="Truncated">The tree stops at a bound short of the whole repository.</param>
sealed record PbiFiles(
    string? Branch,
    bool Exists,
    string? Checkout,
    bool InWorktree,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Modified,
    bool Truncated
);
