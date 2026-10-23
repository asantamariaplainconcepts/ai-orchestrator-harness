using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #636. An epic closes itself when its last sub-issue merges: the engine comments on it with what it
/// took, closes it and writes a <c>moved</c> line to <c>done</c> in its journal. While any sub-issue
/// is still open, held or not, the epic is left as it is.
/// </summary>
public sealed class EpicCloseTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task The_third_sub_issue_merging_closes_the_epic_with_a_comment_and_a_journal_line()
    {
        Epic("10", body: "Sub-issues #11, #12 and #13. A follow-up stays in #20, and #21 is already closed.");
        Child("11", "10", "done", closed: true);
        Child("12", "10", "done", closed: true);
        Child("13", "10", "merging");
        Pbi("20", null, closed: false);
        Pbi("21", null, closed: true);
        fixture.Vendor.Pulls["11"] = Pull("111", merged: true);
        fixture.Vendor.Pulls["13"] = Pull("113", merged: false);
        Merges();

        using var client = fixture.CreateClient();
        await RunOnce(client);

        fixture.Vendor.Writes.ShouldContain("close #13");
        fixture.Vendor.Writes.ShouldContain("close #10");
        fixture.Vendor.Writes.IndexOf("close #10").ShouldBeGreaterThan(fixture.Vendor.Writes.IndexOf("move #13 done"));
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "10").Closed.ShouldBeTrue();
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "10").State.ShouldBeNull("the epic's label is left as it was");

        var comment = fixture.Vendor.Writes.Single(write => write.StartsWith("comment #10: ", StringComparison.Ordinal));
        comment.ShouldBe(
            "comment #10: Closed by the loop: all 3 sub-issues are closed, with 2 pull requests. Opened 2026-10-01, closed "
                + $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.\n\n"
                + "- #11 PBI 11: pull request #111\n"
                + "- #12 PBI 12: no pull request\n"
                + "- #13 PBI 13: pull request #113\n"
                + "\nStill open, named in the body: #20.\n"
        );

        var journal = await client.GetFromJsonAsync<List<Line>>("/api/loop/journal?repository=owner/repo&pbi=10");
        var line = journal.ShouldHaveSingleItem();
        (line.Kind, line.From, line.To).ShouldBe(("moved", "no state", "done"));
        line.Why.ShouldBe("all 3 sub-issues are closed, the last one owner/repo#13");
    }

    [Fact]
    public async Task A_held_sub_issue_leaves_the_epic_open()
    {
        Epic("30", body: "");
        Child("31", "30", "done", closed: true);
        Child("32", "30", "proposal-review", held: true);
        Child("33", "30", "merging");
        fixture.Vendor.Pulls["33"] = Pull("133", merged: false);
        Merges();

        using var client = fixture.CreateClient();
        await RunOnce(client);

        fixture.Vendor.Writes.ShouldContain("close #33");
        fixture.Vendor.Writes.ShouldNotContain(write => write.EndsWith("#30", StringComparison.Ordinal) || write.Contains("#30:", StringComparison.Ordinal));
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "30").Closed.ShouldBeFalse();
        (await client.GetFromJsonAsync<List<Line>>("/api/loop/journal?repository=owner/repo&pbi=30")).ShouldBeEmpty();
    }

    sealed record Line(DateTimeOffset At, string Repository, string Pbi, string From, string Kind, string? To, string Why);

    void Epic(string id, string body)
    {
        Pbi(id, null, closed: false);
        fixture.Vendor.Bodies[id] = body;
    }

    void Child(string id, string parent, string state, bool held = false, bool closed = false)
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, id), $"PBI {id}", state, held, FakeVendor.Me, closed));
        fixture.Vendor.Parents[id] = parent;
    }

    void Pbi(string id, string? state, bool closed) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, id), $"PBI {id}", state, false, null, closed));

    static PullRequestInfo Pull(string id, bool merged) =>
        new(id, new Uri($"https://github.com/owner/repo/pull/{id}"), false, merged, true);

    void Merges()
    {
        fixture.Scripts.Answers["archive"] = (0, """{"ok": true, "archived": null, "open": [], "observed": false, "why": "nothing to archive."}""");
        fixture.Scripts.Answers["merge"] = (0, """{"outcome": "merged", "observed": true, "why": "The pull request reads back as merged."}""");
    }

    static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
