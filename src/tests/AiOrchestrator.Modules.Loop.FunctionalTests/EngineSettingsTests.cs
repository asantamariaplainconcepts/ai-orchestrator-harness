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
using Microsoft.Extensions.Time.Testing;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// The real host with its configuration as it ships (<c>Loop:Enabled</c> unset, a pass every 60
/// seconds), two folders of two repositories, and a clock the test moves. The schedule runs; nothing
/// is passed by hand.
/// </summary>
public sealed class EngineSettingsFixture : ApiServiceFixtureBase
{
    public const string Other = "owner/other";

    internal FakeVendor Vendor { get; } = new();

    internal FakeRuns Runs { get; } = new(new FakeScripts());

    internal TwoFolders Folders { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));

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
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

/// <summary>Two checkouts holding this repository's loop: <c>owner/repo</c> and <c>owner/other</c>.</summary>
sealed class TwoFolders : IFolderDirectory
{
    public FakeFolders One { get; } = new();

    public FakeFolders Two { get; } = new();

    public Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoopFolder>>(
            [new LoopFolder(One.Path, "github", FakeFolders.Repository), new LoopFolder(Two.Path, "github", EngineSettingsFixture.Other)]
        );
}

/// <summary>#562. Nobody changed anything: the settings read as configured, and nothing is scheduled.</summary>
public sealed class EngineDefaultsTests(EngineSettingsFixture fixture) : IClassFixture<EngineSettingsFixture>
{
    [Fact]
    public async Task Without_stored_settings_the_configuration_of_today_holds()
    {
        using var client = fixture.CreateClient();

        var settings = await EngineSettings.Get(client, "/api/loop/settings");
        settings.GetProperty("intervalSeconds").GetInt32().ShouldBe(60);
        settings.GetProperty("folders").EnumerateArray().Select(folder => folder.GetProperty("on").GetBoolean()).ShouldBe([false, false]);

        var runs = await EngineSettings.Get(client, "/api/runs/settings");
        runs.GetProperty("staleAfterSeconds").GetInt32().ShouldBe(600);
        runs.GetProperty("permissionMode").GetString().ShouldBe("auto");

        var present = await EngineSettings.Get(client, "/api/loop/present");
        present.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        present.GetProperty("intervalSeconds").GetInt32().ShouldBe(60);
        present.GetProperty("nextPassAt").ValueKind.ShouldBe(JsonValueKind.Null);

        fixture.Clock.Advance(TimeSpan.FromHours(1));
        await Task.Delay(300);
        (await EngineSettings.Get(client, "/api/loop/present")).GetProperty("lastPass").ValueKind.ShouldBe(JsonValueKind.Null);
        fixture.Runs.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_setting_out_of_range_or_a_repository_no_folder_holds_is_refused()
    {
        using var client = fixture.CreateClient();

        using var fast = await client.PutAsJsonAsync("/api/loop/settings", new { intervalSeconds = 1 });
        fast.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var hasty = await client.PutAsJsonAsync("/api/runs/settings", new { staleAfterSeconds = 5 });
        hasty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var reckless = await client.PutAsJsonAsync("/api/runs/settings", new { permissionMode = "anything" });
        reckless.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var nowhere = await client.PutAsJsonAsync("/api/loop/engine", new { repository = "someone/else", on = true });
        nowhere.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

/// <summary>
/// #562. The engine turned on and off per folder from the API: a scheduled pass works the folders
/// that are on and leaves the others alone, their live runs going on, and every switch is a line of
/// the journal.
/// </summary>
public sealed class EngineSwitchTests(EngineSettingsFixture fixture) : IClassFixture<EngineSettingsFixture>
{
    [Fact]
    public async Task A_folder_turned_off_starts_nothing_while_the_other_works_and_its_live_run_goes_on()
    {
        Pbi(FakeFolders.Repository, 1);
        Pbi(EngineSettingsFixture.Other, 7);
        using var client = fixture.CreateClient();

        // owner/repo on: the schedule wakes and passes at once, over it alone.
        var on = await Switch(client, FakeFolders.Repository, true);
        on.GetProperty("written").GetProperty("kind").GetString().ShouldBe("switched");
        var first = await PassAfter(client, null);
        first.GetProperty("folders").GetInt32().ShouldBe(1);
        Started().ShouldBe(["1"]);

        // owner/repo off and owner/other on. A new PBI waits in owner/repo, beside #1's live run.
        await Switch(client, FakeFolders.Repository, false);
        await Switch(client, EngineSettingsFixture.Other, true);
        Pbi(FakeFolders.Repository, 3);

        var present = await EngineSettings.Get(client, "/api/loop/present");
        present.GetProperty("liveRuns").EnumerateArray().Single().GetProperty("pbi").GetString().ShouldBe("1");

        await EngineSettings.Until(client, body => body.GetProperty("nextPassAt").ValueKind == JsonValueKind.String);
        await Task.Delay(200);
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        var second = await PassAfter(client, first.GetProperty("at").GetDateTimeOffset());

        // The pass worked owner/other and nothing of owner/repo: #3 did not start, and nothing was
        // written for #1, whose run is still live.
        second.GetProperty("folders").GetInt32().ShouldBe(1);
        Started().ShouldBe(["1", "7"]);
        fixture.Vendor.Writes.ShouldBeEmpty();
        var live = (await EngineSettings.Get(client, "/api/loop/present")).GetProperty("liveRuns").EnumerateArray()
            .Select(run => run.GetProperty("pbi").GetString()).ToList();
        live.ShouldBe(["1", "7"], ignoreOrder: true);

        // Each switch is a line of its repository, with no PBI.
        var switched = (await EngineSettings.Get(client, "/api/loop/journal")).EnumerateArray()
            .Where(line => line.GetProperty("kind").GetString() == "switched")
            .Select(line => $"{line.GetProperty("repository").GetString()} {line.GetProperty("from").GetString()}→{line.GetProperty("to").GetString()} '{line.GetProperty("pbi").GetString()}'")
            .Reverse()
            .ToList();
        switched.ShouldBe(["owner/repo off→on ''", "owner/repo on→off ''", "owner/other off→on ''"]);

        // Setting what is set writes nothing.
        (await Switch(client, EngineSettingsFixture.Other, true)).GetProperty("written").ValueKind.ShouldBe(JsonValueKind.Null);

        var settings = await EngineSettings.Get(client, "/api/loop/settings");
        settings.GetProperty("folders").EnumerateArray().Select(folder => folder.GetProperty("on").GetBoolean()).ShouldBe([false, true]);
    }

    List<string> Started() => [.. fixture.Runs.Started.Select(run => run.Request.Pbi.Id)];

    void Pbi(string repository, int id) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(repository, $"{id}"), $"PBI {id}", "ready-for-proposal", false, null));

    static async Task<JsonElement> Switch(HttpClient client, string repository, bool on)
    {
        using var response = await client.PutAsJsonAsync("/api/loop/engine", new { repository, on });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    static async Task<JsonElement> PassAfter(HttpClient client, DateTimeOffset? after) =>
        (await EngineSettings.Until(client, body =>
            body.GetProperty("lastPass") is { ValueKind: JsonValueKind.Object } last
            && (after is null || last.GetProperty("at").GetDateTimeOffset() > after)))
        .GetProperty("lastPass");
}

/// <summary>#562. The interval changed by the API counts from the last pass, without a restart.</summary>
public sealed class EngineIntervalTests(EngineSettingsFixture fixture) : IClassFixture<EngineSettingsFixture>
{
    [Fact]
    public async Task A_new_interval_takes_effect_on_the_next_pass()
    {
        using var client = fixture.CreateClient();
        using var on = await client.PutAsJsonAsync("/api/loop/engine", new { repository = FakeFolders.Repository, on = true });
        on.StatusCode.ShouldBe(HttpStatusCode.OK);

        var present = await EngineSettings.Until(client, body => body.GetProperty("lastPass").ValueKind == JsonValueKind.Object
            && body.GetProperty("nextPassAt").ValueKind == JsonValueKind.String);
        var first = present.GetProperty("lastPass").GetProperty("at").GetDateTimeOffset();
        present.GetProperty("nextPassAt").GetDateTimeOffset().ShouldBe(first.AddSeconds(60));

        using var saved = await client.PutAsJsonAsync("/api/loop/settings", new { intervalSeconds = 600 });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("intervalSeconds").GetInt32().ShouldBe(600);

        await EngineSettings.Until(client, body =>
            body.GetProperty("nextPassAt") is { ValueKind: JsonValueKind.String } next && next.GetDateTimeOffset() == first.AddSeconds(600));
        await Task.Delay(200);

        // The old interval goes by without a pass.
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        await Task.Delay(300);
        var quiet = await EngineSettings.Get(client, "/api/loop/present");
        quiet.GetProperty("lastPass").GetProperty("at").GetDateTimeOffset().ShouldBe(first);
        quiet.GetProperty("intervalSeconds").GetInt32().ShouldBe(600);

        // The new one ends in a pass.
        fixture.Clock.Advance(TimeSpan.FromSeconds(540));
        await EngineSettings.Until(client, body => body.GetProperty("lastPass").GetProperty("at").GetDateTimeOffset() == first.AddSeconds(600));
    }

    [Fact]
    public async Task The_stale_threshold_is_stored_and_read_back()
    {
        using var client = fixture.CreateClient();

        using var saved = await client.PutAsJsonAsync("/api/runs/settings", new { staleAfterSeconds = 1200 });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        (await EngineSettings.Get(client, "/api/runs/settings")).GetProperty("staleAfterSeconds").GetInt32().ShouldBe(1200);
    }
}

static class EngineSettings
{
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    public static async Task<JsonElement> Get(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>The engine's present once <paramref name="done"/> holds for it.</summary>
    public static async Task<JsonElement> Until(HttpClient client, Func<JsonElement, bool> done)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        while (true)
        {
            var body = await Get(client, "/api/loop/present");
            if (done(body))
            {
                return body;
            }

            await Task.Delay(50, deadline.Token);
        }
    }
}
