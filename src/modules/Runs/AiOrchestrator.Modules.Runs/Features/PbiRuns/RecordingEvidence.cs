using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// #697. What happens after a run ended, in the background: its recording is copied to the
/// repository's <c>aio/evidence</c> branch, and the loop is told so the PBI's journey comment lists
/// it with the path there. Another instance replays it from the vendor with nothing in its database.
/// <para>
/// <b>It never fails the run or blocks the PBI.</b> The row is saved as ended before this starts. A
/// push that does not land is a warning in the log, and the next run of the repository pushes every
/// recording of this instance the branch still lacks, so nothing has to remember what failed. The
/// recordings stay where they are: the branch is a copy.
/// </para>
/// <para>
/// <b>Only to a repository with the harness.</b> A folder without <c>.aio/version</c>, such as a
/// client's repository a person opened a terminal in, gets nothing pushed; a debug line says so.
/// </para>
/// <para>
/// One push at a time from this instance, so two runs ending together do not refuse each other.
/// </para>
/// </summary>
sealed partial class RecordingEvidence(
    IServiceScopeFactory scopes,
    IOptions<RunsOptions> options,
    IConfiguration configuration,
    ILogger<RecordingEvidence> logger,
    IEvidenceBranch? branch = null
)
{
    readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>Where a run's recording goes on the branch, or null when its session never wrote one.</summary>
    public static string? PathOf(PbiRun run) => File.Exists(run.RecordingPath) ? EvidenceBranch.RecordingOf(run.PbiId, run.Id) : null;

    /// <summary>Starts the copy and the telling; the run has ended and nothing waits for either.</summary>
    public void Copy(Guid runId) => _ = Task.Run(() => CopyAsync(runId, CancellationToken.None), CancellationToken.None);

    internal async Task CopyAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            if (await database.PbiRuns.AsNoTracking().SingleOrDefaultAsync(run => run.Id == runId, ct) is not { } ended)
            {
                return;
            }

            // A folder without the harness is someone else's repository: nothing is pushed there, and
            // the loop is told all the same, which applies the same rule to the comment.
            if (branch is not null && HarnessInstall.In(ended.FolderPath))
            {
                await Push(database, ended, branch, ct);
            }
            else if (branch is not null)
            {
                NoHarness(logger, ended.Id, ended.FolderPath);
            }

            if (scope.ServiceProvider.GetService<IRunEndings>() is { } endings)
            {
                await endings.EndedAsync(ended.Pbi, ct);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            Stopped(logger, runId, failure.Message);
        }
    }

    async Task Push(RunsDbContext database, PbiRun ended, IEvidenceBranch evidence, CancellationToken ct)
    {
        var runs = await database
            .PbiRuns.AsNoTracking()
            .Where(run => run.Repository == ended.Repository && run.EndedAt != null)
            .ToListAsync(ct);
        var files = runs.OrderBy(run => run.StartedAt)
            .Select(run => (Run: run, Path: PathOf(run)))
            .Where(each => each.Path is not null)
            .Select(each => new EvidenceFile(
                each.Path!,
                each.Run.RecordingPath,
                $"{each.Run.PbiId} · {(each.Run.State.Length > 0 ? each.Run.State : "no state")} · {each.Run.Id:N}"
            ))
            .ToList();
        if (files.Count == 0)
        {
            return;
        }

        var scratch = Path.Combine(options.Value.RunDirectory(configuration.GetConnectionString(RunsModule.ConnectionStringName)), ".evidence");
        await _one.WaitAsync(ct);
        try
        {
            var pushed = await evidence.PushAsync(ended.FolderPath, scratch, files, ct);
            if (pushed.Refusal is { } why)
            {
                Unpushed(logger, ended.Id, why);
            }
            else if (pushed.Pushed.Count > 0)
            {
                Pushed(logger, pushed.Pushed.Count, EvidenceBranch.Name, ended.Repository);
            }
        }
        finally
        {
            _one.Release();
        }
    }

    [LoggerMessage(EventId = 5066, Level = LogLevel.Warning, Message = "The recording of run {RunId} was not copied to aio/evidence, and the next run tries again: {Why}")]
    static partial void Unpushed(ILogger logger, Guid runId, string why);

    [LoggerMessage(EventId = 5067, Level = LogLevel.Information, Message = "{Count} recordings copied to {Branch} of {Repository}")]
    static partial void Pushed(ILogger logger, int count, string branch, string repository);

    [LoggerMessage(EventId = 5069, Level = LogLevel.Debug, Message = "The recording of run {RunId} stays local: {Folder} has no .aio/version")]
    static partial void NoHarness(ILogger logger, Guid runId, string folder);

    [LoggerMessage(EventId = 5068, Level = LogLevel.Warning, Message = "What follows the end of run {RunId} stopped: {Why}")]
    static partial void Stopped(ILogger logger, Guid runId, string why);
}
