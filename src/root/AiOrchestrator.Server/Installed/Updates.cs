using System.Net;
using System.Net.Http.Json;

namespace AiOrchestrator.Server.Installed;

/// <summary>
/// Whether nuget.org has a newer <c>aio</c> than this one (#679, ADR-0009). Nothing is downloaded or
/// installed here: the answer names the command that updates, which the person runs or Settings'
/// <em>Update and restart</em> runs for them (<see cref="SelfUpdate"/>, #703).
/// <para>
/// The tool asks once when it starts and again when the person presses <em>Check</em>. Reading the
/// answer waits for the last check and asks nothing new, so opening Settings costs no request.
/// </para>
/// </summary>
sealed partial class Updates(HttpClient http, string version, string how, ILogger<Updates> logger)
{
    /// <summary>The package's versions on nuget.org: anonymous, and a 404 until the first publish.</summary>
    public const string Index = "https://api.nuget.org/v3-flatcontainer/aio/index.json";

    Task<UpdateAnswer>? last;

    /// <summary>The last check's answer, or <c>unchecked</c> when nothing has asked yet.</summary>
    public Task<UpdateAnswer> Read(CancellationToken cancellationToken) =>
        last?.WaitAsync(cancellationToken) ?? Task.FromResult(Answer("unchecked"));

    /// <summary>
    /// Asks nuget.org again. The request does not own the check: a closed page leaves it running,
    /// and the next read gets its answer.
    /// </summary>
    public Task<UpdateAnswer> Check(CancellationToken cancellationToken)
    {
        var check = Ask();
        last = check;
        return check.WaitAsync(cancellationToken);
    }

    async Task<UpdateAnswer> Ask()
    {
        try
        {
            using var response = await http.GetAsync(Index);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Answer("unpublished");
            }

            response.EnsureSuccessStatusCode();

            var index = await response.Content.ReadFromJsonAsync<PackageIndex>();

            // Stable versions only: a preview on the feed is not what `dotnet tool update` installs.
            var latest = (index?.Versions ?? [])
                .Select(published => Version.TryParse(published, out var parsed) ? parsed : null)
                .OfType<Version>()
                .DefaultIfEmpty()
                .Max();

            if (latest is null)
            {
                return Answer("unpublished");
            }

            return Version.TryParse(version.Split('+')[0], out var running) && latest <= running
                ? Answer("up-to-date", latest.ToString())
                : Answer("available", latest.ToString());
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Offline, a timeout, a body that is not the index. The check says so; the app keeps working.
            CheckFailed(logger, failure);
            return Answer("error", detail: failure.Message);
        }
    }

    UpdateAnswer Answer(string state, string? latest = null, string? detail = null) =>
        new(state, version.Split('+')[0], latest, how, detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The update check failed")]
    static partial void CheckFailed(ILogger logger, Exception failure);

    sealed record PackageIndex(string[]? Versions);
}

/// <param name="State">One of <c>unchecked</c>, <c>up-to-date</c>, <c>available</c>, <c>unpublished</c> (nuget.org has no <c>aio</c> yet) or <c>error</c>.</param>
/// <param name="Installed">This process's version.</param>
/// <param name="Latest">The newest stable version on nuget.org, when the check found one.</param>
/// <param name="How">How this process was started: <c>tool</c>, <c>dnx</c> or <c>checkout</c>. It picks the command that updates.</param>
/// <param name="Detail">Why the check failed, when it did.</param>
sealed record UpdateAnswer(string State, string Installed, string? Latest, string How, string? Detail);

/// <summary>The start-up check: once, only in the tool.</summary>
sealed class StartupUpdateCheck(Updates updates) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Installation.IsTool ? updates.Check(stoppingToken) : Task.CompletedTask;
}

static class UpdateEndpoints
{
    public static IServiceCollection AddUpdates(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(Updates), http => http.Timeout = TimeSpan.FromSeconds(10));

        return services
            .AddSingleton(provider => new Updates(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(Updates)),
                Installation.Version,
                Installation.How,
                provider.GetRequiredService<ILogger<Updates>>()
            ))
            .AddHostedService<StartupUpdateCheck>();
    }

    public static WebApplication MapUpdates(this WebApplication app)
    {
        var group = app.MapGroup("/api/updates").WithTags("Updates");

        group
            .MapGet("", async (Updates updates, CancellationToken cancellationToken) =>
                TypedResults.Ok(await updates.Read(cancellationToken)))
            .WithName("ReadUpdates");

        group
            .MapPost("/check", async (Updates updates, CancellationToken cancellationToken) =>
                TypedResults.Ok(await updates.Check(cancellationToken)))
            .WithName("CheckForUpdates");

        // #703. Update and restart, for the installed tool.
        group.MapInstall();

        return app;
    }
}
