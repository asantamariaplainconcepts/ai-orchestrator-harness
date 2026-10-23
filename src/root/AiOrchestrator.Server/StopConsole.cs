using Microsoft.AspNetCore.Http.HttpResults;

namespace AiOrchestrator.Server;

/// <summary>
/// Stops this process from Settings (#548). Closing the browser leaves the installed app serving,
/// because a browser tab cannot tell a closed window from a reload, and the installed app has no
/// terminal to press Ctrl+C in. This is that Ctrl+C.
/// <para>
/// The stop is graceful, the same <see cref="IHostApplicationLifetime.StopApplication"/> an
/// install of an update uses: every live session ends with the process, and a loop run that had not
/// written its end record is recorded as failed the next time the console starts. Settings says so
/// before it asks. The answer is sent first and the stop follows a moment later, so the browser
/// hears that it worked rather than a dropped connection.
/// </para>
/// </summary>
static class StopConsole
{
    /// <summary>Long enough for the answer to leave, short enough that nothing else starts in between.</summary>
    static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    public static WebApplication MapStopConsole(this WebApplication app)
    {
        app.MapPost(
                "/api/console/stop",
                (IHostApplicationLifetime lifetime) => Stop(lifetime)
            )
            .WithName("StopConsole")
            .WithTags("Console");

        return app;
    }

    /// <summary>Answers that it stops, and stops once the answer has had <see cref="Delay"/> to leave.</summary>
    public static Ok<ConsoleStop> Stop(IHostApplicationLifetime lifetime)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(Delay);
            lifetime.StopApplication();
        });

        return TypedResults.Ok(new ConsoleStop(true));
    }
}

/// <param name="Stopping">True: the process stops once this answer has left.</param>
sealed record ConsoleStop(bool Stopping);
