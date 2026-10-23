using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #707. A person stops a run and releases its PBI: the end of the run stopped does not hold the PBI
/// again, and a release while the run has not ended yet is refused instead of racing it.
/// </summary>
public sealed class StoppedReleaseTests(LoopFixture fixture) : IClassFixture<LoopFixture>, IDisposable
{
    public void Dispose()
    {
        fixture.Vendor.Pbis.Clear();
        fixture.Runs.Started.Clear();
    }

    /// <summary>The order of aio-dogfood#31 on 13 October: released at 21:32:19, held again at 21:32:28.</summary>
    [Fact]
    public async Task A_run_stopped_and_then_released_does_not_hold_its_PBI_again()
    {
        var pbi = Hung("1");
        using var client = fixture.CreateClient();
        await RunOnce(client);
        Hold(pbi);

        fixture.Runs.Fail(Index(pbi), RunFailure.Stopped, "Stopped by a person.");
        var released = await Release(client, pbi.Id, HttpStatusCode.OK);
        released.GetProperty("kind").GetString().ShouldBe("released");
        await Ended(pbi);
        await RunOnce(client);

        fixture.Vendor.Pbis.Single(row => row.Ref == pbi).Held.ShouldBeFalse();
        var journal = await Journal(client, pbi.Id);
        journal.Select(line => line.Kind).ShouldBe(["started", "released", "ran"]);
        journal.Last().Why.ShouldBe(
            "the run failed (Stopped): Stopped by a person. The hold was released after the stop, so it stays off"
        );

        // The run is acted on once: the next pass asks for the state's agent again.
        await RunOnce(client);
        fixture.Runs.Started.Count(run => run.Request.Pbi == pbi).ShouldBe(2);
    }

    [Fact]
    public async Task Releasing_while_the_run_has_not_ended_is_refused_and_writes_nothing()
    {
        var pbi = Hung("2");
        using var client = fixture.CreateClient();
        await RunOnce(client);
        Hold(pbi);

        var refused = await Release(client, pbi.Id, HttpStatusCode.Conflict);

        refused.GetProperty("detail").GetString().ShouldBe("Nothing was changed: a run of this PBI is still stopping; release it once it ends.");
        fixture.Vendor.Pbis.Single(row => row.Ref == pbi).Held.ShouldBeTrue();
        (await Journal(client, pbi.Id)).Select(line => line.Kind).ShouldBe(["started"]);
    }

    /// <summary>What #707 leaves as it was: a run that failed with nobody stopping it holds.</summary>
    [Fact]
    public async Task A_run_that_failed_on_its_own_after_a_release_still_holds()
    {
        var pbi = Hung("3");
        using var client = fixture.CreateClient();
        await RunOnce(client);
        Hold(pbi);

        fixture.Runs.Fail(Index(pbi), RunFailure.NoEndRecord, "The agent ended without calling aio done.");
        await Release(client, pbi.Id, HttpStatusCode.OK);
        await RunOnce(client);

        fixture.Vendor.Pbis.Single(row => row.Ref == pbi).Held.ShouldBeTrue();
        (await Journal(client, pbi.Id)).Last().Why.ShouldBe("the run failed (NoEndRecord): The agent ended without calling aio done.");
    }

    PbiRef Hung(string id)
    {
        var pbi = new PbiRef(FakeFolders.Repository, id);
        fixture.Vendor.Pbis.Add(new Pbi(pbi, $"PBI {id}", "ready-for-proposal", false, null));
        return pbi;
    }

    /// <summary>A person holds the PBI at the vendor while its run goes on.</summary>
    void Hold(PbiRef pbi)
    {
        var index = fixture.Vendor.Pbis.FindIndex(row => row.Ref == pbi);
        fixture.Vendor.Pbis[index] = fixture.Vendor.Pbis[index] with { Held = true };
    }

    int Index(PbiRef pbi) => fixture.Runs.Started.FindIndex(run => run.Request.Pbi == pbi);

    async Task Ended(PbiRef pbi)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunEndings>().EndedAsync(pbi, CancellationToken.None);
    }

    static async Task<JsonElement> Release(HttpClient client, string id, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/pbi/release",
            new { repository = FakeFolders.Repository, id, to = "ready-for-proposal" }
        );
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    sealed record Line(string Kind, string From, string Why);

    static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    static async Task<List<Line>> Journal(HttpClient client, string pbi)
    {
        var lines = await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi={pbi}");
        lines.ShouldNotBeNull().Reverse();
        return lines;
    }
}
