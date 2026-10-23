using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #614. A state placed in an environment that cannot take a run now, such as sbx with no Claude
/// credential, gets no run: the PBI waits with the environment's sentence, as on an untrusted folder.
/// In a sandbox the agent keeps its own configuration, so this machine's trust is not asked.
/// </summary>
public sealed class SandboxPlacementTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    const string NoClaude = "sbx holds no Claude credential. Sign in once inside a sandbox with /login. Run: sbx run claude";

    [Fact]
    public async Task The_engine_waits_on_an_environment_that_cannot_take_the_run_and_skips_this_machines_trust_in_a_sandbox()
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "1"), "PBI 1", "ready-for-proposal", false, null));
        fixture.Runs.Placement = new RunPlacement("sbx", true, NoClaude);
        using var client = fixture.CreateClient();

        await RunOnce(client);
        await RunOnce(client);

        fixture.Runs.Started.ShouldBeEmpty();
        var waits = await Journal(client);
        waits.Select(line => $"{line.Kind} {line.From}").ShouldBe(["waited ready-for-proposal"], "one line however many passes it lasts");
        waits.Single().Why.ShouldBe(NoClaude);

        // The person signs in. Claude Code on this machine does not trust the folder, and the sandbox does not ask.
        fixture.Runs.Placement = new RunPlacement("sbx", true, null);
        fixture.Agent.Trusted = false;
        await RunOnce(client);

        fixture.Runs.Started.Single().Request.Provider.ShouldBe("claude-code");
        (await Journal(client)).Last().Kind.ShouldBe("started");
    }

    sealed record Line(string Kind, string From, string Why);

    static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    static async Task<List<Line>> Journal(HttpClient client)
    {
        var lines = await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi=1");
        lines.ShouldNotBeNull().Reverse();
        return lines;
    }
}
