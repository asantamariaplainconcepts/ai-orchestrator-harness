using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders;

/// <summary>
/// The Workspace side of <see cref="IFolderDirectory"/> (#505): the folders the loop engine walks,
/// read from this module's rows so the loop never reads this schema.
/// <para>
/// A folder counts when its <c>origin</c> is on github.com, the one vendor read today. Two
/// folders of one repository are one entry, the first by path, so the loop does not work a PBI twice.
/// </para>
/// <para>
/// Each carries its default agent and model, which a state of the loop that names no agent runs
/// with (#546).
/// </para>
/// </summary>
sealed class FolderDirectory(WorkspaceDbContext database) : IFolderDirectory
{
    const string GitHub = "github.com/";

    public async Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct)
    {
        var folders = await database
            .Folders.AsNoTracking()
            .Where(folder => folder.IsRepository && folder.RemoteUrl != null)
            .OrderBy(folder => folder.Path)
            .Select(folder => new { folder.Path, folder.RemoteUrl, folder.DefaultAgentId, folder.DefaultModel })
            .ToListAsync(ct);

        return
        [
            .. folders
                .Select(folder => (Folder: folder, Repository: RepositoryIdentity.Of(folder.RemoteUrl)))
                .Where(entry => entry.Repository?.StartsWith(GitHub, StringComparison.Ordinal) == true)
                .Select(entry => new LoopFolder(
                    LocalPath.Expand(entry.Folder.Path),
                    "github",
                    entry.Repository![GitHub.Length..],
                    string.IsNullOrWhiteSpace(entry.Folder.DefaultAgentId) ? null : entry.Folder.DefaultAgentId,
                    entry.Folder.DefaultModel
                ))
                .DistinctBy(folder => folder.Repository, StringComparer.OrdinalIgnoreCase),
        ];
    }
}
