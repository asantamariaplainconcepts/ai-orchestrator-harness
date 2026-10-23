using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #597. A script runs inside the pass, so until it ends nothing at Runs says so. The engine writes
/// its start to the journal before it runs, and the present lists it while it runs, stale past the
/// threshold a silent run is held to.
/// </summary>
public sealed class RunningScriptTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task A_running_gate_is_in_the_journal_and_in_the_present_until_it_ends()
    {
        Pbi(7, "verifying");
        fixture.Vendor.Pulls["7"] = new PullRequestInfo("17", new Uri("https://github.com/owner/repo/pull/17"), true, false, false);
        fixture.Scripts.Answers["gate"] = (0, """{"go": true, "outcome": "green", "why": "aspire do ci: 1520 passed."}""");
        fixture.Scripts.Answers["evidence"] = (0, """{"published": false, "why": "The diff does not touch src/frontend/."}""");
        var gate = new TaskCompletionSource();
        fixture.Scripts.Until["gate"] = gate.Task;

        using var client = fixture.CreateClient();
        var pass = client.PostAsync("/api/loop/run-once", null);

        var running = await Until(client, scripts => scripts.Count == 1);
        var script = running.Single();
        (script.Repository, script.Pbi, script.State, script.Script, script.Stale).ShouldBe(("owner/repo", "7", "verifying", "gate", false));

        var started = (await Journal(client, "7")).ShouldHaveSingleItem();
        (started.Kind, started.From, started.To, started.Why).ShouldBe(("started", "verifying", "verifying", "running gate"));

        // Past the threshold a silent run is held to, the script is stale as such a run is.
        fixture.Runs.StaleAfter = TimeSpan.Zero;
        (await Present(client)).Single().Stale.ShouldBeTrue();

        gate.SetResult();
        using (var done = await pass)
        {
            done.StatusCode.ShouldBe(HttpStatusCode.OK, await done.Content.ReadAsStringAsync());
        }

        (await Present(client)).ShouldBeEmpty();
        (await Journal(client, "7")).Select(line => $"{line.Kind} {line.Why}").ShouldBe([
            "started running gate",
            "ran gate: aspire do ci: 1520 passed.",
            "started running evidence",
            "ran evidence: The diff does not touch src/frontend/.",
            "started running quality",
            "ran quality: Nothing published: gh could not comment.",
            "moved green: aspire do ci: 1520 passed.",
        ]);
    }

    [Fact]
    public async Task A_script_that_keeps_waiting_is_announced_once_with_its_wait()
    {
        Pbi(8, "spec-ready");
        fixture.Scripts.Answers["file-overlap"] = (0, """{"clear": false, "wait": true, "why": "#7 in flight shares src/a.cs."}""");

        using var client = fixture.CreateClient();
        for (var pass = 0; pass < 3; pass++)
        {
            using var response = await client.PostAsync("/api/loop/run-once", null);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        fixture.Scripts.Ran.Count(run => run.Pbi.Id == "8").ShouldBe(3);
        (await Journal(client, "8")).Select(line => $"{line.Kind} {line.Why}").ShouldBe([
            "started running file-overlap",
            "waited #7 in flight shares src/a.cs.",
        ]);
    }

    sealed record Line(DateTimeOffset At, string Repository, string Pbi, string From, string Kind, string? To, string Why);

    void Pbi(int id, string state) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, false, FakeVendor.Me));

    static async Task<List<RunningScript>> Present(HttpClient client)
    {
        var present = await client.GetFromJsonAsync<JsonElement>("/api/loop/present");
        return [.. present.GetProperty("liveScripts").EnumerateArray().Select(script => new RunningScript(
            script.GetProperty("repository").GetString()!,
            script.GetProperty("pbi").GetString()!,
            script.GetProperty("state").GetString()!,
            script.GetProperty("script").GetString()!,
            script.GetProperty("stale").GetBoolean()
        ))];
    }

    static async Task<List<RunningScript>> Until(HttpClient client, Func<List<RunningScript>, bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (true)
        {
            var scripts = await Present(client);
            if (condition(scripts) || DateTimeOffset.UtcNow > deadline)
            {
                return scripts;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>The PBI's journal oldest first; the API answers newest first.</summary>
    static async Task<List<Line>> Journal(HttpClient client, string pbi)
    {
        var lines = await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal?repository=owner/repo&pbi={pbi}");
        lines.ShouldNotBeNull().Reverse();
        return lines;
    }

    sealed record RunningScript(string Repository, string Pbi, string State, string Script, bool Stale);
}
