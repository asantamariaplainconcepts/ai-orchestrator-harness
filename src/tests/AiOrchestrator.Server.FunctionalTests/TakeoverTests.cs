using System.Net;
using System.Net.Sockets;
using AiOrchestrator.Server.Installed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// #703. A launch that finds an app on its port, against a real one on a free port of loopback that
/// answers <c>/api/version</c> and stops when <c>/api/app/stop</c> asks, as the installed app does.
/// </summary>
public sealed class TakeoverTests
{
    [Fact]
    public async Task A_newer_version_asks_the_old_one_to_stop_and_keeps_the_port()
    {
        await using var old = await Running("0.3.1+515d5c24");
        using var output = new StringWriter();
        var opened = new List<string>();

        var arrival = Takeover.Settle(old.Origin, "0.3.2+63cd88f5", new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, output, opened.Add, TimeSpan.FromSeconds(20));

        arrival.ShouldBe(Arrival.Listen);
        output.ToString().ShouldContain($"aio 0.3.1 is running at {old.Origin}; this is 0.3.2.");
        old.StopsAsked.ShouldBe(1);
        opened.ShouldBeEmpty();

        // The port is the newer launch's to take.
        using var listener = new TcpListener(IPAddress.Loopback, old.Port);
        listener.Start();
    }

    [Fact]
    public async Task The_same_version_opens_the_browser_there_and_leaves()
    {
        await using var running = await Running("0.3.2+aaaaaaaa");
        using var output = new StringWriter();
        var opened = new List<string>();

        var arrival = Takeover.Settle(running.Origin, "0.3.2+bbbbbbbb", new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, output, opened.Add, TimeSpan.FromSeconds(5));

        arrival.ShouldBe(Arrival.Leave);
        opened.ShouldBe([running.Origin + "/"]);
        running.StopsAsked.ShouldBe(0);
        output.ToString().ShouldContain("Already running at");
    }

    [Fact]
    public async Task An_old_version_that_does_not_stop_is_said_and_left_alone()
    {
        await using var stubborn = await Running("0.3.1", stops: false);
        using var output = new StringWriter();

        var arrival = Takeover.Settle(stubborn.Origin, "0.3.2", new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, output, _ => { }, TimeSpan.FromSeconds(1));

        arrival.ShouldBe(Arrival.Stuck);
        output.ToString().ShouldContain("did not stop within 1 s");
    }

    [Fact]
    public void A_free_port_is_taken_without_a_word()
    {
        var port = FreePort();
        using var output = new StringWriter();

        Takeover.Settle($"http://127.0.0.1:{port}", "0.3.2", new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, output, _ => { }, TimeSpan.FromSeconds(1))
            .ShouldBe(Arrival.Listen);

        output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void The_stop_is_answered_to_loopback_only_and_never_to_a_page()
    {
        LocalCaller.IsLoopback(Request("127.0.0.1")).ShouldBeTrue();
        LocalCaller.IsLoopback(Request("::1")).ShouldBeTrue();
        LocalCaller.IsLoopback(Request("192.168.1.10")).ShouldBeFalse();

        LocalCaller.IsForeignPage(Request("127.0.0.1", origin: "http://127.0.0.1:48117")).ShouldBeFalse();
        LocalCaller.IsForeignPage(Request("127.0.0.1", origin: "https://somewhere.example")).ShouldBeTrue();
        LocalCaller.IsForeignPage(Request("127.0.0.1")).ShouldBeFalse();
    }

    static DefaultHttpContext Request(string address, string? origin = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Host = new HostString("127.0.0.1:48117");
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        return context;
    }

    static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    static async Task<RunningApp> Running(string version, bool stops = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var running = new RunningApp(app);

        app.MapGet("/api/version", () => Results.Json(new { version }));
        app.MapPost("/api/app/stop", () =>
        {
            running.StopsAsked++;
            if (stops)
            {
                _ = Task.Run(async () =>
                {
                    // What RunAsync does with StopApplication in the real app; started, this one stops itself.
                    await Task.Delay(200);
                    await app.StopAsync();
                });
            }

            return Results.Json(new { stopping = stops });
        });

        await app.StartAsync();
        running.Origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
        return running;
    }

    sealed class RunningApp(WebApplication app) : IAsyncDisposable
    {
        public string Origin { get; set; } = "";
        public int Port => new Uri(Origin).Port;
        public int StopsAsked { get; set; }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
