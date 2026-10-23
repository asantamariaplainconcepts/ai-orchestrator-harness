using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// The runs: the loop asks for one through <see cref="IRunRequests"/>, a person opens one on a
/// PBI with <see cref="OpenAsync"/>, and the PBI's Journey reads them all with <see cref="ListAsync"/>.
/// <para>
/// <b>A manual run is invisible to the loop.</b> <see cref="GetAsync"/> answers null for it and
/// <see cref="ListLiveAsync"/> leaves it out, so it never counts against the WIP limit and its end
/// is never an outcome the engine could turn into a transition. It is stored like any other, so the
/// Journey shows what a person did by hand beside what the loop did.
/// </para>
/// <para>
/// <b>Stale is read, not stored.</b> A live run that has written nothing for
/// <see cref="StaleThreshold"/> (#562) reads as stale, and reads as live again the moment it
/// writes. The clock and the last output are both in this process, so there is nothing to keep in
/// step.
/// </para>
/// </summary>
sealed class RunBook(
    IServiceScopeFactory scopes,
    RunExecution execution,
    PbiScripts scripts,
    LiveRuns live,
    TimeProvider clock,
    IOptions<RunsOptions> options,
    StaleThreshold threshold,
    IConfiguration configuration,
    IEnumerable<IEnvironment> environments,
    StateEnvironments stateEnvironments
) : IRunRequests
{
    readonly RunsOptions _options = options.Value;

    public Task<Guid> StartAsync(RunRequest request, CancellationToken ct) =>
        Begin(PbiRun.Loop(request, Directory(), clock.GetUtcNow()), ct);

    public async Task<RunStatus?> GetAsync(Guid runId, CancellationToken ct)
    {
        var run = await Read(runs => runs.Where(entity => entity.Id == runId && !entity.Manual), ct);

        return run.Count == 0 ? null : Status(run[0], await threshold.ReadAsync(ct));
    }

    public async Task<IReadOnlyList<RunStatus>> ListLiveAsync(CancellationToken ct)
    {
        var runs = await Read(runs => runs.Where(entity => entity.EndedAt == null && !entity.Manual), ct);
        var staleAfter = await threshold.ReadAsync(ct);

        return [.. runs.Where(run => !run.Ended).Select(run => Status(run, staleAfter))];
    }

    public async Task<IReadOnlyList<RunStatus>> ListEndedAsync(PbiRef pbi, CancellationToken ct)
    {
        var runs = await Read(
            runs => runs.Where(entity => entity.Repository == pbi.Repository && entity.PbiId == pbi.Id && entity.EndedAt != null),
            ct
        );
        var staleAfter = await threshold.ReadAsync(ct);

        return [.. runs.OrderBy(run => run.StartedAt).Select(run => Status(run, staleAfter))];
    }

    public async Task<IReadOnlyList<RunStatus>> ListRepositoryAsync(string repository, CancellationToken ct)
    {
        var runs = await Read(runs => runs.Where(entity => entity.Repository == repository), ct);
        var staleAfter = await threshold.ReadAsync(ct);

        return [.. runs.OrderBy(run => run.StartedAt).Select(run => Status(run, staleAfter))];
    }

    public IScriptPlace OpenScriptPlace(PbiRef pbi, string folderPath, string branch) => scripts.Open(folderPath, branch);

    public Task<TimeSpan> StaleAfterAsync(CancellationToken ct) => threshold.ReadAsync(ct);

    /// <summary>
    /// The environment the run's state would get, as <see cref="RunExecution"/> reads it, and what it
    /// says about placing this run now. An environment with no adapter answers null: the run fails
    /// with <see cref="RunFailure.EnvironmentFailed"/> and names the ones there are.
    /// </summary>
    public async Task<RunPlacement?> PlacementAsync(RunRequest request, CancellationToken ct)
    {
        var chosen = await stateEnvironments.ReadAsync(request.Pbi.Repository, request.State, ct) ?? _options.Environment;

        if (environments.FirstOrDefault(environment => environment.Name == chosen) is not { } environment)
        {
            return null;
        }

        var waits = await environment.WaitsAsync(new PlaceRequest(request.FolderPath, request.Branch, RunExecution.BaseRef), ct);
        return new RunPlacement(environment.Name, environment.Sandboxed, waits);
    }

    /// <summary>
    /// Opens a session by hand on a PBI, in its branch and its environment: a terminal running the
    /// person's shell when <paramref name="provider"/> is null, otherwise the agent with its model and
    /// an optional command.
    /// </summary>
    public Task<Guid> OpenAsync(
        PbiRef pbi,
        string state,
        string folderPath,
        string branch,
        string? provider,
        string? model,
        string? command,
        CancellationToken ct
    ) => Begin(PbiRun.Opened(pbi, state, folderPath, branch, provider, model, command, Directory(), clock.GetUtcNow()), ct);

    /// <summary>
    /// Ends a live run's session, as a person stopping it. A loop run then ends as
    /// <see cref="RunFailure.Stopped"/>, which the engine holds on. A run that is not live, or whose
    /// session has not started yet, is left as it is.
    /// </summary>
    public async Task<bool> StopAsync(Guid runId, CancellationToken ct)
    {
        if (live.Find(runId) is not { Session: { } session } current)
        {
            return false;
        }

        current.Stop();
        await session.KillAsync(ct);
        return true;
    }

    /// <summary>Every run of a PBI, the loop's and the manual ones, oldest first, as the Journey shows them.</summary>
    public async Task<IReadOnlyList<PbiRunView>> ListAsync(PbiRef pbi, CancellationToken ct)
    {
        var runs = await Read(
            runs => runs.Where(entity => entity.Repository == pbi.Repository && entity.PbiId == pbi.Id),
            ct
        );
        var staleAfter = await threshold.ReadAsync(ct);

        return [.. runs.OrderBy(run => run.StartedAt).Select(run => View(run, staleAfter))];
    }

    async Task<Guid> Begin(PbiRun run, CancellationToken ct)
    {
        // Live before the row exists, so no reader ever finds the row without its process and takes
        // it for one the server lost.
        live.Add(run.Id, run.StartedAt);

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            database.PbiRuns.Add(run);
            await database.SaveChangesAsync(ct);
        }
        catch
        {
            live.Remove(run.Id);
            throw;
        }

        execution.Start(run);
        return run.Id;
    }

    /// <summary>
    /// The rows a query names, with any the server lost ended on the way: a row that has not ended
    /// and is not live had its process stop under it, so it ends with what its end record says, or
    /// as a run that left none.
    /// </summary>
    async Task<List<PbiRun>> Read(Func<IQueryable<PbiRun>, IQueryable<PbiRun>> query, CancellationToken ct)
    {
        // Taken before the query: a run that ends while it reads was live here, and its row is
        // saved as ended before it leaves the live set, so it is never mistaken for a lost one.
        var wasLive = live.Ids();

        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var runs = await query(database.PbiRuns).ToListAsync(ct);
        var lost = runs.Where(run => !run.Ended && !wasLive.Contains(run.Id) && live.Find(run.Id) is null).ToList();

        foreach (var run in lost)
        {
            var record = ReadRecord(run.EndRecordPath);
            var end = run.Manual ? null : EndRecords.Parse(record, run.EndRecordPath, run.Outcomes, run.StartedAt);

            if (end is { Failure: RunFailure.NoEndRecord })
            {
                end = end with { Reason = $"The control plane stopped while the run was live. {end.Reason}" };
            }

            run.End(end, record, null, clock.GetUtcNow());
        }

        if (lost.Count > 0)
        {
            await database.SaveChangesAsync(ct);
        }

        return runs;
    }

    RunStatus Status(PbiRun run, TimeSpan staleAfter)
    {
        var status = new RunStatus(run.Id, run.Pbi, run.State, null)
        {
            Provider = run.Provider,
            Model = run.Model,
            Command = run.Command,
            StartedAt = run.StartedAt,
            Manual = run.Manual,
        };
        if (run.Ended)
        {
            return status with { End = End(run), Recording = RecordingEvidence.PathOf(run) };
        }

        var (lastOutputAt, stale) = Liveness(run, staleAfter);
        return status with { LastOutputAt = lastOutputAt, Stale = stale };
    }

    PbiRunView View(PbiRun run, TimeSpan staleAfter)
    {
        var (lastOutputAt, stale) = run.Ended ? (null, false) : Liveness(run, staleAfter);

        return new PbiRunView(
            run.Id,
            run.State,
            run.Manual,
            run.Provider,
            run.Model,
            run.Command,
            run.StartedAt,
            run.EndedAt,
            lastOutputAt,
            stale,
            run.Outcome is not null || run.Failure is not null ? End(run) : null,
            run.EndRecord,
            run.InputTokens,
            run.OutputTokens,
            run.CostUsd,
            run.RecordingPath
        );
    }

    (DateTimeOffset? LastOutputAt, bool Stale) Liveness(PbiRun run, TimeSpan staleAfter) =>
        live.Find(run.Id) is { } current
            ? (current.LastOutputAt, current.IsStale(clock.GetUtcNow(), staleAfter))
            : (null, false);

    static RunEnd End(PbiRun run) =>
        new(run.Outcome, run.Reason, run.Failure, run.Problems)
        {
            EndedAt = run.EndedAt,
            CostUsd = run.CostUsd,
            InputTokens = run.InputTokens,
            OutputTokens = run.OutputTokens,
        };

    string Directory() => _options.RunDirectory(configuration.GetConnectionString(RunsModule.ConnectionStringName));

    static string? ReadRecord(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>A run as the PBI's Journey shows it.</summary>
/// <param name="RunId">The run.</param>
/// <param name="State">The state it belongs to.</param>
/// <param name="Manual">A person opened it; it moved nothing.</param>
/// <param name="Provider">The agent CLI, or null for a terminal.</param>
/// <param name="Model">The model, or null for the CLI's default.</param>
/// <param name="Command">What the agent was asked, or empty.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null while live.</param>
/// <param name="LastOutputAt">When it last wrote, while live.</param>
/// <param name="Stale">Live and silent for longer than the threshold.</param>
/// <param name="End">How it ended; null while live, and for a manual run that ended without failing.</param>
/// <param name="EndRecord">The end record as <c>aio done</c> wrote it, or null.</param>
/// <param name="InputTokens">Every token the model read, or null when unknown.</param>
/// <param name="OutputTokens">Every token the model wrote, or null when unknown.</param>
/// <param name="CostUsd">What it cost, or null when unknown.</param>
/// <param name="RecordingPath">Its asciicast v2 recording.</param>
sealed record PbiRunView(
    Guid RunId,
    string State,
    bool Manual,
    string? Provider,
    string? Model,
    string Command,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset? LastOutputAt,
    bool Stale,
    RunEnd? End,
    string? EndRecord,
    long? InputTokens,
    long? OutputTokens,
    decimal? CostUsd,
    string RecordingPath
);
