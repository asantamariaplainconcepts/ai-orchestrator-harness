using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// Carries one run from its row to its end, in the background: the environment the folder chose for
/// the run's state prepares a place, the provider says what to spawn, the environment says how that
/// starts inside the place, a session runs it and records it, and when it exits the end record and
/// the cost are written to the row and the place is retired.
/// <para>
/// <b>A run of the loop ends when its end record appears.</b> Interactive <c>claude</c> stays open
/// after <c>aio done</c>, so the record is watched for, and once it is there the agent has
/// <see cref="RunsOptions.EndGrace"/> to exit before its session is killed. A manual run ends when a
/// person exits it or stops it: an agent a person is talking to is not cut off for saying done.
/// </para>
/// <para>
/// <b>Every way it can go wrong ends the row.</b> A place that cannot be prepared, a CLI that is not
/// installed, an agent that never called <c>aio done</c>: each is a <see cref="RunFailure"/> with the
/// sentence that explains it. Nothing retries (principle 5).
/// </para>
/// <para>
/// <b>A run of the loop also ends when its agent waits for a person.</b> When the session has been
/// silent for <see cref="RunsOptions.PersonWait"/> on a screen its provider reads as a permission
/// prompt or a login (#706), the session is killed and the run fails with the provider's sentence, so
/// the loop holds the PBI with what to do instead of leaving it stale. Nothing answers the prompt.
/// </para>
/// <para>
/// <b>And when it has been silent for the stale threshold without its end record</b>, whatever its
/// screen says (#764). On 15 October two reviews finished their turn asking a person to allow a
/// commit, sat at Claude Code's prompt, and stayed live for 86 and 14 minutes until a person
/// stopped them: the prompt is not a permission prompt, so the watch above never fired, and
/// stale was only shown. The run fails with <c>WaitingForInput</c> and the last lines of its screen.
/// </para>
/// </summary>
sealed partial class RunExecution(
    IServiceScopeFactory scopes,
    IEnumerable<IEnvironment> environments,
    IEnumerable<IAgentProvider> providers,
    PermissionModeSetting permissionMode,
    StaleThreshold threshold,
    StateEnvironments stateEnvironments,
    LiveRuns live,
    RecordingEvidence evidence,
    TimeProvider clock,
    IOptions<RunsOptions> options,
    IServer server,
    ILogger<RunExecution> logger
)
{
    /// <summary>What a run branches from when its branch does not exist yet: the remote's default branch.</summary>
    internal const string BaseRef = "HEAD";

    static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>How often a silent run reads the stale threshold again, so one changed in Settings applies to a run already live.</summary>
    static readonly TimeSpan ThresholdRead = TimeSpan.FromSeconds(5);

    /// <summary>How many lines of its screen a run ended for its silence quotes.</summary>
    const int Quoted = 6;

    readonly RunsOptions _options = options.Value;

    /// <summary>Starts the run in the background. The row exists and the run is registered as live.</summary>
    public void Start(PbiRun run) => _ = Task.Run(() => Execute(run), CancellationToken.None);

    async Task Execute(PbiRun run)
    {
        var current = live.Find(run.Id)!;
        RunPlace? place = null;

        try
        {
            // Read now, like the permission mode, so a choice changed on the folder's page applies to the next run (#612).
            var chosen = await stateEnvironments.ReadAsync(run.Repository, run.State, CancellationToken.None) ?? _options.Environment;

            if (environments.FirstOrDefault(environment => environment.Name == chosen) is not { } environment)
            {
                var registered = string.Join(", ", environments.Select(candidate => candidate.Name));
                await End(
                    run.Id,
                    Failed(
                        RunFailure.EnvironmentFailed,
                        $"No environment is called '{chosen}'. There is {registered}: choose one for {run.State} on the folder's page."
                    )
                );
                return;
            }

            try
            {
                // The run's directory goes with the request, so a sandbox can keep the end record in reach.
                var asked = new PlaceRequest(run.FolderPath, run.Branch, BaseRef, Path.GetDirectoryName(run.EndRecordPath));
                place = await environment.PrepareAsync(asked, CancellationToken.None);
            }
            catch (Exception refused) when (refused is not OperationCanceledException)
            {
                await End(run.Id, Failed(RunFailure.EnvironmentFailed, refused.Message));
                return;
            }

            current.Placed(place.WorkingDirectory);
            await Change(run.Id, row => row.Placed(place));

            // Read now, not when the run was asked for, so a mode changed in Settings applies to the next run (#578).
            // An environment with a mode of its own, such as a sandbox, sets it instead.
            var permissions = environment.Permissions ?? await permissionMode.ReadAsync(CancellationToken.None);
            var request = new AgentRequest(
                run.Command,
                run.Model,
                place,
                run.EndRecordPath,
                run.RecordingPath,
                run.Id,
                run.StartedAt,
                permissions,
                run.Repository,
                ControlPlaneAddress.Of(server)
            );
            IAgentProvider? provider = null;
            SessionOptions spawn;

            if (run.Provider is null)
            {
                spawn = Shell(place, run.RecordingPath);
            }
            else
            {
                provider = providers.FirstOrDefault(candidate => candidate.Name == run.Provider);

                if (provider is null)
                {
                    var installed = string.Join(", ", providers.Select(candidate => candidate.Name));
                    await End(run.Id, Failed(RunFailure.AgentNotFound, $"No agent provider is called '{run.Provider}'. There is {installed}."));
                    return;
                }

                try
                {
                    spawn = provider.Launch(request);
                }
                catch (InvalidOperationException refused)
                {
                    await End(run.Id, Failed(RunFailure.AgentNotFound, refused.Message));
                    return;
                }
            }

            // The environment says how the launch starts inside the place; the session, its recording
            // and the end record's path are the same whatever it answers.
            try
            {
                spawn = environment.Enter(place, spawn);
            }
            catch (InvalidOperationException refused)
            {
                await End(run.Id, Failed(RunFailure.EnvironmentFailed, refused.Message));
                return;
            }

            // One scope per run: a session is transient and disposable, and resolved from the root it
            // would be held until the server stopped.
            await using var scope = scopes.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            try
            {
                await session.StartAsync(spawn, CancellationToken.None);
            }
            catch (InvalidOperationException refused)
            {
                await End(run.Id, Failed(RunFailure.AgentNotFound, refused.Message));
                return;
            }

            current.Started(session);

            using var exited = new CancellationTokenSource();
            var watch = run.Manual ? Task.CompletedTask : EndWhenRecorded(run.EndRecordPath, session, exited.Token);
            var screen = new ScreenTail(spawn.Columns, spawn.Rows);
            var waiting = run.Manual ? Task.FromResult<RunEnd?>(null) : EndWhenWaiting(provider, screen, current, session, run.EndRecordPath, exited.Token);

            // The only reader of the session. Viewers in the browser get each chunk from the fan-out
            // on the live run, so watching a run never takes output from it.
            await foreach (var chunk in session.ReadAsync(CancellationToken.None))
            {
                current.Wrote(clock.GetUtcNow());
                screen.Append(chunk.Span);
                current.Output.Publish(chunk);
            }

            current.Output.End();
            await exited.CancelAsync();
            await watch;
            var waited = await waiting;

            var end = provider is null ? null : await provider.EndAsync(request, CancellationToken.None);
            var reading = run.Manual ? null : EndRecords.Parse(end?.Record, run.EndRecordPath, run.Outcomes, run.StartedAt);

            // Killed waiting for a person, so the missing end record is not the agent's forgetting.
            if (waited is not null && reading is { Failure: RunFailure.NoEndRecord })
            {
                reading = waited;
            }

            // Whatever the agent had written, a person stopped it: the loop holds on that, not on
            // an outcome the agent never got to finish.
            if (current.StoppedByPerson && !run.Manual)
            {
                reading = Failed(RunFailure.Stopped, "Stopped by a person.");
            }

            await Change(run.Id, row => row.End(reading, end?.Record, end?.Usage, clock.GetUtcNow()));
        }
        catch (Exception unexpected)
        {
            Stopped(logger, unexpected, run.Id);
            await End(run.Id, Failed(RunFailure.NoEndRecord, $"The run stopped before it could end: {unexpected.Message}"));
        }
        finally
        {
            current.Output.End();

            // Retired while still live, so a run read as not live has nothing left running for it.
            await Retire(run, place);
            live.Remove(run.Id);

            // #697. Its recording to aio/evidence and its row to the journey comment, after the end is saved.
            evidence.Copy(run.Id);
        }
    }

    /// <summary>
    /// Kills the session once the end record is there and the agent has had its grace to exit. Ends
    /// quietly when the session exits first.
    /// </summary>
    async Task EndWhenRecorded(string recordPath, ISession session, CancellationToken exited)
    {
        try
        {
            while (!File.Exists(recordPath))
            {
                await Task.Delay(Poll, clock, exited);
            }

            await Task.Delay(_options.EndGrace, clock, exited);
            await session.KillAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (exited.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Kills the session once it has been silent for <see cref="RunsOptions.PersonWait"/> on a screen
    /// <paramref name="provider"/> reads as waiting for a person, or for the stale threshold with no
    /// end record at <paramref name="recordPath"/>, and answers how the run failed. The provider reads
    /// the screen once per silence, not on every poll of a session that stays silent. Ends with null
    /// when the session exits first.
    /// </summary>
    async Task<RunEnd?> EndWhenWaiting(
        IAgentProvider? provider,
        ScreenTail screen,
        LiveRun current,
        ISession session,
        string recordPath,
        CancellationToken exited
    )
    {
        DateTimeOffset? read = null;
        var after = TimeSpan.MaxValue;
        var afterReadAt = DateTimeOffset.MinValue;
        try
        {
            while (true)
            {
                await Task.Delay(Poll, clock, exited);
                var now = clock.GetUtcNow();
                var since = current.LastOutputAt;

                if (provider is not null && since != read && now - since >= _options.PersonWait)
                {
                    read = since;
                    if (provider.WaitsForPerson(screen.Text()) is { } wait)
                    {
                        await session.KillAsync(CancellationToken.None);
                        return Failed(wait.Kind == PersonWaitKind.Login ? RunFailure.NotLoggedIn : RunFailure.WaitingForPerson, wait.Why);
                    }
                }

                if (now - afterReadAt >= ThresholdRead)
                {
                    after = await threshold.ReadAsync(exited);
                    afterReadAt = now;
                }

                // An agent that called aio done and sits at its prompt is ended by its end record.
                if (now - since >= after && !File.Exists(recordPath))
                {
                    await session.KillAsync(CancellationToken.None);
                    return Failed(RunFailure.WaitingForInput, Silent(after, screen.LastLines(Quoted)));
                }
            }
        }
        catch (OperationCanceledException) when (exited.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>The reason of a run ended for its silence: how long, what to do, and the last lines of its screen.</summary>
    internal static string Silent(TimeSpan after, IReadOnlyList<string> lines)
    {
        var minutes = (int)after.TotalMinutes;
        var silence = minutes == 0 ? $"{after.Seconds}s" : after.Seconds == 0 ? $"{minutes}m" : $"{minutes}m {after.Seconds}s";
        var said = lines.Count == 0 ? "Its screen was empty." : "Its screen ended with:\n" + string.Join('\n', lines);
        return $"The agent wrote nothing for {silence} and did not call aio done, so its session was stopped. Do what it asks, then release. {said}";
    }

    /// <summary>
    /// Gives the place back, unless another live run is working in it: a person's terminal on the
    /// same PBI shares the worktree. A refusal leaves the place on disk and the run as it ended — an
    /// uncommitted file is the script's reason to keep a worktree, not the run's failure.
    /// <para>
    /// The agent's provider retires first, while the place is still on disk, so what it wrote
    /// outside the place for it (Claude Code's trust, #546) goes with it. A later run there writes
    /// it again when it launches.
    /// </para>
    /// </summary>
    async Task Retire(PbiRun run, RunPlace? place)
    {
        var runId = run.Id;
        if (place is null || live.Uses(place.WorkingDirectory, except: runId))
        {
            return;
        }

        try
        {
            providers.FirstOrDefault(candidate => candidate.Name == run.Provider)?.Retire(place);
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            // What the agent wrote outside the place is not a reason to keep the place.
            NotTakenBack(logger, runId, place.WorkingDirectory, refused.Message);
        }

        try
        {
            if (environments.FirstOrDefault(environment => environment.Name == place.Environment) is { } environment)
            {
                await environment.RetireAsync(place, CancellationToken.None);
            }
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            LeftInPlace(logger, runId, place.WorkingDirectory, refused.Message);
        }
    }

    /// <summary>The person's login shell, for a terminal opened by hand.</summary>
    internal static SessionOptions Shell(RunPlace place, string? recordingPath)
    {
        string shell;
        string[] arguments;

        if (OperatingSystem.IsWindows())
        {
            shell = System.Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            arguments = [];
        }
        else
        {
            shell = System.Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } login ? login : "/bin/sh";
            arguments = ["-l"];
        }

        return new SessionOptions(shell, arguments, place.WorkingDirectory, new Dictionary<string, string>(), 120, 30, recordingPath);
    }

    static RunEnd Failed(RunFailure cause, string reason) => new(null, reason, cause, []);

    Task End(Guid runId, RunEnd end) => Change(runId, row => row.End(end, null, null, clock.GetUtcNow()));

    async Task Change(Guid runId, Action<PbiRun> change)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var row = await database.PbiRuns.SingleAsync(entity => entity.Id == runId);
        change(row);
        await database.SaveChangesAsync();
    }

    [LoggerMessage(EventId = 5061, Level = LogLevel.Error, Message = "Run {RunId} stopped before it could end")]
    static partial void Stopped(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(EventId = 5062, Level = LogLevel.Warning, Message = "Run {RunId} left {Place} in place: {Reason}")]
    static partial void LeftInPlace(ILogger logger, Guid runId, string place, string reason);

    [LoggerMessage(EventId = 5065, Level = LogLevel.Warning, Message = "Run {RunId} could not take back what its agent wrote for {Place}: {Reason}")]
    static partial void NotTakenBack(ILogger logger, Guid runId, string place, string reason);
}
