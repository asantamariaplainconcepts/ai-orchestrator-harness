using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>A host of its own: one of these tests stops it.</summary>
public sealed class ConsoleFixture : ApiServiceFixtureBase;

[CollectionDefinition(Name)]
public sealed class ConsoleCollection : ICollectionFixture<ConsoleFixture>
{
    public const string Name = "console";
}

/// <summary>
/// #548. An /api route nobody mapped answers 404 in both habitats, and Settings can stop the
/// process.
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class ConsoleTests(ConsoleFixture host)
{
    [Fact]
    public async Task An_unknown_api_route_is_a_404_and_not_the_spa()
    {
        using var client = host.CreateClient();

        var response = await client.GetAsync("/api/no-existe");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("<html", Case.Insensitive);
    }

    /// <summary>
    /// In development the Server hands every request it has no endpoint for to the Vite dev server,
    /// and Vite's proxy sends <c>/api</c> back. The frontend here is a port nobody listens on: before
    /// the fallback, the request went there and came back as a proxy failure instead of a 404.
    /// </summary>
    [Fact]
    public async Task In_development_an_unknown_api_route_is_answered_here_and_never_proxied()
    {
        using var development = host.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").UseSetting("services:frontend:http:0", "http://127.0.0.1:9")
        );
        using var client = development.CreateClient();

        var unknown = await client.GetAsync("/api/no-existe");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // A mapped route still answers, so the fallback shadows nothing.
        (await client.GetAsync("/api/version")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The installed-app spec's guard: with <c>Identity:Reach</c> at its default, a host told to listen
    /// on a routable address refuses to start and says why.
    /// </summary>
    [Fact]
    public void A_host_told_to_listen_on_a_public_address_does_not_start_and_says_why()
    {
        using var exposed = host.WithWebHostBuilder(builder => builder.UseSetting("ASPNETCORE_URLS", "http://192.168.1.10:5080"));

        var refusal = Should.Throw<InvalidOperationException>(() => exposed.CreateClient().Dispose());

        refusal.Message.ShouldStartWith("This host is not a machine one person owns: it listens on 192.168.1.10, which is not loopback.");
    }

    [Fact]
    public async Task Stop_answers_first_and_then_stops_the_process()
    {
        using var stoppable = host.WithWebHostBuilder(_ => { });
        using var client = stoppable.CreateClient();
        var lifetime = stoppable.Services.GetRequiredService<IHostApplicationLifetime>();
        var stopping = new TaskCompletionSource();
        lifetime.ApplicationStopping.Register(() => stopping.TrySetResult());

        var response = await client.PostAsync("/api/console/stop", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stopping").GetBoolean().ShouldBeTrue();
        await stopping.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
