using AiOrchestrator.BuildingBlocks.Folders;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// Three things, each of which is a property of the composition rather than of any one part.
/// <para>
/// <b>Every assertion here is on what a person can see.</b> Not on a response body, not on a store,
/// not on a count from the API — those are covered by the module tests, and a browser is an
/// expensive way to repeat them. Orca's E2E notes are blunt about where that goes wrong: a store
/// round-trip kept passing against a modal that had been deleted, and the tautology is what let a
/// crash ship. So the rule here is the same one: reach the state however you like, then prove it by
/// reading the screen.
/// </para>
/// <para>
/// <b>These used to assert on invented rows</b> — a GitHub issue titled "Story cards drop focus
/// after inline edit" and a Jira key <c>PORT-88</c>, neither of which had ever been at a vendor,
/// both attributed to a directory that was on nobody's disk. J2 deleted them, and what took their
/// place is the row a first boot genuinely has: <b>this checkout</b>, seeded because it is on disk.
/// The assertion is stronger for it — a green run proves that the migration resource, the seed and
/// the screen all agree about a repository that actually exists.
/// </para>
/// <para>
/// Assertions are existence, never totals. The suite runs against its own throwaway database
/// file, but the seed is the product's own and may grow; a test asserting "six tasks" would be
/// asserting on the seed's afternoon.
/// </para>
/// </summary>
[Collection(AppCollection.Name)]
public sealed class AppTests(AppFixture app)
{
    /// <summary>
    /// The checkout this suite is running from, which is the folder a first boot seeds. Asked with
    /// the product's own code rather than recomputed here: two answers to "which repository is
    /// this" is how a test starts passing against something other than the thing it is about.
    /// </summary>
    static string CheckoutPath =>
        SelfCheckout.Root()
        ?? throw new InvalidOperationException(
            "this suite has to run from a git checkout — it asserts on the folder a first boot "
                + "seeds, and outside a repository there correctly is not one"
        );

    /// <summary>The folder's name as the screen prints it: the repository's own directory.</summary>
    static string CheckoutName =>
        Path.GetFileName(CheckoutPath.TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// The composition, end to end: the migration resource ran to completion before the Server
    /// started, the seed is in a real SQLite database, the API answered, the SPA was served from the same
    /// origin, and React rendered a row from that database. Since #565 the row is the folder's name in
    /// the rail, and its page reads the checkout from disk.
    /// </summary>
    [Fact]
    public async Task The_folder_page_shows_the_checkout_this_server_is_running_from()
    {
        var page = await app.Open("/pbis");

        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await rail.GetByRole(AriaRole.Link, new() { Name = CheckoutName, Exact = true })
            .ClickAsync(new() { Timeout = 30_000 });

        // The path on the page, which is the whole claim of the change that put this row here: what
        // the screen names is somewhere that is actually on this disk.
        await page.GetByRole(AriaRole.Main).GetByText(CheckoutPath)
            .First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        // This repository has harness 1 installed, read from its own .aio/version, and the build
        // ships 37: it keeps its own loop.yaml and gate, what its commands add is .aio/context.md (#618),
        // the issue and epic templates are in the package since #633, aio issue since #634,
        // explore cuts an epic since #635, release left the package in #652, and file-overlap names
        // the section as the template does since #655, diff-risk lets a small diff through since #551, and
        // evidence reads .harness/evidence.json since #709, and refine edits the body from --markdown since #710, and the package moved to .harness/aio/ in #729, and the hook refuses what treads on shared work since #731, and diff-risk reads protected-paths since #732, and evidence answers four outcomes since #733, and the package carries .aio/README.md since #742, and the claim says where its committer comes from since #741, and quality reports what a change measures since #746, and the scripts read git in English since #762, and quality shows three more rows since #758, and laya keeps one environment per machine and pin since #757, and the retro asks for its own commit since #765, and laya moved to .harness/aio/scorers/laya/ in #759, and everything moved from .harness/ to .aio/ in #779.
        await page.GetByText("1 installed · the app ships 37", new() { Exact = false })
            .First.WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// <b>Which of the two branches served the document</b>, which is the one thing a green screen
    /// cannot tell you. The other tests here pass identically against a bundle built three hours
    /// ago: <c>Program.cs</c> falls back to <c>wwwroot</c>, the SPA renders, every assertion holds,
    /// and what is on screen is somebody's earlier afternoon. Measured, not feared — a string
    /// changed in <c>en.ts</c>, the Server restarted, <c>GET /</c> answered 200 and the asset it
    /// served did not contain the change.
    /// <para>
    /// So the assertion is on a marker only the dev server produces. Vite injects its own client
    /// into the document it serves; a built bundle has no such script, because in a built bundle
    /// there is nothing to hot-reload. If a Vite upgrade ever renames it this fails loudly, which is
    /// the correct direction for a test whose whole job is to refuse a silent fallback.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_document_comes_from_the_development_server_and_not_from_disk()
    {
        var page = await app.Open("/");

        (await page.ContentAsync()).ShouldContain(
            "/@vite/client",
            Case.Sensitive,
            "the SPA was served from wwwroot rather than proxied to the dev server — every screen "
                + "still renders, from whenever that bundle was built"
        );
    }

    /// <summary>
    /// A client route typed into the address bar: the Server's fallback answers it with the SPA, and
    /// the router draws the screen it names rather than a 404.
    /// </summary>
    [Fact]
    public async Task A_client_route_asked_for_directly_comes_back_as_the_application()
    {
        var page = await app.Open("/loop");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Loop" }).WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Navigation is the router's, not the server's: the URL changes, the header follows it, and the
    /// document is never reloaded. Proved with a marker left on the window before the click — if the
    /// browser fetched a new document, the marker is gone. That is a fact about the browser rather
    /// than about the application's own state, which is the difference between this and asserting
    /// that a store setter works.
    /// </summary>
    /// <remarks>
    /// Folders → Tasks, since #374 removed the History link and its screen: the proof is the
    /// router's, and any two rail destinations carry it as well as the pair this used to click.
    /// </remarks>
    [Fact]
    public async Task Moving_between_screens_is_the_router_and_not_a_page_load()
    {
        var page = await app.Open("/settings");

        await page.GetByText(CheckoutName).First.WaitForAsync(new() { Timeout = 30_000 });

        await page.EvaluateAsync("window.__sameDocument = true");

        await page.GetByRole(AriaRole.Link, new() { Name = "PBIs" })
            .First.ClickAsync(new() { Timeout = 30_000 });

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/pbis$"));
        new Uri(page.Url).Authority.ShouldBe(
            app.Server.Authority,
            "the SPA and the API share an origin, so nothing here has a base URL to get wrong"
        );
        (await page.EvaluateAsync<bool?>("window.__sameDocument")).ShouldBe(true);
    }
}
