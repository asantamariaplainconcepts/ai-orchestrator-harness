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
/// The real host with the engine on its own, a pass every second, against the same vendor, scripts
/// and Runs held in memory as <see cref="LoopFixture"/>. What the vendor holds is set before the host
/// starts, because the schedule reads it from its first second.
/// </summary>
public sealed class ScheduledLoopFixture : ApiServiceFixtureBase
{
    internal FakeVendor Vendor { get; } = new();

    internal FakeRuns Runs { get; } = new(new FakeScripts());

    internal FakeFolders Folders { get; } = new();

    public ScheduledLoopFixture()
    {
        Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "1"), "PBI 1", "ready-for-proposal", false, null));
        Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "5"), "PBI 5", "proposal-review", true, FakeVendor.Me));
        Vendor.Pulls["5"] = new PullRequestInfo("15", new Uri("https://github.com/owner/repo/pull/15"), true, false, false);
        Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "2"), "PBI 2", "done", false, null, Closed: true));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Loop:Enabled", "true");
        builder.UseSetting("Loop:IntervalSeconds", "1");

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

/// <summary>
/// #508. With the engine running on its schedule, the routes answer and the journal it writes is
/// read through the API. That nothing ticks by default is EngineDefaultsTests' (#668).
/// </summary>
public sealed class ScheduleTests(ScheduledLoopFixture fixture) : IClassFixture<ScheduledLoopFixture>
{
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task The_engine_runs_on_its_own_and_the_routes_read_what_it_did()
    {
        using var client = fixture.CreateClient();

        // The strip: the engine says it is on, how often it looks, and the pass it made. The wait is
        // for every signal read below, not for an instant: the pass is written down before the
        // schedule sets the next one (#584), and under the gate's load the strip was read with the
        // pass written and the live run not yet listed (#668).
        var present = await Until(client, "/api/loop/present", body =>
            body.GetProperty("lastPass") is { ValueKind: JsonValueKind.Object } last
            && last.GetProperty("failure").ValueKind == JsonValueKind.Null
            && body.GetProperty("lastDecision").ValueKind == JsonValueKind.Object
            && body.GetProperty("nextPassAt").ValueKind == JsonValueKind.String
            && body.GetProperty("liveRuns").GetArrayLength() > 0);

        present.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        present.GetProperty("intervalSeconds").GetInt32().ShouldBe(1);
        present.GetProperty("nextPassAt").ValueKind.ShouldBe(JsonValueKind.String);
        present.GetProperty("lastPass").GetProperty("folders").GetInt32().ShouldBe(1);
        present.GetProperty("lastPass").GetProperty("pbis").GetInt32().ShouldBe(2);
        var folder = present.GetProperty("folders").EnumerateArray().Single();
        folder.GetProperty("repository").GetString().ShouldBe(FakeFolders.Repository);
        folder.GetProperty("wip").GetInt32().ShouldBe(4);
        folder.GetProperty("problem").ValueKind.ShouldBe(JsonValueKind.Null);
        present.GetProperty("liveRuns").EnumerateArray().Single().GetProperty("pbi").GetString().ShouldBe("1");

        // Nobody ran a pass by hand: the run #1 asked for came from the schedule.
        fixture.Runs.Started.ShouldContain(run => run.Request.Pbi.Id == "1");

        // The journal it wrote, read back whole and narrowed to one PBI.
        await Until(client, "/api/loop/journal", journal => journal.EnumerateArray().Any(line =>
            line.GetProperty("pbi").GetString() == "1" && line.GetProperty("kind").GetString() == "started"));
        var one = await Get(client, "/api/loop/journal?repository=owner/repo&pbi=1");
        one.EnumerateArray().ShouldAllBe(line => line.GetProperty("pbi").GetString() == "1");

        // The PBIs, with their state and hold, read from the vendor now.
        var pbis = await Get(client, "/api/pbis");
        pbis.GetProperty("unread").GetArrayLength().ShouldBe(0);
        var held = pbis.GetProperty("pbis").EnumerateArray().Single(pbi => pbi.GetProperty("id").GetString() == "5");
        held.GetProperty("state").GetString().ShouldBe("proposal-review");
        held.GetProperty("held").GetBoolean().ShouldBeTrue();
        held.GetProperty("folderPath").GetString().ShouldBe(fixture.Folders.Path);
        pbis.GetProperty("pbis").EnumerateArray().ShouldNotContain(open => open.GetProperty("id").GetString() == "2");

        // #515. The closed ones are a list of their own, behind the PBIs screen's Done filter.
        var done = (await Get(client, "/api/pbis/done")).GetProperty("pbis").EnumerateArray().Single();
        done.GetProperty("id").GetString().ShouldBe("2");
        done.GetProperty("closed").GetBoolean().ShouldBeTrue();

        // One PBI with its pull request; its runs are Runs', and it has none.
        var pbi = await Get(client, "/api/pbi?repository=owner/repo&id=5");
        pbi.GetProperty("pullRequest").GetProperty("id").GetString().ShouldBe("15");
        (await Get(client, "/api/runs?repository=owner/repo&pbi=5")).GetArrayLength().ShouldBe(0);
        (await Get(client, "/api/sessions")).GetArrayLength().ShouldBe(0);
        (await Get(client, "/api/machine")).GetProperty("agents").EnumerateArray().Select(agent => agent.GetString())
            .ShouldContain("claude-code");

        using var missing = await client.GetAsync("/api/pbi?repository=owner/repo&id=404");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var elsewhere = await client.GetAsync("/api/pbi?repository=someone/else&id=1");
        elsewhere.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    static async Task<JsonElement> Get(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    static async Task<JsonElement> Until(HttpClient client, string route, Func<JsonElement, bool> done)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        while (true)
        {
            var body = await Get(client, route);
            if (done(body))
            {
                return body;
            }

            await Task.Delay(200, deadline.Token);
        }
    }
}
