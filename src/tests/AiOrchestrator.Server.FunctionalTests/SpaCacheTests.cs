using System.Net;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// The host with a web root of its own: a temp directory holding an index and one hashed asset.
/// The checkout's <c>wwwroot</c> is the frontend build's output, which <c>pnpm build</c> empties
/// and rewrites while the gate runs these tests (#584); a test that wrote or read there saw
/// whatever the build had left at that instant. Nothing here touches the checkout.
/// </summary>
public sealed class SpaCacheFixture : ApiServiceFixtureBase
{
    public const string AssetName = "index-0123abcd.js";

    public string WebRoot => _webRoot;

    readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"aio-spa-{Guid.NewGuid():N}");

    public SpaCacheFixture()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>fixture</title>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", AssetName), "// the fixture's own asset");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseWebRoot(_webRoot);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SpaCacheCollection : ICollectionFixture<SpaCacheFixture>
{
    public const string Name = "spa-cache";
}

/// <summary>
/// The cache contract a window held open across an install depends on. The document must
/// revalidate, or the pre-update index stays authoritative and its renamed chunks 404; the
/// hashed assets are immutable, because their hash is the freshness. Measured through the
/// host's own pipeline, which is what the installed app runs.
/// </summary>
[Collection(SpaCacheCollection.Name)]
public sealed class SpaCacheTests(SpaCacheFixture host)
{
    HttpClient Client => host.CreateClient();

    static string? CacheControl(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Cache-Control", out var values)
            ? values?.FirstOrDefault()
            : null;

    [Fact]
    public void The_host_serves_the_fixtures_web_root_and_no_dev_server()
    {
        // Without this the three below would pass against a checkout that happens to hold a build,
        // and race the next `pnpm build` again; or measure a Vite dev server named by a variable
        // inherited from the running app, and fail on its headers instead of the host's.
        host.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath.ShouldBe(host.WebRoot);
        host.Services.GetRequiredService<IConfiguration>()["services:frontend:http:0"].ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task The_document_revalidates_on_every_visit()
    {
        var index = await Client.GetAsync("/");
        index.EnsureSuccessStatusCode();
        CacheControl(index).ShouldBe("no-cache");
    }

    [Fact]
    public async Task A_hashed_asset_is_immutable()
    {
        var asset = await Client.GetAsync($"/assets/{SpaCacheFixture.AssetName}");
        asset.EnsureSuccessStatusCode();
        var directive = CacheControl(asset)!.ToLowerInvariant();
        directive.ShouldContain("max-age=31536000");
        directive.ShouldContain("immutable");
    }

    [Fact]
    public async Task An_unknown_asset_gets_no_cache_directive_at_all()
    {
        // The 404 an old chunk receives is empty on purpose — the point is that nothing
        // invents a directive where there is no content to cache.
        var gone = await Client.GetAsync("/assets/was-never-built-here.js");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        CacheControl(gone).ShouldBeNull();
    }
}
