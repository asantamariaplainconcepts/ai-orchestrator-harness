using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #638. On 11 October a dogfood PBI held in <c>verifying</c> on "aspire do ci failed" and nothing
/// kept what the gate had printed. The engine keeps every script's output in a file of its own under
/// the PBI, the script's <c>ran</c> line names it, and <c>GET /api/loop/journal/{id}/output</c> serves
/// it, which is the link the Journey draws.
/// </summary>
public sealed class ScriptOutputTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task A_red_gates_output_is_kept_named_by_its_ran_line_and_served_by_it()
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "9"), "PBI 9", "verifying", false, FakeVendor.Me));
        fixture.Vendor.Pulls["9"] = new PullRequestInfo("19", new Uri("https://github.com/owner/repo/pull/19"), true, false, false);
        fixture.Scripts.Printed["gate"] = "12:01:01 ✓ ci-build succeeded\n12:01:02 ✗ ci-test failed: 3 tests failed\n9/10 steps succeeded";
        fixture.Scripts.Answers["gate"] = (1, """{"outcome": "red", "go": false, "why": "aspire do ci failed (exit 1): ✗ ci-test failed: 3 tests failed"}""");
        fixture.Scripts.Answers["evidence"] = (0, """{"published": false, "why": "The diff does not touch src/frontend/."}""");

        using var client = fixture.CreateClient();
        using (var pass = await client.PostAsync("/api/loop/run-once", null))
        {
            pass.StatusCode.ShouldBe(HttpStatusCode.OK, await pass.Content.ReadAsStringAsync());
        }

        var journal = (await client.GetFromJsonAsync<List<Line>>("/api/loop/journal?repository=owner/repo&pbi=9"))!;
        journal.Reverse();
        journal.Select(line => $"{line.Kind} {line.Why}").ShouldBe([
            "started running gate",
            "ran gate: aspire do ci failed (exit 1): ✗ ci-test failed: 3 tests failed",
            "started running evidence",
            "ran evidence: The diff does not touch src/frontend/.",
            "started running quality",
            "ran quality: Nothing published: gh could not comment.",
            "held El gate está en rojo: aspire do ci failed (exit 1): ✗ ci-test failed: 3 tests failed",
        ]);

        // Only the ran lines name a file; each sits under the PBI, named after its script.
        journal.Where(line => line.Kind != "ran").ShouldAllBe(line => line.Output == null);
        var gate = journal.Single(line => line.Kind == "ran" && line.Why.StartsWith("gate:", StringComparison.Ordinal));
        var evidence = journal.Single(line => line.Kind == "ran" && line.Why.StartsWith("evidence:", StringComparison.Ordinal));
        Path.GetDirectoryName(gate.Output).ShouldBe(Path.Combine(fixture.ScriptsDirectory, "owner", "repo", "9"));
        Path.GetFileName(gate.Output)!.ShouldMatch(@"^gate-\d{8}-\d{6}-\d{3}\.log$");
        Path.GetFileName(evidence.Output)!.ShouldStartWith("evidence-");
        File.ReadAllText(gate.Output!).ShouldBe(
            "12:01:01 ✓ ci-build succeeded\n12:01:02 ✗ ci-test failed: 3 tests failed\n9/10 steps succeeded\n"
                + """{"outcome": "red", "go": false, "why": "aspire do ci failed (exit 1): ✗ ci-test failed: 3 tests failed"}"""
        );

        // The route serves the file by the line, as text the browser shows.
        using var served = await client.GetAsync($"/api/loop/journal/{gate.Id}/output");
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.ToString().ShouldBe("text/plain; charset=utf-8");
        (await served.Content.ReadAsStringAsync()).ShouldBe(File.ReadAllText(gate.Output!));

        // A line that ran no script has nothing to serve, and a line that does not exist is 404 too.
        var held = journal.Single(line => line.Kind == "held");
        using var nothing = await client.GetAsync($"/api/loop/journal/{held.Id}/output");
        nothing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await nothing.Content.ReadAsStringAsync()).ShouldContain("Output.NotFound");
        using var unknown = await client.GetAsync("/api/loop/journal/987654321/output");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadAsStringAsync()).ShouldContain("Journal.NotFound");

        // Removed from disk, the line still stands and the route says the file is gone.
        File.Delete(gate.Output!);
        using var gone = await client.GetAsync($"/api/loop/journal/{gate.Id}/output");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await gone.Content.ReadAsStringAsync()).ShouldContain("was removed");
    }

    sealed record Line(long Id, DateTimeOffset At, string Pbi, string From, string Kind, string? To, string Why, string? Output);
}
