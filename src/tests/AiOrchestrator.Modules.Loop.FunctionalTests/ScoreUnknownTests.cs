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

/// <summary>The loop's host as <see cref="LoopFixture"/> builds it, and a folder whose harness has a <c>laya</c>.</summary>
public sealed class ScoreFixture : ApiServiceFixtureBase
{
    internal FakeVendor Vendor { get; } = new();

    internal FakeScripts Scripts { get; } = new();

    internal FakeRuns Runs { get; }

    internal FakeFolders Folders { get; } = new();

    internal FakeHarness Harness { get; } = new();

    public ScoreFixture()
    {
        Runs = new FakeRuns(Scripts);
        Directory.CreateDirectory(Path.Combine(Folders.Path, ".aio", "scorers", "laya"));
        File.WriteAllText(Path.Combine(Folders.Path, ".aio", "scorers", "laya", "laya"), "#!/bin/sh\n");
    }

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
            services.RemoveAll<IScriptRunner>();
            services.AddSingleton<IScriptRunner>(Harness);
        });
    }
}

/// <summary>The folder's own scripts, run outside a PBI's place: only <c>laya ensure</c>, which fails here.</summary>
sealed class FakeHarness : IScriptRunner
{
    public List<ScriptRequest> Ran { get; } = [];

    public Task<ScriptResult> RunAsync(ScriptRequest request, CancellationToken ct)
    {
        Ran.Add(request);
        return Task.FromResult(new ScriptResult(1, null, "laya: no Python 3.10+ on PATH"));
    }
}

/// <summary>
/// #596. A score that could not be made answers <c>unknown</c>: the state's <c>on</c> takes it where it
/// says, and a state that does not name it holds with the score's own why rather than reading it as
/// high. Before the first score of a pass, the engine runs <c>laya ensure</c> once in the folder.
/// </summary>
public sealed class ScoreUnknownTests(ScoreFixture fixture) : IClassFixture<ScoreFixture>
{
    const string Unscored = "risk-score: no se pudo puntuar: no laya server on http://127.0.0.1:8317";

    [Fact]
    public async Task Unknown_holds_with_the_score_why_unless_the_state_names_it_and_laya_ensure_runs_once_a_pass()
    {
        fixture.Scripts.Answers["risk-score"] = (0, $$"""{"band": "unknown", "outcome": "unknown", "why": "{{Unscored}}"}""");
        Pbi(1);
        Pbi(3);

        using var client = fixture.CreateClient();

        // Pass 1, with this repository's loop: proposal-review does not name unknown.
        await RunOnce(client);

        fixture.Harness.Ran.Select(run => $"{run.WorkingDirectory}: {run.Script} {string.Join(' ', run.Arguments)}")
            .ShouldBe([$"{fixture.Folders.Path}: laya ensure"], "once a pass, in the folder's checkout");
        fixture.Scripts.Ran.Select(run => $"{run.Script} {run.Pbi.Id}").ShouldBe(["risk-score 1", "risk-score 3"]);
        fixture.Vendor.Writes.ShouldBe(["hold #1", "hold #3"]);

        var held = (await Journal(client, "?repository=owner/repo&pbi=1")).Single(line => line.Kind == "held");
        held.Why.ShouldContain($"proposal-review produced unknown, which its on does not name: {Unscored}");
        held.Why.ShouldNotContain("alto");

        // Pass 2, with unknown declared: the state goes where it says. The held PBIs wait unscored.
        var loop = Path.Combine(fixture.Folders.Path, ".aio", "loop.yaml");
        await File.WriteAllTextAsync(
            loop,
            (await File.ReadAllTextAsync(loop)).Replace(
                "    tab: proposal\n    on:\n",
                "    tab: proposal\n    on:\n      unknown: in-progress\n",
                StringComparison.Ordinal
            )
        );
        Pbi(5);

        await RunOnce(client);

        fixture.Harness.Ran.Count.ShouldBe(2);
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "5").State.ShouldBe("in-progress");
        var moved = (await Journal(client, "?repository=owner/repo&pbi=5")).Single(line => line.Kind == "moved");
        moved.Why.ShouldBe($"unknown: {Unscored}");
    }

    sealed record Line(DateTimeOffset At, string Repository, string Pbi, string From, string Kind, string? To, string Why);

    void Pbi(int id) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", "proposal-review", false, FakeVendor.Me));

    static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    static async Task<List<Line>> Journal(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal{query}", JsonSerializerOptions.Web)).ShouldNotBeNull();
}
