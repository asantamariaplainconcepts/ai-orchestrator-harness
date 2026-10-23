using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.Server.Installed;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// The manifest and the worker are source files in the frontend's <c>public</c>, which the build
/// only copies into the Server's <c>wwwroot</c>. Served from there, they are read where nothing
/// empties them: <c>pnpm build</c> rewrites <c>wwwroot</c> while the gate runs (#584), and a checkout
/// that was never built has none.
/// </summary>
public sealed class InstalledAppFixture : ApiServiceFixtureBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseWebRoot(FrontendPublic());
    }

    static string FrontendPublic()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "frontend", "public");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"No src/frontend/public above {AppContext.BaseDirectory}");
    }
}

[CollectionDefinition(Name)]
public sealed class InstalledAppCollection : ICollectionFixture<InstalledAppFixture>
{
    public const string Name = "installed-app";
}

/// <summary>
/// #517. What the one process serves for being installed: the PWA's manifest and service worker,
/// and the update routes. The test host is not the tool, so it checks nothing when it starts (#679)
/// and installs nothing (#703).
/// </summary>
[Collection(InstalledAppCollection.Name)]
public sealed class InstalledAppTests(InstalledAppFixture host)
{
    HttpClient Client => host.CreateClient();

    [Fact]
    public async Task The_manifest_declares_an_app_with_its_own_window()
    {
        var response = await Client.GetAsync("/manifest.webmanifest");
        response.EnsureSuccessStatusCode();

        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;
        root.GetProperty("display").GetString().ShouldBe("standalone");
        root.GetProperty("start_url").GetString().ShouldBe("/");
        root.GetProperty("icons").EnumerateArray().Select(icon => icon.GetProperty("sizes").GetString())
            .ShouldContain("512x512");
    }

    [Fact]
    public async Task The_service_worker_is_served_and_always_revalidated()
    {
        var response = await Client.GetAsync("/sw.js");
        response.EnsureSuccessStatusCode();

        // A worker that a browser kept from an old install would keep the old one alive.
        response.Headers.CacheControl?.NoCache.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldContain("addEventListener(\"install\"");
    }

    [Fact]
    public async Task A_checkout_reads_its_version_and_has_asked_nuget_nothing()
    {
        var status = await Client.GetFromJsonAsync<JsonElement>("/api/updates");

        status.GetProperty("state").GetString().ShouldBe("unchecked");
        status.GetProperty("how").GetString().ShouldBe("checkout");
        status.GetProperty("installed").GetString().ShouldBe(Installation.Version.Split('+')[0]);
    }

    [Fact]
    public async Task A_checkout_installs_no_update_and_says_why()
    {
        var response = await Client.PostAsync("/api/updates/install", null);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Only the installed tool updates itself");
    }

    [Fact]
    public async Task A_checkout_has_no_stop_for_a_newer_launch()
    {
        // #703. /api/app/stop is mapped in the installed tool only.
        (await Client.PostAsync("/api/app/stop", null)).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_checkout_reads_start_at_login_and_cannot_change_it()
    {
        var state = await Client.GetFromJsonAsync<JsonElement>("/api/app/service");
        state.GetProperty("how").GetString().ShouldBe("checkout");

        var change = await Client.PutAsJsonAsync("/api/app/service", new { registered = true });
        change.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }
}
