using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// A session a person opens on a folder, from the rail's <c>…</c> menu (DESIGN.md#the-screens,
/// "Las sesiones a mano"): the person's machine, in the checkout as it stands. It dies and leaves
/// nothing behind — no row, no recording, no worktree — so it lives only in <see cref="LiveRuns"/>,
/// where the tray and the attach find it like any other live session.
/// <para>
/// Unlike a run, it starts before the request answers: there is no row to carry a failure, so a CLI
/// that is not installed is the request's refusal, in the provider's words.
/// </para>
/// </summary>
sealed partial class FolderSessions(
    IServiceScopeFactory scopes,
    IEnumerable<IAgentProvider> providers,
    PermissionModeSetting permissionMode,
    LiveRuns live,
    TimeProvider clock,
    IOptions<RunsOptions> options,
    IServer server,
    ILogger<FolderSessions> logger
)
{
    /// <summary>Opens the person's shell when <paramref name="provider"/> is null, otherwise the agent.</summary>
    /// <param name="repository">The folder's repository, which <c>aio issue</c> in the session reaches (#634), or null.</param>
    public async Task<ErrorOr<Guid>> OpenAsync(
        string folderPath,
        string? repository,
        string? provider,
        string? model,
        string? command,
        CancellationToken ct
    )
    {
        var id = Guid.CreateVersion7();

        // Where `aio done` would write if the agent called it, deleted with the session.
        var scratch = Path.Combine(Path.GetTempPath(), "aio-folder-sessions", id.ToString("N"));
        var place = new RunPlace(options.Value.Environment, folderPath);
        SessionOptions spawn;

        if (provider is null)
        {
            spawn = RunExecution.Shell(place, recordingPath: null);
        }
        else
        {
            if (providers.FirstOrDefault(candidate => candidate.Name == provider) is not { } agent)
            {
                return Error.NotFound("Session.NoAgent", $"No agent provider is called '{provider}'.");
            }

            var permissions = await permissionMode.ReadAsync(ct);

            try
            {
                var request = new AgentRequest(
                    command ?? string.Empty,
                    model,
                    place,
                    Path.Combine(scratch, "done.json"),
                    null,
                    Permissions: permissions,
                    Repository: repository,
                    Api: ControlPlaneAddress.Of(server)
                );
                spawn = agent.Launch(request);
            }
            catch (InvalidOperationException refused)
            {
                return Error.Conflict("Session.AgentMissing", refused.Message);
            }
        }

        // One scope per session, kept until it ends: a session is transient and disposable.
        var scope = scopes.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();

        try
        {
            await session.StartAsync(spawn, ct);
        }
        catch (InvalidOperationException refused)
        {
            await scope.DisposeAsync();
            return Error.Conflict("Session.NotStarted", refused.Message);
        }

        var current = live.Add(id, clock.GetUtcNow());
        current.OnFolder(new FolderSession(folderPath, provider, command ?? string.Empty));
        current.Started(session);

        _ = Task.Run(() => Pump(id, current, session, scope, scratch), CancellationToken.None);
        return id;
    }

    /// <summary>The only reader of the session, until it exits; then it is gone without a trace.</summary>
    async Task Pump(Guid id, LiveRun current, ISession session, AsyncServiceScope scope, string scratch)
    {
        try
        {
            await foreach (var chunk in session.ReadAsync(CancellationToken.None))
            {
                current.Wrote(clock.GetUtcNow());
                current.Output.Publish(chunk);
            }
        }
        catch (Exception unexpected)
        {
            Ended(logger, unexpected, id);
        }
        finally
        {
            current.Output.End();
            await scope.DisposeAsync();

            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }

            // Last, so a session read as gone has left nothing.
            live.Remove(id);
        }
    }

    [LoggerMessage(EventId = 5064, Level = LogLevel.Warning, Message = "Folder session {SessionId} stopped reading")]
    static partial void Ended(ILogger logger, Exception exception, Guid sessionId);
}
