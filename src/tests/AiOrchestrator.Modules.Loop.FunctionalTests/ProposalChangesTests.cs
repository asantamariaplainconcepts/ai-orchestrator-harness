using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #512. <i>Request changes</i> on the Proposal tab through the API: the review goes to the vendor
/// as one that comments, the hold comes off and the PBI goes back to <c>spec-ready</c>, and the
/// journal says a person asked for it, which is the line the Journey shows. Anywhere but
/// <c>proposal-review</c> the review stands and nothing moves, and the answer says why.
/// </summary>
public sealed class ProposalChangesTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task Requesting_changes_posts_the_review_and_sends_the_PBI_back_to_spec_ready()
    {
        Pbi(31, "proposal-review", held: true);
        using var client = fixture.CreateClient();

        var answer = await Post(
            client,
            new { repository = FakeFolders.Repository, id = "31", body = "Split the port change into its own ADR." },
            HttpStatusCode.OK
        );

        var line = answer.GetProperty("line");
        line.GetProperty("kind").GetString().ShouldBe("released");
        line.GetProperty("from").GetString().ShouldBe("proposal-review");
        line.GetProperty("to").GetString().ShouldBe("spec-ready");
        line.GetProperty("why").GetString().ShouldBe("a person requested changes on the proposal");
        var pbi = fixture.Vendor.Pbis.Single(row => row.Ref.Id == "31");
        (pbi.State, pbi.Held).ShouldBe(("spec-ready", false));
        fixture.Vendor.Reviews.ShouldContain("Split the port change into its own ADR.");
        var writes = fixture.Vendor.Writes;
        writes.IndexOf("review #31").ShouldBeLessThan(writes.IndexOf("move #31 spec-ready"), "the review is at the vendor before the PBI moves");

        var journal = await client.GetFromJsonAsync<List<JsonElement>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi=31");
        journal.ShouldNotBeNull().First().GetProperty("why").GetString().ShouldBe("a person requested changes on the proposal");
    }

    [Fact]
    public async Task Outside_proposal_review_the_review_stands_and_nothing_moves()
    {
        Pbi(32, "in-progress", held: false);
        using var client = fixture.CreateClient();

        var answer = await Post(client, new { repository = FakeFolders.Repository, id = "32", body = "Too late." }, HttpStatusCode.OK);

        answer.GetProperty("line").ValueKind.ShouldBe(JsonValueKind.Null);
        answer.GetProperty("unmoved").GetString().ShouldNotBeNull().ShouldContain("tab: proposal, and in-progress is not one");
        fixture.Vendor.Pbis.Single(row => row.Ref.Id == "32").State.ShouldBe("in-progress");

        await Post(client, new { repository = FakeFolders.Repository, id = "32", body = "" }, HttpStatusCode.BadRequest);
    }

    void Pbi(int id, string state, bool held) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, held, FakeVendor.Me));

    static async Task<JsonElement> Post(HttpClient client, object body, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync("/api/pbi/proposal/request-changes", body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
