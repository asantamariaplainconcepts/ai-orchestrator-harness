using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Persistence;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// #697. A run ended, so its PBI's journey comment is written again with its row and the path of its
/// recording on <c>aio/evidence</c>. Between two passes, like a person's decision, so it never edits
/// the comment while a pass is writing it. A repository with no folder here, or whose vendor has no
/// adapter, has no comment to write, nor does one without the harness (<see cref="HarnessInstall"/>); a comment the vendor did not keep is a warning, and the next
/// transition writes it whole again.
/// <para>
/// A run that ended waiting for a person (#706), or silent without its end record (#764), also asks
/// the schedule for its next pass now, so the hold with what to do reaches Needs you without waiting
/// out the interval.
/// </para>
/// </summary>
sealed partial class RunEndings(
    IEnumerable<IPbiProvider> providers,
    IRunRequests runs,
    IFolderDirectory folders,
    LoopDbContext database,
    Passes passes,
    TimeProvider clock,
    ILogger<RunEndings> logger
) : IRunEndings
{
    public async Task EndedAsync(PbiRef pbi, CancellationToken ct)
    {
        try
        {
            var folder = (await folders.ListAsync(ct)).FirstOrDefault(candidate =>
                string.Equals(candidate.Repository, pbi.Repository, StringComparison.OrdinalIgnoreCase)
            );
            if (folder is null || providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
            {
                return;
            }

            var failed = await passes.Between(
                () => new LoopEngine(providers, runs, folders, database, passes, clock, logger).WriteJourneyAsync(folder, provider, pbi, ct),
                ct
            );
            if (failed is not null)
            {
                Unwritten(logger, $"{pbi.Repository}#{pbi.Id}", failed);
            }

            if ((await runs.ListEndedAsync(pbi, ct)).LastOrDefault()?.End?.Failure is RunFailure.WaitingForPerson or RunFailure.NotLoggedIn or RunFailure.WaitingForInput)
            {
                passes.Soon();
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            Unwritten(logger, $"{pbi.Repository}#{pbi.Id}", failure.Message);
        }
    }

    [LoggerMessage(EventId = 5057, Level = LogLevel.Warning, Message = "The journey comment of {Pbi} was not written after a run ended: {Why}")]
    static partial void Unwritten(ILogger logger, string pbi, string why);
}
