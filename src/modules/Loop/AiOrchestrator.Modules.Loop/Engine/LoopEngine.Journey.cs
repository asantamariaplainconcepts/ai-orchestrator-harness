using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Engine;

sealed partial class LoopEngine
{
    /// <summary>
    /// #696. After the lines of one step are saved, writes the journey comment of every PBI one of
    /// them moved or stopped: found among the issue's comments by its marker and edited, or added
    /// when there is none, so an issue keeps one however many transitions it goes through. It is
    /// built from this instance's journal and runs, which is the database that decided.
    /// <para>
    /// It never stops the transition, which is saved before it. A comment the vendor did not write or
    /// keep is a <c>waited</c> line from the state the PBI left, saying so, and the next transition
    /// writes the whole comment again.
    /// </para>
    /// </summary>
    async Task WriteJourneys(LoopFolder folder, IPbiProvider provider, IEnumerable<JournalEntry> written, CancellationToken ct)
    {
        var moved = written
            .Where(JourneyComment.Transition)
            .GroupBy(line => new PbiRef(line.Repository, line.Pbi))
            .Select(group => (Pbi: group.Key, From: group.Last().From))
            .ToList();

        foreach (var (pbi, from) in moved)
        {
            if (await WriteJourney(folder, provider, pbi, ct) is { } failed)
            {
                LogJourneyUnwritten(logger, $"{pbi.Repository}#{pbi.Id}", failed);
                _journal.Write(pbi, from, JournalKind.Waited, null, $"the journey comment was not written: {failed}");
                await _journal.SaveAsync(ct);
            }
        }
    }

    /// <summary>
    /// #697. The PBI's journey comment written again because one of its runs ended, a person's
    /// included, which no transition follows. Null when it was written, or why not.
    /// </summary>
    public Task<string?> WriteJourneyAsync(LoopFolder folder, IPbiProvider provider, PbiRef pbi, CancellationToken ct) => WriteJourney(folder, provider, pbi, ct);

    /// <summary>
    /// The comment written or edited, or why it was not. Nothing is written for a folder without
    /// <c>.aio/version</c> (#697), read now: a client's repository opened as a folder gets no
    /// comment from this app, and a debug line says so.
    /// </summary>
    async Task<string?> WriteJourney(LoopFolder folder, IPbiProvider provider, PbiRef pbi, CancellationToken ct)
    {
        if (!HarnessInstall.In(folder.Path))
        {
            LogNoHarness(logger, $"{pbi.Repository}#{pbi.Id}", folder.Path);
            return null;
        }

        try
        {
            var lines = await database
                .Journal.AsNoTracking()
                .Where(entry => entry.Repository == pbi.Repository && entry.Pbi == pbi.Id)
                .OrderBy(entry => entry.Id)
                .ToListAsync(ct);
            var asked = await database
                .AskedRuns.AsNoTracking()
                .Where(run => run.Repository == pbi.Repository && run.Pbi == pbi.Id)
                .OrderBy(run => run.AskedAt)
                .Select(run => run.RunId)
                .ToListAsync(ct);

            // #697. Every ended run Runs has of the PBI, a person's included, and any the loop asked
            // for that the list leaves out.
            var ended = (await runs.ListEndedAsync(pbi, ct)).ToList();
            foreach (var id in asked.Where(id => ended.All(run => run.RunId != id)))
            {
                if (await runs.GetAsync(id, ct) is { End: not null } status)
                {
                    ended.Add(status);
                }
            }

            var body = JourneyComment.Render(lines, ended);
            if (await provider.ReadThreadAsync(pbi, ct) is not { } thread)
            {
                return $"{pbi.Repository} has no PBI {pbi.Id} to comment on.";
            }

            var kept = thread.Comments.FirstOrDefault(comment => JourneyComment.Is(comment.Body));
            if (kept is not null && kept.Body.ReplaceLineEndings("\n") == body)
            {
                return null;
            }

            var comment = kept is null
                ? await provider.CommentAsync(pbi, body, [], ct)
                : await provider.EditCommentAsync(pbi, kept.Id, body, ct);
            return comment.Mismatch;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Any failure, the vendor's or a port that cannot edit: the PBI has already moved.
            return failure.Message;
        }
    }

    [LoggerMessage(EventId = 5058, Level = LogLevel.Debug, Message = "No journey comment on {Pbi}: {Folder} has no .aio/version")]
    static partial void LogNoHarness(ILogger logger, string pbi, string folder);

    [LoggerMessage(EventId = 5056, Level = LogLevel.Warning, Message = "The journey comment of {Pbi} was not written: {Why}")]
    static partial void LogJourneyUnwritten(ILogger logger, string pbi, string why);
}
