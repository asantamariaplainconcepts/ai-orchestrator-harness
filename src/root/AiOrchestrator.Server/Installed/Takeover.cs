using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiOrchestrator.Server.Installed;

/// <summary>What a launch does about the app already on its port (#703).</summary>
enum Arrival
{
    /// <summary>The port is free, or was freed: listen there.</summary>
    Listen,

    /// <summary>Another program holds it: listen on any free port.</summary>
    ListenElsewhere,

    /// <summary>The same version answers: the browser was opened there, and this process ends.</summary>
    Leave,

    /// <summary>Another version answers and did not stop when asked: this process ends with a reason.</summary>
    Stuck,
}

/// <summary>
/// The launch that finds an app on its port (#517, #703). The same version means a second launch,
/// which only opens the browser. Another version is not kept quiet: on 13 October 0.3.2 found 0.3.1
/// there, opened the browser on it and left, and 0.3.1 kept answering until it was killed by hand.
/// Now the newer launch says which version runs, asks it to stop, waits for the port, and takes it.
/// </summary>
static class Takeover
{
    /// <summary>Long enough for a graceful stop, whose live sessions end with it.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    public static Arrival Settle(string origin, string version, HttpClient http, TextWriter output, Action<string> openBrowser, TimeSpan patience)
    {
        var (tenant, running) = WhoAnswers(origin, http);

        switch (tenant)
        {
            case Tenant.Nobody:
                return Arrival.Listen;

            case Tenant.Stranger:
                output.WriteLine($"Port {new Uri(origin).Port} is taken by another program; listening on a free port instead.");
                return Arrival.ListenElsewhere;
        }

        if (Short(running!) == Short(version))
        {
            output.WriteLine($"Already running at {origin}; opening the browser there.");
            openBrowser(origin + "/");
            return Arrival.Leave;
        }

        output.WriteLine($"aio {Short(running!)} is running at {origin}; this is {Short(version)}. Asking it to stop.");
        AskToStop(origin, http);

        for (var waited = TimeSpan.Zero; waited < patience; waited += TimeSpan.FromMilliseconds(250))
        {
            if (WhoAnswers(origin, http).Tenant == Tenant.Nobody)
            {
                output.WriteLine($"aio {Short(running!)} stopped; starting {Short(version)} at {origin}.");
                return Arrival.Listen;
            }

            Thread.Sleep(250);
        }

        output.WriteLine($"aio {Short(running!)} at {origin} did not stop within {patience.TotalSeconds:0} s. Stop it from Settings, then run aio again.");
        return Arrival.Stuck;
    }

    /// <summary>
    /// <c>/api/app/stop</c>, or, on a version from before it existed (0.3.2 and older), the
    /// <c>/api/console/stop</c> that Settings' <em>Stop console</em> calls.
    /// </summary>
    static void AskToStop(string origin, HttpClient http)
    {
        try
        {
            using var stop = http.PostAsync($"{origin}/api/app/stop", null).GetAwaiter().GetResult();
            if (stop.StatusCode == HttpStatusCode.NotFound)
            {
                using var _ = http.PostAsync($"{origin}/api/console/stop", null).GetAwaiter().GetResult();
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            // It went away while asked, which is what was asked. The wait below says either way.
        }
    }

    static string Short(string version) => version.Split('+')[0];

    enum Tenant
    {
        Nobody,
        Ourselves,
        Stranger,
    }

    /// <summary>
    /// Who is on the port, and which version when it is this app: it answers <c>/api/version</c>
    /// with one, and anything else that answers is somebody else's program.
    /// </summary>
    static (Tenant Tenant, string? Version) WhoAnswers(string origin, HttpClient http)
    {
        try
        {
            using var response = http.GetAsync($"{origin}/api/version").GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            try
            {
                return response.IsSuccessStatusCode
                    && JsonDocument.Parse(body).RootElement.TryGetProperty("version", out var version)
                    && version.GetString() is { } named
                    ? (Tenant.Ourselves, named)
                    : (Tenant.Stranger, null);
            }
            catch (JsonException)
            {
                return (Tenant.Stranger, null);
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            // Refused, or a timeout (which HttpClient raises as TaskCanceledException).
            return (Tenant.Nobody, null);
        }
    }

    /// <summary>The version that answers at <paramref name="origin"/>, or null; for <c>aio service status</c>.</summary>
    public static string? VersionAt(string origin)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        return WhoAnswers(origin, http).Version is { } version ? Short(version) : null;
    }

    /// <summary>
    /// <c>POST /api/app/stop</c>, what a newer launch calls (#703). Mapped only in the installed
    /// tool, answered only to loopback and never to a page: a browser sends <c>Origin</c> with
    /// every POST, and <c>aio</c> sends none.
    /// </summary>
    public static WebApplication MapAppStop(this WebApplication app)
    {
        app.MapPost(
                "/api/app/stop",
                Results<Ok<ConsoleStop>, StatusCodeHttpResult> (HttpContext context, IHostApplicationLifetime lifetime) =>
                {
                    if (!LocalCaller.IsLoopback(context) || context.Request.Headers.Origin.Count > 0)
                    {
                        return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
                    }

                    return StopConsole.Stop(lifetime);
                }
            )
            .ExcludeFromDescription();

        return app;
    }
}

/// <summary>Who sent a request, for the routes that change the installation (#703).</summary>
static class LocalCaller
{
    public static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);

    /// <summary>
    /// A request from a page that is not this app's: its <c>Origin</c> names another host. A POST
    /// with no body is sent cross-site without asking, so any page could otherwise press it.
    /// </summary>
    public static bool IsForeignPage(HttpContext context) =>
        context.Request.Headers.Origin.ToString() is { Length: > 0 } origin
        && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Authority == context.Request.Host.Value);
}
