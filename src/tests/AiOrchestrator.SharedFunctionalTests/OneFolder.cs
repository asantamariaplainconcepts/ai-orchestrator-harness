using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.SharedFunctionalTests;

/// <summary>One GitHub checkout of <paramref name="repository"/> at <c>/checkouts/repo</c>, the folder a vendor fake answers for.</summary>
public sealed class OneFolder(string repository = "owner/repo") : IFolderDirectory
{
    public Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoopFolder>>([new LoopFolder("/checkouts/repo", "github", repository)]);
}
