using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// What <see cref="ReleaseHold"/> and <see cref="MovePbi"/> share (#511): find the PBI's folder,
/// its loop and the PBI as the vendor has it now, let <see cref="Planner"/> decide as it does for
/// the engine, and apply the decision through <see cref="LoopEngine"/> between two passes.
/// </summary>
sealed class PersonDecisions(
    IEnumerable<IPbiProvider> providers,
    IRunRequests runs,
    IFolderDirectory folders,
    LoopDbContext database,
    Passes passes,
    TimeProvider clock,
    ILogger<PersonDecisions> logger
)
{
    public async Task<ErrorOr<ReadJournal.Line>> ApplyAsync(
        string repository,
        string id,
        Func<LoopDefinition, Pbi, LastTransition?, Decision> decide,
        CancellationToken ct
    )
    {
        var folder = (await folders.ListAsync(ct)).FirstOrDefault(candidate =>
            string.Equals(candidate.Repository, repository, StringComparison.OrdinalIgnoreCase)
        );
        if (folder is null)
        {
            return Error.NotFound("Pbi.NoFolder", $"No folder is a checkout of {repository}. Add one from the rail.");
        }

        var (loop, problem) = await LoopReader.ReadAsync(folder, ct);
        if (loop is null)
        {
            return Error.Conflict("Loop.Unreadable", $"{folder.Path}: {problem}");
        }

        if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
        {
            return Error.NotFound("Pbi.NoAdapter", $"No adapter reads {folder.Vendor}.");
        }

        try
        {
            return await passes.Between<ErrorOr<ReadJournal.Line>>(
                async () =>
                {
                    if ((await provider.ReadThreadAsync(new PbiRef(folder.Repository, id), ct))?.AsPbi is not { } pbi)
                    {
                        return Error.NotFound("Pbi.NotFound", $"{folder.Repository} has no PBI {id}.");
                    }

                    var last = await database
                        .Journal.AsNoTracking()
                        .Where(entry =>
                            entry.Repository == pbi.Ref.Repository
                            && entry.Pbi == pbi.Ref.Id
                            && (entry.Kind == JournalKind.Moved || entry.Kind == JournalKind.Held || entry.Kind == JournalKind.Released)
                        )
                        .OrderByDescending(entry => entry.Id)
                        .FirstOrDefaultAsync(ct);

                    var decision = decide(loop, pbi, last is null ? null : new LastTransition(last.Kind, last.From, last.To, last.Why));
                    if (decision is Ignore refused)
                    {
                        return Error.Conflict("Pbi.NothingToDo", $"Nothing was changed: {refused.Why}.");
                    }

                    var engine = new LoopEngine(providers, runs, folders, database, passes, clock, logger);
                    var written = await engine.ApplyForPersonAsync(decision, folder, provider, pbi, ct);
                    if (written is null)
                    {
                        return Error.Unexpected("Loop.NothingWritten", "The engine applied the decision and wrote no journal line.");
                    }

                    // A release or a move the vendor did not keep is written as what happened instead
                    // (a wait, or a hold), and the person reads it as a refusal with its reason.
                    return written.Kind is JournalKind.Released or JournalKind.Moved
                        ? ReadJournal.Line.Of(written)
                        : Error.Conflict("Pbi.VendorRefused", written.Why);
                },
                ct
            );
        }
        catch (PbiProviderException failure)
        {
            return Error.Conflict("Pbi.VendorUnread", failure.Message);
        }
    }
}
