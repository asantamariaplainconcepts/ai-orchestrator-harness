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

/// <summary>The real host, with the vendor, the scripts, Runs and the folders answered from memory.</summary>
public sealed class LoopFixture : ApiServiceFixtureBase
{
    internal FakeVendor Vendor { get; } = new();

    internal FakeScripts Scripts { get; } = new();

    internal FakeRuns Runs { get; }

    public LoopFixture() => Runs = new FakeRuns(Scripts);

    internal FakeFolders Folders { get; } = new();

    internal FakeAgent Agent { get; } = new();

    /// <summary>Where the engine keeps each script's output (#638), its own and not beside the test database.</summary>
    public string ScriptsDirectory { get; } = Directory.CreateTempSubdirectory("aio-loop-scripts-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Loop:ScriptsDirectory", ScriptsDirectory);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPbiProvider>();
            services.AddSingleton<IPbiProvider>(Vendor);
            services.RemoveAll<IRunRequests>();
            services.AddSingleton<IRunRequests>(Runs);
            services.RemoveAll<IFolderDirectory>();
            services.AddSingleton<IFolderDirectory>(Folders);
            services.RemoveAll<IAgentProvider>();
            services.AddSingleton<IAgentProvider>(Agent);
        });
    }
}

/// <summary>
/// #505. Passes of the engine through the API, against a vendor, scripts and Runs held in memory:
/// what each pass did to the vendor, and the journal read back with a reason on every line.
/// </summary>
public sealed class RunOnceTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    const string Me = FakeVendor.Me;

    [Fact]
    public async Task Three_passes_move_hold_wait_and_explain_every_decision()
    {
        Pbi(1, "ready-for-proposal", null);
        Pbi(2, "in-progress", "someone");
        Pbi(3, "verifying", Me);
        Pbi(4, "spec-ready", null);
        Pbi(6, "merging", Me);
        fixture.Vendor.Pulls["3"] = new PullRequestInfo("13", new Uri("https://github.com/owner/repo/pull/13"), true, false, false);
        fixture.Vendor.Pulls["6"] = new PullRequestInfo("16", new Uri("https://github.com/owner/repo/pull/16"), false, false, true);
        fixture.Scripts.Answers["gate"] = (0, """{"outcome": "green", "why": "#3 may start: not held, and 0 of 4 in progress."}""");
        fixture.Scripts.Answers["evidence"] = (0, """{"step": "evidence", "published": false, "why": "The diff does not touch src/frontend/."}""");
        fixture.Scripts.Answers["archive"] = (0, """{"ok": true, "archived": null, "open": [], "observed": false, "why": "nothing to archive."}""");
        fixture.Scripts.Answers["merge"] = (0, """{"outcome": "merged", "observed": true, "why": "Pull request 16 reads back as merged."}""");
        fixture.Runs.Elsewhere = 3;

        using var client = fixture.CreateClient();

        // Pass 1. #1 takes the last place under the WIP; #4 waits for one. #2 is someone else's.
        var first = await RunOnce(client);
        first.GetProperty("folders").GetInt32().ShouldBe(1);
        first.GetProperty("pbis").GetInt32().ShouldBe(5);

        var asked = fixture.Runs.Started.Single().Request;
        asked.Outcomes.ShouldBe(["ready", "gaps"]);
        (asked with { Outcomes = asked.Outcomes }).ShouldBe(
            new RunRequest(
                new PbiRef(FakeFolders.Repository, "1"),
                "ready-for-proposal",
                fixture.Folders.Path,
                "change/1",
                "claude-code",
                "claude-opus-5-5",
                "/aio:refine 1",
                asked.Outcomes
            )
        );
        fixture.Vendor.Writes.ShouldBe(["ready #3", "move #3 code-review", "close #6", "move #6 done"]);
        fixture.Scripts.Ran.Select(run => $"{run.Script} {string.Join(' ', run.Arguments)}")
            .ShouldBe(["gate 3", "evidence 3", "quality 3", "archive ", "merge 16"]);
        // #507: on the PBI's branch, prepared from the folder, and never in the folder's checkout.
        fixture.Scripts.Ran.ShouldAllBe(run => run.FolderPath == fixture.Folders.Path && run.Branch == $"change/{run.Pbi.Id}");

        // Pass 2. Nothing has changed for #4, so its wait is not written twice.
        await RunOnce(client);

        // The run ends; pass 3 moves #1 where its outcome leads.
        fixture.Runs.End(0, "ready", "The issue meets the Definition of Ready.");
        await RunOnce(client);

        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "1").State.ShouldBe("spec-ready");
        fixture.Vendor.Writes.ShouldNotContain(write => write.EndsWith("#2", StringComparison.Ordinal) || write.Contains("#2 ", StringComparison.Ordinal));

        var journal = await Journal(client, "");
        journal.ShouldAllBe(line => line.Why.Length > 0);
        journal.Select(line => line.Pbi).ShouldNotContain("2");

        Lines(journal, "1").ShouldBe([
            "started ready-for-proposal → ready-for-proposal",
            "waited ready-for-proposal",
            "moved ready-for-proposal → spec-ready",
        ]);
        // #597. Each script's start is a line before it runs, and #593's ran line follows it.
        Lines(journal, "3")[..8].ShouldBe([
            "started verifying → verifying",
            "ran verifying",
            "started verifying → verifying",
            "ran verifying",
            "started verifying → verifying",
            "ran verifying",
            "moved verifying → code-review",
            "waited code-review",
        ]);
        Lines(journal, "4").ShouldBe(["waited spec-ready"]);
        Lines(journal, "6").ShouldBe(["started merging → merging", "ran merging", "started merging → merging", "ran merging", "moved merging → done"]);

        // #593: a script that passes leaves its why too, not only the one that decides.
        journal.Where(line => line.Pbi == "3" && line.Kind == "ran").Select(line => line.Why).ShouldBe([
            "gate: #3 may start: not held, and 0 of 4 in progress.",
            "evidence: The diff does not touch src/frontend/.",
            "quality: Nothing published: gh could not comment.",
        ]);
        journal.First(line => line.Pbi == "6" && line.Kind == "ran").Why.ShouldBe("archive: nothing to archive.");

        journal.Single(line => line.Pbi == "4").Why.ShouldBe("WIP 4 of 4");
        journal.Single(line => line.Pbi == "3" && line.Kind == "moved").Why
            .ShouldBe("green: #3 may start: not held, and 0 of 4 in progress.");
        journal.Last(line => line.Pbi == "1").Why.ShouldBe("ready: The issue meets the Definition of Ready.");

        var one = await Journal(client, "?repository=owner/repo&pbi=1&limit=1");
        one.Single().Kind.ShouldBe("moved");
    }

    [Fact]
    public async Task A_PBI_named_without_its_repository_is_refused()
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/api/loop/journal?pbi=1");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    sealed record Line(DateTimeOffset At, string Repository, string Pbi, string From, string Kind, string? To, string Why);

    void Pbi(int id, string state, string? assignee) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, false, assignee));

    static async Task<JsonElement> RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>The journal oldest first, as the assertions read it; the API answers newest first.</summary>
    static async Task<List<Line>> Journal(HttpClient client, string query)
    {
        var lines = await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal{query}");
        lines.ShouldNotBeNull().Reverse();
        return lines;
    }

    static string[] Lines(List<Line> journal, string pbi) =>
        [.. journal.Where(line => line.Pbi == pbi).Select(line => line.To is null ? $"{line.Kind} {line.From}" : $"{line.Kind} {line.From} → {line.To}")];
}
