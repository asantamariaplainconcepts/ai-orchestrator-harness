using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #706. A run that ended waiting for a person, a permission or a login, holds its PBI with the
/// sentence that says what to do, and asks the schedule for its next pass now, so the hold reaches
/// Needs you within the minute rather than at the end of the interval.
/// </summary>
public sealed class PersonWaitTests(LoopFixture fixture) : IClassFixture<LoopFixture>, IDisposable
{
    public void Dispose()
    {
        fixture.Vendor.Pbis.Clear();
        fixture.Runs.Started.Clear();
    }

    [Theory]
    [InlineData("1", RunFailure.NotLoggedIn, "Claude Code is not logged in. Run `claude /login` on this machine, then release.")]
    [InlineData(
        "2",
        RunFailure.WaitingForPerson,
        "The agent asked to run `python3 -c \"print(1)\"`. Switch permissions to auto in Settings or add it to the allow list of .claude/settings.json, then release."
    )]
    public async Task A_run_that_waited_for_a_person_holds_its_PBI_with_what_to_do(string id, RunFailure failure, string why)
    {
        var pbi = new PbiRef(FakeFolders.Repository, id);
        fixture.Vendor.Pbis.Add(new Pbi(pbi, $"PBI {id}", "ready-for-proposal", false, null));
        var passes = fixture.Services.GetRequiredService<Passes>();
        using var client = fixture.CreateClient();

        await RunOnce(client);
        await Ended(pbi);
        passes.TakeSoon().ShouldBeFalse("a run still going asks for nothing");

        fixture.Runs.Fail(fixture.Runs.Started.FindIndex(run => run.Request.Pbi == pbi), failure, why);
        await Ended(pbi);
        passes.TakeSoon().ShouldBeTrue();

        await RunOnce(client);

        fixture.Vendor.Pbis.Single(row => row.Ref == pbi).Held.ShouldBeTrue();
        var held = (await Journal(client, id)).Last();
        held.Kind.ShouldBe("held");
        held.Why.ShouldBe($"the run failed ({failure}): {why}");
    }

    [Fact]
    public async Task A_run_that_failed_otherwise_waits_for_the_interval()
    {
        var pbi = new PbiRef(FakeFolders.Repository, "3");
        fixture.Vendor.Pbis.Add(new Pbi(pbi, "PBI 3", "ready-for-proposal", false, null));
        var passes = fixture.Services.GetRequiredService<Passes>();
        using var client = fixture.CreateClient();

        await RunOnce(client);
        fixture.Runs.Fail(fixture.Runs.Started.FindIndex(run => run.Request.Pbi == pbi), RunFailure.NoEndRecord, "The agent ended without calling aio done.");
        await Ended(pbi);

        passes.TakeSoon().ShouldBeFalse();
    }

    async Task Ended(PbiRef pbi)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunEndings>().EndedAsync(pbi, CancellationToken.None);
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
