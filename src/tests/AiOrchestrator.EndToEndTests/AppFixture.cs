using System.Runtime.CompilerServices;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The whole product, started the way a person starts it, with a real browser pointed at it.
/// <para>
/// <b>What this covers that nothing else can.</b> The functional tests drive the Server through
/// <c>WebApplicationFactory</c>, which skips everything the AppHost does: that migrations are a
/// resource the Server waits for rather than something it does itself, that the SPA is served from
/// the same origin as the API so there is no base URL and no CORS, and that a client-side route
/// typed into the address bar comes back as the application rather than a 404. Each of those is a
/// composition, and a composition has no unit.
/// </para>
/// <para>
/// One AppHost for the whole collection. It runs the migration resource to completion against a
/// throwaway SQLite file, starts the Server and the Vite dev server, and that is most of a minute —
/// per test it would be most of an afternoon.
/// </para>
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    /// <summary>
    /// Generous, and deliberately so. A cold start runs migrations and compiles the SPA; a budget
    /// tuned to a warm machine is a test that fails on somebody's first morning and teaches them
    /// to distrust the suite.
    /// </summary>
    static readonly TimeSpan StartUpBudget = TimeSpan.FromMinutes(5);

    readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"aio-e2e-{Guid.NewGuid():N}.db"
    );

    /// <summary>
    /// Where every test leaves what it looked like: <c>videos/</c> and <c>screenshots/</c>, one of
    /// each per test, named after it. Under <c>src/artifacts/</c> beside the coverage the gate
    /// writes — a reviewer who can watch the screen a test drove does not have to take the
    /// assertion's word for it.
    /// </summary>
    public static readonly string Media = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "playwright")
    );

    /// <summary>Every page a test opened, with the name its video and screenshot are filed under.</summary>
    readonly List<(IPage Page, string Name)> _opened = [];

    /// <summary>The AppHost's name for the API host, which is also where the SPA is served from.</summary>
    const string ServerResource = "server";

    DistributedApplication? _app;
    IPlaywright? _playwright;
    IBrowser? _browser;

    /// <summary>Where the Server ended up. Aspire picks the port, so nothing here is hard-coded.</summary>
    public Uri Server { get; private set; } = new("http://localhost");

    public async Task InitializeAsync()
    {
        EnsureTheFrontendCanStart();

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AiOrchestrator_AppHost>();

        // Nothing here names the environment any more, and the empty space is the point. This is
        // where `WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")` used to be, under a
        // comment reporting the exact failure #61 was later filed for: the testing builder supplies
        // no launch profile, so the Server took its other branch and answered 404 to every route
        // while the API answered 200. The finding was right and it was written down in the wrong
        // place — a suite that patches the composition it is testing is a suite that cannot see the
        // defect, which is how `aspire do ci` counted 259 green against a server nobody could open.
        //
        // `AppHost.cs` declares it on the resource now, so this fixture gets it the same way
        // `aspire start --isolated` does. That makes the tests below the regression net for it: take
        // that line out of the AppHost and they fail here, in a browser, on a 404.
        DetachTheDevelopersDatabase(builder);

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var startUp = new CancellationTokenSource(StartUpBudget);

        // The dev server first: the Server proxies to it, so a browser arriving before it is up is
        // a 502 that reads like a bug in the SPA.
        await _app.ResourceNotifications.WaitForResourceAsync(
            "frontend",
            KnownResourceStates.Running,
            startUp.Token
        );

        // Healthy, not merely running: the Server's health checks include a DbContext check per
        // module, so this is also the assertion that every module reached the database the
        // migration resource had just finished with.
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(ServerResource, startUp.Token);

        Server = _app.GetEndpoint(ServerResource, "http");

        // Idempotent, and here rather than in a README because a prerequisite somebody has to
        // remember is a prerequisite that fails for the next person instead.
        // Qualified: the AppHost project reference brings its own Program into scope here.
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        exitCode.ShouldBe(0, "Playwright could not install its browser");

        // Owned by this run: media from an earlier one would be filed as if these tests made it.
        if (Directory.Exists(Media))
        {
            Directory.Delete(Media, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(Media, "screenshots"));

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    /// <summary>
    /// The width a phone actually is, which until #217 no test here had ever asked for.
    /// <para>
    /// 390×844 is an iPhone 14's CSS viewport, and it is the size the defects were measured at: five
    /// merge-gate cells whose ten values all came out elided, a header spending its width on icons
    /// and a folder name left at two characters. The number is here rather than in each test so that
    /// "phone width" means one thing across the file.
    /// </para>
    /// </summary>
    public static ViewportSize Phone => new() { Width = 390, Height = 844 };

    /// <summary>
    /// A fresh context per test: no cookies, no local storage, nothing one test left behind.
    /// <para>
    /// <b>The viewport is asked for, and asking for nothing is Playwright's own 1280×720.</b> This
    /// used to be <c>NewContextAsync()</c> with no options at all, so every test in this suite ran at
    /// that one width and no test existed below it — which is why a layout that only fits a desktop
    /// could not fail here (#217). The default is kept deliberately: the four tests that predate this
    /// parameter are about composition rather than about width, and moving them would change what
    /// they cover.
    /// </para>
    /// </summary>
    /// <param name="test">The test asking, filled in by the compiler: it names the media.</param>
    /// <param name="file">The file the test is in, filled in by the compiler for the same reason.</param>
    public async Task<IPage> Open(
        string path = "/",
        ViewportSize? viewport = null,
        [CallerMemberName] string test = "",
        [CallerFilePath] string file = ""
    )
    {
        // A default-constructed options object is what passing none means, so the wide path is
        // untouched rather than re-specified — a hard-coded 1280×720 here would be this file's own
        // opinion about what Playwright's default is, and wrong the day it changes.
        // Recorded always, not on failure: the video is evidence of what passed as much as of what
        // broke, and Playwright only knows the outcome after the file is already written.
        var options = new BrowserNewContextOptions { RecordVideoDir = Path.Combine(Media, "videos") };
        if (viewport is not null)
        {
            options.ViewportSize = viewport;
        }

        var context = await _browser!.NewContextAsync(options);
        var page = await context.NewPageAsync();
        lock (_opened)
        {
            var name = $"{Path.GetFileNameWithoutExtension(file)}.{test}";
            var taken = _opened.Count(opened => opened.Name.StartsWith(name, StringComparison.Ordinal));
            _opened.Add((page, taken == 0 ? name : $"{name}.{taken + 1}"));
        }

        var response = await page.GotoAsync(new Uri(Server, path).ToString());

        // The first thing to fail if the SPA is not being served at all, and it says so here rather
        // than as an identical timeout in every spec. Orca's own E2E notes make this point about
        // their build step: when every test fails on the same line, suspect what serves the app.
        response.ShouldNotBeNull($"no response at all from {path}");
        response!.Status.ShouldBeLessThan(
            400,
            $"{path} answered {response.Status} — the SPA fallback is what serves a client route"
        );

        return page;
    }

    /// <summary>
    /// Points the migration resource and the Server at a throwaway SQLite file, so the suite gets
    /// an empty database of its own and touches nothing a developer is using.
    /// <para>
    /// <b>This is not tidiness, it is the bug that found it.</b> When the database was a Postgres
    /// container with a data volume, running the suite once attached the checkout's own database
    /// and left <c>aio-postgres-data</c> unable to start at all — <i>PANIC: could not locate a
    /// valid checkpoint record</i> — which was somebody's working database, broken by running the
    /// tests. The database is a file now (ADR-0003) and the AppHost derives its path from the
    /// checkout, so the suite would otherwise share the developer's file the same way; overriding
    /// the connection string on both resources is the whole detachment, and there is no container
    /// lifetime left to argue with.
    /// </para>
    /// <para>
    /// The empty database is better coverage, not a compromise. It is the fresh-clone path the
    /// AppHost's own comment is about — migrations are a resource the Server waits for, so a clone
    /// with no database file boots with an up-to-date schema. Sharing one tests that on exactly
    /// the one morning somebody deletes it.
    /// </para>
    /// </summary>
    void DetachTheDevelopersDatabase(IDistributedApplicationTestingBuilder builder)
    {
        var throwaway = $"Data Source={_databasePath}";

        foreach (var name in new[] { "migrations", ServerResource })
        {
            var project = builder
                .Resources.OfType<ProjectResource>()
                .Single(resource => resource.Name == name);

            // Applied after the AppHost's own WithEnvironment, so this value is the one that
            // wins — environment callbacks are folded into one dictionary in declaration order.
            builder
                .CreateResourceBuilder(project)
                .WithEnvironment("ConnectionStrings__aiorchestratordb", throwaway);
        }
    }

    /// <summary>
    /// The last frame of every test as a screenshot, then its context closed — which is what
    /// finishes the video file — and the video renamed from Playwright's hash to the test's name.
    /// Here rather than per test because the fixture is the one thing every test hands its page to;
    /// the page is exactly as the test left it.
    /// </summary>
    async Task KeepTheMedia()
    {
        foreach (var (page, name) in _opened)
        {
            try
            {
                await page.ScreenshotAsync(new() { Path = Path.Combine(Media, "screenshots", $"{name}.png"), FullPage = true });
                await page.Context.CloseAsync();
                if (page.Video is { } video && await video.PathAsync() is { } recorded && File.Exists(recorded))
                {
                    File.Move(recorded, Path.Combine(Media, "videos", $"{name}.webm"), overwrite: true);
                }
            }
            catch (PlaywrightException)
            {
                // A page the test already closed, or a browser that went away: that test's media is
                // lost, and nobody else's should go with it.
            }
        }
    }

    /// <summary>
    /// The frontend is an Aspire resource running <c>pnpm dev</c>, and a missing node_modules makes
    /// it fail in a log nobody is reading. Said plainly, once, before anything starts.
    /// </summary>
    static void EnsureTheFrontendCanStart()
    {
        var frontend = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend")
        );

        Directory.Exists(frontend).ShouldBeTrue($"expected the SPA at {frontend}");
        Directory
            .Exists(Path.Combine(frontend, "node_modules"))
            .ShouldBeTrue(
                $"the SPA's dependencies are not installed — run `pnpm install` in {frontend}. "
                    + "The AppHost starts the Vite dev server as a resource, and without them it "
                    + "exits and every test here fails on the same line."
            );
    }

    public async Task DisposeAsync()
    {
        await KeepTheMedia();

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        foreach (
            var file in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" }
        )
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
