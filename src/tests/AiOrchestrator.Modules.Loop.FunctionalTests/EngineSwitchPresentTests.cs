using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// The real host as it ships, with two folders: <c>owner/repo</c> holds this repository's loop and
/// <c>owner/bare</c> declares none. Nothing is scheduled until a switch is on.
/// </summary>
public sealed class EngineSwitchPresentFixture : ApiServiceFixtureBase
{
    public const string Bare = "owner/bare";

    internal FakeVendor Vendor { get; } = new();

    internal FakeRuns Runs { get; } = new(new FakeScripts());

    internal LoopAndBare Folders { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPbiProvider>();
            services.AddSingleton<IPbiProvider>(Vendor);
            services.RemoveAll<IRunRequests>();
            services.AddSingleton<IRunRequests>(Runs);
            services.RemoveAll<IFolderDirectory>();
            services.AddSingleton<IFolderDirectory>(Folders);
            services.RemoveAll<IAgentProvider>();
            services.AddSingleton<IAgentProvider>(new FakeAgent());
        });
    }
}

/// <summary>One checkout with a loop and one with an empty directory where the loop would be.</summary>
sealed class LoopAndBare : IFolderDirectory
{
    public FakeFolders WithLoop { get; } = new();

    public string BarePath { get; } = Directory.CreateTempSubdirectory("aio-bare-").FullName;

    public Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoopFolder>>(
            [new LoopFolder(WithLoop.Path, "github", FakeFolders.Repository), new LoopFolder(BarePath, "github", EngineSwitchPresentFixture.Bare)]
        );
}

/// <summary>
/// #564. The engine's present lists each folder with its switch and whether its loop can be read,
/// which is what the Loop strip draws; a switch changes the next read, and a folder without a loop
/// cannot be turned on.
/// </summary>
public sealed class EngineSwitchPresentTests(EngineSwitchPresentFixture fixture) : IClassFixture<EngineSwitchPresentFixture>
{
    [Fact]
    public async Task A_switch_changes_the_next_read_and_a_folder_without_a_loop_stays_off_with_its_reason()
    {
        using var client = fixture.CreateClient();

        Folders(await Present(client)).ShouldBe(["owner/repo off loop", "owner/bare off No loop declared. Add .aio/loop.yaml to the repository."]);

        (await Status(client, FakeFolders.Repository, true)).ShouldBe(HttpStatusCode.OK);
        var on = await Present(client);
        Folders(on).ShouldBe(["owner/repo on loop", "owner/bare off No loop declared. Add .aio/loop.yaml to the repository."]);
        on.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        on.GetProperty("lastDecision").GetProperty("kind").GetString().ShouldBe("switched");

        // Refused with the reason, and nothing changed: no switch stored, no journal line.
        using var refused = await Switch(client, EngineSwitchPresentFixture.Bare, true);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().ShouldBe(
            "The engine cannot be turned on for owner/bare: No loop declared. Add .aio/loop.yaml to the repository."
        );
        var after = await Present(client);
        Folders(after).ShouldBe(["owner/repo on loop", "owner/bare off No loop declared. Add .aio/loop.yaml to the repository."]);
        after.GetProperty("lastDecision").GetProperty("repository").GetString().ShouldBe(FakeFolders.Repository);

        // Off is always taken, loop or not.
        (await Status(client, EngineSwitchPresentFixture.Bare, false)).ShouldBe(HttpStatusCode.OK);
        (await Status(client, FakeFolders.Repository, false)).ShouldBe(HttpStatusCode.OK);
        var off = await Present(client);
        Folders(off).ShouldBe(["owner/repo off loop", "owner/bare off No loop declared. Add .aio/loop.yaml to the repository."]);
        off.GetProperty("enabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_pass_lists_the_folder_without_a_loop_under_skipped_with_its_reason_and_walks_the_other()
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "8"), "PBI 8", "in-progress", false, "someone"));
        using var client = fixture.CreateClient();

        using var response = await client.PostAsync("/api/loop/run-once", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var pass = await response.Content.ReadFromJsonAsync<JsonElement>();
        pass.GetProperty("skipped").EnumerateArray().Select(line => line.GetString()).ShouldBe(
            [$"{fixture.Folders.BarePath}: No loop declared. Add .aio/loop.yaml to the repository."]
        );
        pass.GetProperty("folders").GetInt32().ShouldBe(1);
        pass.GetProperty("pbis").GetInt32().ShouldBe(1);
    }

    static List<string> Folders(JsonElement present) =>
    [
        .. present.GetProperty("folders").EnumerateArray().Select(folder =>
            $"{folder.GetProperty("repository").GetString()} {(folder.GetProperty("on").GetBoolean() ? "on" : "off")} {folder.GetProperty("problem").GetString() ?? "loop"}"
        ),
    ];

    static async Task<JsonElement> Present(HttpClient client)
    {
        using var response = await client.GetAsync("/api/loop/present");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    static async Task<HttpStatusCode> Status(HttpClient client, string repository, bool on)
    {
        using var response = await Switch(client, repository, on);
        return response.StatusCode;
    }

    static Task<HttpResponseMessage> Switch(HttpClient client, string repository, bool on) =>
        client.PutAsJsonAsync("/api/loop/engine", new { repository, on });
}

/// <summary>
/// The PBIs route over the same two folders, with the vendor of <c>owner/bare</c> failing as
/// <c>gh</c> does when it cannot reach GitHub: the other folder is still listed.
/// </summary>
public sealed class UnreachableVendorTests(EngineSwitchPresentFixture fixture) : IClassFixture<EngineSwitchPresentFixture>
{
    [Fact]
    public async Task A_folder_whose_vendor_fails_is_named_under_unread_and_the_other_is_listed()
    {
        const string said = "error connecting to api.github.com\ncheck your internet connection or https://githubstatus.com";
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "9"), "PBI 9", "ready-for-proposal", false, null));
        fixture.Vendor.Unreachable[EngineSwitchPresentFixture.Bare] = said;
        using var client = fixture.CreateClient();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/pbis");

        list.GetProperty("pbis").EnumerateArray().Select(pbi => $"{pbi.GetProperty("repository").GetString()}#{pbi.GetProperty("id").GetString()}")
            .ShouldBe(["owner/repo#9"]);
        var unread = list.GetProperty("unread").EnumerateArray().Single();
        unread.GetProperty("repository").GetString().ShouldBe(EngineSwitchPresentFixture.Bare);
        unread.GetProperty("folderPath").GetString().ShouldBe(fixture.Folders.BarePath);
        unread.GetProperty("why").GetString().ShouldBe(said);
    }
}
