using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #606. A folder Claude Code does not trust gets no claude-code run: the run would sit on the trust
/// question until it went stale, as the first one in <c>aio-dogfood</c> did for 80 minutes.
/// </summary>
public sealed class UntrustedFolderTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task The_engine_does_not_start_the_run_and_the_journal_says_why_once_until_the_folder_is_trusted()
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "1"), "PBI 1", "ready-for-proposal", false, null));
        fixture.Agent.Trusted = false;
        using var client = fixture.CreateClient();

        await RunOnce(client);
        await RunOnce(client);

        fixture.Runs.Started.ShouldBeEmpty();
        var waits = await Journal(client);
        waits.Select(line => $"{line.Kind} {line.From}").ShouldBe(["waited ready-for-proposal"], "one line however many passes it lasts");
        waits.Single().Why.ShouldBe($"Claude Code does not trust {fixture.Folders.Path}. Run claude there once and accept it.");

        // The person runs claude there and accepts; the next pass starts the run.
        fixture.Agent.Trusted = true;
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
