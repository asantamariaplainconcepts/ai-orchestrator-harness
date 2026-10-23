using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Loop.Features;
using AiOrchestrator.Modules.Loop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// The engine on its own (#508): one <see cref="RunOnce"/> as soon as a folder is on, then one every
/// interval, over the folders that are on.
/// <para>
/// <b>It reads its settings before every wait (#562)</b>, from <see cref="ControlSettings"/>, and a
/// person's change wakes it through <see cref="Passes.Changes"/>: a new interval counts from the last
/// pass, and a folder turned on with none on before gets its pass now. With no folder on, or the
/// interval at zero or below, nothing is scheduled and it waits for the next change.
/// </para>
/// <para>
/// It lives in the module rather than in the Server because <c>RunOnce.Command</c> is internal to
/// it, and it sends the command through the same pipeline as the route, so a scheduled pass is
/// logged and authorized like one by hand. The migration step loads this module too, but builds its
/// host without running it, so no hosted service starts there.
/// </para>
/// <para>
/// A pass that throws is written down in <see cref="Passes"/> and the schedule goes on: the next
/// pass reads the vendor again, which is the only retry the loop has (principle 5 forbids any other).
/// </para>
/// </summary>
sealed partial class LoopSchedule(
    IServiceScopeFactory scopes,
    Passes passes,
    IOptions<LoopSettings> configured,
    TimeProvider clock,
    ILogger<LoopSchedule> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? last = null;
        var scheduled = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            var changed = passes.Changes;
            TimeSpan? wait;

            try
            {
                var (interval, anyOn) = await Read(stoppingToken);
                if (interval <= 0 || !anyOn)
                {
                    if (scheduled)
                    {
                        Off(logger, interval);
                    }

                    scheduled = false;
                    passes.Next = null;
                    wait = null;
                }
                else
                {
                    scheduled = true;
                    var now = clock.GetUtcNow();
                    var due = last is { } at && !passes.TakeSoon() ? at + TimeSpan.FromSeconds(interval) : now;
                    if (due <= now)
                    {
                        last = now;
                        await Pass(stoppingToken);
                        continue;
                    }

                    passes.Next = due;
                    wait = due - now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception failure)
            {
                // The settings could not be read; try again after the configured interval.
                SettingsFailed(logger, failure);
                wait = configured.Value.IntervalSeconds > 0 ? TimeSpan.FromSeconds(configured.Value.IntervalSeconds) : null;
            }

            using var wake = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, changed);
            try
            {
                await Task.Delay(wait ?? Timeout.InfiniteTimeSpan, clock, wake.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // A setting changed: read them again.
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        passes.Next = null;
    }

    async Task<(int Interval, bool AnyOn)> Read(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ControlSettings>().ReadAsync(ct);
        var folders = await scope.ServiceProvider.GetRequiredService<IFolderDirectory>().ListAsync(ct);
        return (settings.IntervalSeconds, folders.Any(folder => settings.IsOn(folder.Repository)));
    }

    async Task Pass(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunOnce.Command(OnlyFoldersOn: true), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception failure)
        {
            PassFailed(logger, failure);
        }
    }

    [LoggerMessage(
        EventId = 5052,
        Level = LogLevel.Information,
        Message = "The loop engine schedules nothing: no folder is on, or the interval is {Interval} seconds; a pass runs only by hand, POST /api/loop/run-once"
    )]
    static partial void Off(ILogger logger, int interval);

    [LoggerMessage(EventId = 5051, Level = LogLevel.Error, Message = "A scheduled pass of the loop failed")]
    static partial void PassFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5053, Level = LogLevel.Error, Message = "The loop engine could not read its settings")]
    static partial void SettingsFailed(ILogger logger, Exception exception);
}
