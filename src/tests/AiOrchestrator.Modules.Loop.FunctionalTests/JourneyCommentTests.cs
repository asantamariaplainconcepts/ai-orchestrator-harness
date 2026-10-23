using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #696. The journey travels with the issue: the engine keeps one <c>aio:journey</c> comment per PBI,
/// written at the first transition and edited at every one after it, with a row per transition and per
/// finished run. A comment the vendor refuses is a line in the journal, and the PBI moves all the same.
/// </summary>
public sealed class JourneyCommentTests(LoopFixture fixture) : IClassFixture<LoopFixture>, IDisposable
{
    /// <summary>Every pass walks every PBI, so each test takes its own away before the next one's pass.</summary>
    public void Dispose() => fixture.Vendor.Pbis.RemoveAll(pbi => pbi.Ref.Id is "61" or "62" or "63" or "64" or "65");

    [Fact]
    public async Task Three_transitions_leave_one_journey_comment_with_three_rows_in_order()
    {
        Pbi(61, "verifying");
        fixture.Scripts.Answers["gate"] = (1, """{"outcome": "red", "why": "3 tests failed."}""");
        fixture.Scripts.Answers["evidence"] = (0, """{"published": false, "why": "nothing to capture."}""");
        fixture.Scripts.Answers["diff-risk"] = (0, """{"outcome": "low", "why": "two files, no port"}""");
        using var client = fixture.CreateClient();

        // One: the gate is red and the PBI is held.
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        var first = Journey("61");
        Rows(first.Body).Count.ShouldBe(1);

        // Two: a person releases it to merge-review. Three: the score is low and it moves to retro.
        await Post(client, "/api/pbi/release", new { repository = FakeFolders.Repository, id = "61", to = "merge-review" }, HttpStatusCode.OK);
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);

        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "61").State.ShouldBe("retro");
        var third = Journey("61");
        third.Id.ShouldBe(first.Id, "the comment is edited, never added again");
        third.Body.ShouldStartWith("<!-- aio:journey -->");
        Rows(third.Body)
            .Select(row => $"{row[1]} {row[2]} {row[3]} | {row[4]} | {row[6]}")
            .ShouldBe([
                "verifying held  | gate, evidence, quality | El gate está en rojo: 3 tests failed.",
                "verifying released merge-review |  | a person released the hold",
                "merge-review moved retro | diff-risk | low: two files, no port",
            ]);
        fixture.Vendor.Writes.ShouldNotContain(write => write.Contains("aio:journey"), "the journey is not one of the transition's writes");
    }

    [Fact]
    public async Task A_finished_run_is_a_row_with_its_command_its_outcome_and_why()
    {
        Pbi(62, "retro");
        using var client = fixture.CreateClient();

        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        fixture.Vendor.Comments.ContainsKey("62").ShouldBeFalse("a start moves nothing, so there is no journey yet");

        var index = fixture.Runs.Started.FindIndex(run => run.Request.Pbi.Id == "62");
        fixture.Runs.End(index, "next", "the retro entry is written");
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);

        Rows(Journey("62").Body)
            .Select(row => $"{row[1]} {row[2]} {row[3]} | {row[4]} | {row[5]} | {row[6]}")
            .ShouldBe([
                "retro run  | /aio:review 62 · claude-code · claude-sonnet-5-5 | next | the retro entry is written",
                "retro moved merging |  |  | next: the retro entry is written",
            ]);
    }

    /// <summary>
    /// #697. A terminal a person opened leads to no transition, so its end is what writes the comment:
    /// a <c>manual</c> row, filed under no state for a PBI the loop has not taken, with the path of
    /// its recording on <c>aio/evidence</c> in the last column.
    /// </summary>
    [Fact]
    public async Task A_run_a_person_opened_is_a_manual_row_with_the_path_of_its_recording_once_it_ends()
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, "64"), "PBI 64", null, false, null));
        var pbi = new PbiRef(FakeFolders.Repository, "64");
        var runId = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow.AddMinutes(-3);
        fixture.Runs.Manual.Add(
            new RunStatus(runId, pbi, "", new RunEnd(null, null, null, []) { EndedAt = started.AddSeconds(95) })
            {
                Manual = true,
                StartedAt = started,
                Recording = $"64/{runId:N}/session.cast",
            }
        );

        using (fixture.CreateClient())
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IRunEndings>().EndedAsync(pbi, CancellationToken.None);
        }

        var comment = Journey("64");
        comment.Body.ShouldContain("| When | State | Kind | To | Agent or script | Result | Why | Took | Cost | Recording |");
        Rows(comment.Body)
            .Select(row => $"{row[1]} {row[2]} | {row[7]} | {row[9]}")
            .ShouldBe([$"no state manual | 1m 35s | 64/{runId:N}/session.cast"]);
    }

    /// <summary>
    /// #697. A folder without <c>.aio/version</c> is a repository the app only looks at, such as a
    /// client's: a run that ends there, and a transition, leave no comment on its issues.
    /// </summary>
    [Fact]
    public async Task A_folder_without_the_harness_gets_no_journey_comment()
    {
        var version = Path.Combine(fixture.Folders.Path, ".aio", "version");
        var pbi = new PbiRef(FakeFolders.Repository, "65");
        fixture.Vendor.Pbis.Add(new Pbi(pbi, "PBI 65", null, false, null));
        fixture.Runs.Manual.Add(
            new RunStatus(Guid.NewGuid(), pbi, "", new RunEnd(null, null, null, []) { EndedAt = DateTimeOffset.UtcNow }) { Manual = true, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
        );
        File.Delete(version);
        try
        {
            using (fixture.CreateClient())
            {
                await using var scope = fixture.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRunEndings>().EndedAsync(pbi, CancellationToken.None);
            }

            fixture.Vendor.Comments.ContainsKey("65").ShouldBeFalse("no comment on a repository without the harness");
        }
        finally
        {
            File.WriteAllText(version, "16\n");
        }

        // The same run, once the harness is there, is a row as before.
        using (fixture.CreateClient())
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IRunEndings>().EndedAsync(pbi, CancellationToken.None);
        }

        Rows(Journey("65").Body).Select(row => row[2]).ShouldBe(["manual"]);
    }

    [Fact]
    public async Task A_comment_the_vendor_refuses_is_a_line_and_the_PBI_moves_all_the_same()
    {
        Pbi(63, "merge-review");
        fixture.Scripts.Answers["diff-risk"] = (0, """{"outcome": "low", "why": "two files, no port"}""");
        fixture.Vendor.RefusesComments = "gh: Resource not accessible by integration (HTTP 403)";
        try
        {
            using var client = fixture.CreateClient();
            await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);

            fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "63").State.ShouldBe("retro");
            var journal = await client.GetFromJsonAsync<List<JsonElement>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi=63");
            journal.ShouldNotBeNull()
                .Take(2)
                .Select(line => $"{line.GetProperty("kind").GetString()} {line.GetProperty("from").GetString()}: {line.GetProperty("why").GetString()}")
                .ShouldBe([
                    "waited merge-review: the journey comment was not written: gh: Resource not accessible by integration (HTTP 403)",
                    "moved merge-review: low: two files, no port",
                ]);
        }
        finally
        {
            fixture.Vendor.RefusesComments = null;
        }
    }

    PbiComment Journey(string id) => fixture.Vendor.Comments.GetValueOrDefault(id).ShouldNotBeNull().ShouldHaveSingleItem();

    /// <summary>The table's rows, each split into its cells, without the header and its rule.</summary>
    static List<string[]> Rows(string body) =>
        [
            .. body.Split('\n')
                .Where(line => line.StartsWith("| 20", StringComparison.Ordinal))
                .Select(line => line.Trim().Trim('|').Split(" | ").Select(cell => cell.Trim()).ToArray()),
        ];

    void Pbi(int id, string state) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, false, FakeVendor.Me));

    static async Task<JsonElement> Post(HttpClient client, string path, object? body, HttpStatusCode expected)
    {
        using var response = body is null ? await client.PostAsync(path, null) : await client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}

/// <summary>#710. What <see cref="JourneyComment.Render"/> puts in a row, from journal lines alone.</summary>
public sealed class JourneyCommentRowTests
{
    static readonly DateTimeOffset At = new(2026, 10, 13, 22, 47, 0, TimeSpan.Zero);

    /// <summary>
    /// The engine's own ran line, written since #707 when a person released the hold after a stop,
    /// names no script, so its words before the colon are not one in "Agent or script".
    /// </summary>
    [Fact]
    public void Only_a_ran_line_a_script_wrote_names_a_script()
    {
        var body = JourneyComment.Render(
            [
                Line(0, JournalKind.Ran, null, "the run failed (Stopped): Stopped by a person. The hold was released after the stop, so it stays off"),
                Line(1, JournalKind.Ran, null, "claim: change/62 pushed."),
                Line(2, JournalKind.Ran, null, "risk-score: low"),
                Line(3, JournalKind.Moved, "proposal", "ready: fine"),
            ],
            []
        );

        Cells(body).Single()[4].ShouldBe("claim, risk-score");
    }

    /// <summary>A why with a terminal's colour codes in it reaches the table without them.</summary>
    [Fact]
    public void A_why_with_colour_codes_is_a_cell_without_them()
    {
        var body = JourneyComment.Render(
            [Line(0, JournalKind.Held, null, "gate failed: \u001b[38;5;9m✗ [ERR] Step 'npm-lint' failed.\u001b[0m (6/9 steps succeeded)")],
            []
        );

        body.ShouldNotContain("\u001b");
        Cells(body).Single()[6].ShouldBe("gate failed: ✗ [ERR] Step 'npm-lint' failed. (6/9 steps succeeded)");
    }

    static JournalEntry Line(int second, JournalKind kind, string? to, string why) =>
        new(At.AddSeconds(second), FakeFolders.Repository, "62", "ready-for-proposal", kind, to, why);

    static List<string[]> Cells(string body) =>
        [
            .. body.Split('\n')
                .Where(line => line.StartsWith("| 20", StringComparison.Ordinal))
                .Select(line => line.Trim().Trim('|').Split(" | ").Select(cell => cell.Trim()).ToArray()),
        ];
}
