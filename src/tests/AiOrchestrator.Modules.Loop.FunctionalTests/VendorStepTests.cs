using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #594. One PBI walked through the real host, its loop read from the folder's file, by a loop
/// whose states are not named like this repository's: its pull request is opened as a draft,
/// marked ready and closed where the file's <c>pr</c> says, and nowhere else.
/// </summary>
public sealed class VendorStepTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    const string Renamed = """
        name: renamed
        agents:
          writer:
            provider: claude-code
        states:
          drafting:
            agent: writer
            command: /aio:propose
            next: building
            pr: draft
          building:
            agent: writer
            command: /aio:implement
            on:
              built:
                next: shipping
                pr: ready
          shipping:
            agent: writer
            command: /aio:merge
            on:
              shipped:
                next: finished
                pr: close
        """;

    [Fact]
    public async Task The_pull_request_goes_draft_ready_and_closed_where_the_loop_says()
    {
        await File.WriteAllTextAsync(Path.Combine(fixture.Folders.Path, ".aio", "loop.yaml"), Renamed);
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "7"), "PBI 7", "drafting", false, FakeVendor.Me));
        using var client = fixture.CreateClient();

        string[] outcomes = ["next", "built", "shipped"];
        for (var index = 0; index < outcomes.Length; index++)
        {
            await RunOnce(client);
            fixture.Runs.End(index, outcomes[index], "the agent said so");
            await RunOnce(client);
        }

        fixture.Vendor.Writes.Where(write => !write.StartsWith("assign", StringComparison.Ordinal)).ShouldBe([
            "open draft #7 from change/7",
            "move #7 building",
            "ready #7",
            "move #7 shipping",
            "close #7",
            "move #7 finished",
        ]);
        fixture.Vendor.Pulls["7"].IsDraft.ShouldBeFalse();
        fixture.Vendor.Pbis.Single().Closed.ShouldBeTrue();
    }

    static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
