namespace AiOrchestrator.BuildingBlocks.Folders;

/// <summary>
/// The folders the loop may work in, each with the repository its PBIs come from. <c>Modules.Workspace</c>
/// owns the folders and implements it; <c>Modules.Loop</c> reads it, and neither references the other.
/// </summary>
public interface IFolderDirectory
{
    /// <summary>Every folder whose checkout names a repository at a vendor the loop can read.</summary>
    Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct);
}

/// <summary>A folder as the loop needs it.</summary>
/// <param name="Path">The checkout, absolute. Its <c>.aio/loop.yaml</c> is the loop, and its scripts run here.</param>
/// <param name="Vendor">The <c>IPbiProvider.Name</c> that reads its PBIs, such as <c>github</c>.</param>
/// <param name="Repository">The repository at that vendor, such as <c>owner/name</c>.</param>
/// <param name="DefaultAgent">The provider the folder's runs start with when a state of its loop names
/// no agent, such as <c>claude-code</c>, or null when it has none (#546).</param>
/// <param name="DefaultModel">The model of <paramref name="DefaultAgent"/>, or null for the CLI's own default.</param>
public sealed record LoopFolder(string Path, string Vendor, string Repository, string? DefaultAgent = null, string? DefaultModel = null);
