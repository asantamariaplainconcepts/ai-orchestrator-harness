using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #511. The PBI header's writes through the API, against the vendor, the scripts and Runs held in
/// memory: a person's release goes where the hold leads, exactly as an approval would, and the
/// journal says a person did it; a hold that leads nowhere takes the state the person chose; a PBI
/// where the loop does nothing is moved to the state chosen; and the loop the Journey draws is the
/// one the engine reads.
/// </summary>
public sealed class PersonDecisionTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task Releasing_a_hold_goes_on_by_next_and_the_journal_says_a_person_released_it()
    {
        Pbi(21, "merge-review", held: false);
        fixture.Scripts.Answers["diff-risk"] = (0, """{"outcome": "high", "why": "touches the IPbiProvider port"}""");
        using var client = fixture.CreateClient();

        // The engine holds it on the score, with retro as where the release leads.
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "21").Held.ShouldBeTrue();

        var released = await Post(client, "/api/pbi/release", new { repository = FakeFolders.Repository, id = "21" }, HttpStatusCode.OK);

        released.GetProperty("kind").GetString().ShouldBe("released");
        released.GetProperty("from").GetString().ShouldBe("merge-review");
        released.GetProperty("to").GetString().ShouldBe("retro");
        released.GetProperty("why").GetString().ShouldBe("a person released the hold");
        var pbi = fixture.Vendor.Pbis.Single(row => row.Ref.Id == "21");
        pbi.Held.ShouldBeFalse();
        pbi.State.ShouldBe("retro");
        fixture.Vendor.Writes.ShouldContain("release #21");
        fixture.Vendor.Writes.ShouldContain("move #21 retro");

        var journal = await client.GetFromJsonAsync<List<JsonElement>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi=21");
        journal.ShouldNotBeNull()
            .Select(line => $"{line.GetProperty("kind").GetString()} {line.GetProperty("from").GetString()} → {line.GetProperty("to").GetString()}")
            .ShouldBe(["released merge-review → retro", "held merge-review → retro", "ran merge-review → ", "started merge-review → merge-review"]);

        // A second release finds no hold and changes nothing.
        await Post(client, "/api/pbi/release", new { repository = FakeFolders.Repository, id = "21" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_hold_that_leads_nowhere_asks_for_a_state_and_takes_the_one_chosen()
    {
        Pbi(22, "verifying", held: false);
        fixture.Scripts.Answers["gate"] = (1, """{"outcome": "red", "why": "3 tests failed."}""");
        fixture.Scripts.Answers["evidence"] = (0, """{"published": false, "why": "nothing to capture."}""");
        using var client = fixture.CreateClient();

        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "22").Held.ShouldBeTrue();

        var refused = await Post(client, "/api/pbi/release", new { repository = FakeFolders.Repository, id = "22" }, HttpStatusCode.Conflict);
        refused.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("choose the state");

        var released = await Post(
            client,
            "/api/pbi/release",
            new { repository = FakeFolders.Repository, id = "22", to = "in-progress" },
            HttpStatusCode.OK
        );
        released.GetProperty("to").GetString().ShouldBe("in-progress");
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "22").State.ShouldBe("in-progress");
    }

    [Fact]
    public async Task A_PBI_where_the_loop_does_nothing_moves_to_the_state_chosen_and_nowhere_else_does()
    {
        Pbi(23, "needs-refinement", held: false);
        Pbi(24, "in-progress", held: false);
        using var client = fixture.CreateClient();

        var moved = await Post(
            client,
            "/api/pbi/move",
            new { repository = FakeFolders.Repository, id = "23", to = "ready-for-proposal" },
            HttpStatusCode.OK
        );
        moved.GetProperty("kind").GetString().ShouldBe("moved");
        moved.GetProperty("why").GetString().ShouldBe("a person moved it from needs-refinement");
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "23").State.ShouldBe("ready-for-proposal");

        await Post(client, "/api/pbi/move", new { repository = FakeFolders.Repository, id = "24", to = "done" }, HttpStatusCode.Conflict);
        await Post(client, "/api/pbi/move", new { repository = FakeFolders.Repository, id = "23", to = "nowhere" }, HttpStatusCode.Conflict);
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "24").State.ShouldBe("in-progress");
    }

    [Fact]
    public async Task The_Journey_reads_the_loop_the_engine_reads()
    {
        using var client = fixture.CreateClient();

        var loop = await client.GetFromJsonAsync<JsonElement>($"/api/loop/definition?repository={FakeFolders.Repository}");

        loop.GetProperty("problem").ValueKind.ShouldBe(JsonValueKind.Null);
        loop.GetProperty("states").EnumerateArray().Select(state => state.GetProperty("name").GetString()).ShouldBe([
            "ready-for-proposal",
            "spec-ready",
            "proposal-review",
            "in-progress",
            "verifying",
            "code-review",
            "merge-review",
            "retro",
            "merging",
            "rebasing",
        ]);
        loop.GetProperty("undeclared").EnumerateArray().Select(state => state.GetString()).ShouldBe(["needs-refinement", "done"]);
        var review = loop.GetProperty("states").EnumerateArray().Single(state => state.GetProperty("name").GetString() == "merge-review");
        review.GetProperty("score").GetString().ShouldBe("diff-risk");
        review.GetProperty("transitions").EnumerateArray().Last().GetProperty("hold").GetString().ShouldBe("El riesgo del diff salió alto");
        (review.GetProperty("back").GetString(), review.GetProperty("tab").GetString()).ShouldBe(("in-progress", "review"));

        using var unknown = await client.GetAsync("/api/loop/definition?repository=other/repo");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// #595. A loop with none of this repository's names: Request changes goes where the state's
    /// <c>back</c> says, and a state without one sends the PBI nowhere.
    /// </summary>
    const string Invented = """
        name: invented
        agents:
          writer:
            provider: claude-code
        states:
          sketch:
            agent: writer
            command: /sketch
            next: critique
          critique:
            score: taste
            back: sketch
            tab: proposal
            on:
              fine: build
              poor:
                hold: Taste said no
                next: build
          build:
            agent: writer
            command: /build
            next: check
          check:
            script: tests
            on:
              green: judge
              red:
                hold: Tests are red
          judge:
            score: risk
            back: build
            tab: review
            on:
              low: shipped
              high:
                hold: Risky
                next: shipped
        """;

    [Theory]
    [InlineData("critique", true, "release sketch")]
    [InlineData("critique", false, "move sketch")]
    [InlineData("judge", true, "ignore")]
    [InlineData("check", true, "ignore")]
    public void Request_changes_on_the_proposal_follows_back_in_an_invented_loop(string state, bool held, string expected)
    {
        var loop = LoopReader.Read(Invented).Loop.ShouldNotBeNull();

        Describe(Planner.ProposalChangesRequested(loop, At(state, held))).ShouldBe(expected);
    }

    [Theory]
    [InlineData("judge", true, "release build")]
    [InlineData("critique", true, "release sketch")]
    [InlineData("judge", false, "ignore")]
    [InlineData("check", true, "ignore")]
    [InlineData("shipped", true, "ignore")]
    public void Request_changes_on_the_pull_request_follows_back_in_an_invented_loop(string state, bool held, string expected)
    {
        var loop = LoopReader.Read(Invented).Loop.ShouldNotBeNull();

        Describe(Planner.ChangesRequested(loop, At(state, held))).ShouldBe(expected);
    }

    [Fact]
    public void A_state_without_back_says_why_nothing_moves()
    {
        var loop = LoopReader.Read(Invented).Loop.ShouldNotBeNull();

        Planner.ChangesRequested(loop, At("check", true))
            .ShouldBeOfType<Ignore>()
            .Why.ShouldBe("check declares no back in the loop invented, so a request for changes sends the PBI nowhere");
    }

    static Pbi At(string state, bool held) => new(new PbiRef(FakeFolders.Repository, "9"), "PBI 9", state, held, FakeVendor.Me);

    static string Describe(Decision decision) =>
        decision switch
        {
            Release release => $"release {release.To}",
            Move move => $"move {move.To}",
            Ignore => "ignore",
            _ => decision.ToString(),
        };

    void Pbi(int id, string state, bool held) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, held, FakeVendor.Me));

    static async Task<JsonElement> Post(HttpClient client, string path, object? body, HttpStatusCode expected)
    {
        using var response = body is null ? await client.PostAsync(path, null) : await client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
