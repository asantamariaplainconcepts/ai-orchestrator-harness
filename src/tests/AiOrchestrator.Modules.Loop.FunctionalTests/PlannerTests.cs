using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #505. The decision over photographs of one PBI, against this repository's own
/// <c>.aio/loop.yaml</c>: every transition the file declares, its holds, <c>max_loops</c>
/// spent, a full WIP, someone else's PBI and a run that failed. Each row is a situation and the
/// decision it must give, written as <see cref="Describe"/> prints it.
/// </summary>
public sealed class PlannerTests
{
    static readonly LoopDefinition Loop =
        LoopReader.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "loop.yaml"))).Loop!;

    const string Me = "aio-bot";

    // ── Each transition of loop.yaml ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ready-for-proposal", "ready", "move spec-ready")]
    [InlineData("ready-for-proposal", "gaps", "move needs-refinement")]
    [InlineData("spec-ready", "next", "move proposal-review, then OpenDraftPullRequest")]
    [InlineData("in-progress", "next", "move verifying")]
    [InlineData("code-review", "rejected", "move in-progress")]
    [InlineData("code-review", "approved", "move merge-review")]
    [InlineData("retro", "next", "move merging")]
    [InlineData("rebasing", "next", "move merging")]
    public void An_ended_run_moves_the_PBI_where_its_outcome_leads(string state, string outcome, string expected) =>
        Describe(Planner.Decide(At(state) with { Run = Ended(outcome) })).ShouldBe(expected);

    [Theory]
    [InlineData("proposal-review", "risk-score:low", "move in-progress")]
    [InlineData("proposal-review", "risk-score:high", "hold El score de riesgo salió alto: the model said so, then in-progress")]
    [InlineData("verifying", "gate:green evidence:pass", "move code-review, then MarkReady")]
    [InlineData("verifying", "gate:red evidence:pass", "hold El gate está en rojo: the model said so, then a person chooses")]
    [InlineData("verifying", "gate:green evidence:skipped", "move code-review, then MarkReady")]
    [InlineData("verifying", "gate:green evidence:passed", "move code-review, then MarkReady")]
    [InlineData("verifying", "gate:green evidence:blocked", "hold La evidencia hacía falta y no se pudo tomar: the model said so, then a person chooses")]
    [InlineData("verifying", "gate:green evidence:failed", "hold Las capturas de la evidencia fallaron: the model said so, then a person chooses")]
    [InlineData("verifying", "gate:red evidence:blocked", "hold El gate está en rojo: the model said so, then a person chooses")]
    [InlineData("merge-review", "diff-risk:low", "move retro")]
    [InlineData("merge-review", "diff-risk:high", "hold El riesgo del diff salió alto: the model said so, then retro")]
    [InlineData("merging", "archive:pass merge:merged", "move done, then Close")]
    [InlineData("merging", "archive:pass merge:conflict", "move rebasing")]
    public void A_script_or_a_score_decides_the_outcome_of_its_state(string state, string verdicts, string expected) =>
        Describe(Planner.Decide(At(state) with { Scripts = Verdicts(verdicts) })).ShouldBe(expected);

    [Theory]
    [InlineData("ready-for-proposal", "start claude-code claude-opus-5-5 /aio:refine 7 [ready, gaps]")]
    [InlineData("in-progress", "start claude-code claude-sonnet-5-5 /aio:implement 7 [next]")]
    [InlineData("code-review", "start claude-code claude-sonnet-5-5 /aio:review-diff 7 [rejected, approved]")]
    [InlineData("retro", "start claude-code claude-sonnet-5-5 /aio:review 7 [next]")]
    [InlineData("rebasing", "start claude-code claude-sonnet-5-5 /rebase-safely 7 [next]")]
    public void An_agent_state_with_nothing_asked_yet_asks_Runs_for_its_agent(string state, string expected) =>
        Describe(Planner.Decide(At(state))).ShouldBe(expected);

    [Theory]
    [InlineData("spec-ready", "scripts file-overlap, claim")]
    [InlineData("proposal-review", "scripts risk-score")]
    [InlineData("verifying", "scripts gate, evidence, quality")]
    [InlineData("merge-review", "scripts diff-risk")]
    [InlineData("merging", "scripts archive, merge")]
    public void A_state_with_scripts_runs_them_first(string state, string expected) =>
        Describe(Planner.Decide(At(state))).ShouldBe(expected);

    [Fact]
    public void The_claim_passing_starts_the_proposal() =>
        Describe(Planner.Decide(At("spec-ready") with { Scripts = Verdicts("file-overlap:pass claim:pass") }))
            .ShouldBe("start claude-code claude-opus-5-5 /aio:propose 7 [next]");

    // ── Holds and waits ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_file_overlap_waits_without_holding() =>
        Describe(Planner.Decide(At("spec-ready") with { Scripts = Verdicts("file-overlap:wait") })).ShouldBe("wait the model said so");

    [Theory]
    [InlineData(
        1,
        """{"clear": false, "hold": true, "why": "#7 has no Where it lands section, so its files cannot be compared. /aio:refine adds one."}""",
        "hold file-overlap failed: #7 has no Where it lands section, so its files cannot be compared. /aio:refine adds one., then a person chooses"
    )]
    [InlineData(
        0,
        """{"wait": true, "clear": false, "hold": false, "why": "#9 is in flight and declares .ai/commands/aio/refine.md too."}""",
        "wait #9 is in flight and declares .ai/commands/aio/refine.md too."
    )]
    public void An_issue_that_cannot_be_compared_holds_and_one_that_collides_waits(int exit, string line, string expected)
    {
        // #576: #575 sat in spec-ready unseen because every clear: false was a wait.
        var verdict = ScriptVerdicts.Read(
            "file-overlap",
            new ScriptResult(exit, System.Text.Json.JsonDocument.Parse(line).RootElement.Clone(), line)
        );

        Describe(Planner.Decide(At("spec-ready") with { Scripts = [verdict] })).ShouldBe(expected);
    }

    /// <summary>
    /// #733. The gate is green and the evidence answers blocked with exit 1: the evidence's blocked is
    /// the state's, and the PBI holds with the evidence's own why. A loop.yaml written before evidence
    /// had outcomes, whose on names green and red only, holds it too, as a failure of evidence; its
    /// skipped, exit 0, leaves the gate's green to decide.
    /// </summary>
    [Fact]
    public void Blocked_evidence_after_a_green_gate_holds_with_the_evidence_why()
    {
        const string Why = "Nothing published: aspire start failed: error: the AppHost failed to build.";
        var gate = ScriptVerdicts.Read("gate", Line(0, """{"outcome": "green", "go": true, "why": "aspire do ci passed"}"""));
        var blocked = ScriptVerdicts.Read("evidence", Line(1, $$"""{"outcome": "blocked", "published": false, "comment": null, "why": "{{Why}}"}"""));
        var skipped = ScriptVerdicts.Read("evidence", Line(0, """{"outcome": "skipped", "published": false, "comment": null, "why": "Nothing published: nothing in src/frontend/** changed against origin/master."}"""));

        Describe(Planner.Decide(At("verifying") with { Scripts = [gate, blocked] }))
            .ShouldBe($"hold La evidencia hacía falta y no se pudo tomar: {Why}, then a person chooses");

        var before = LoopReader.Read(
            """
            name: before
            states:
              verifying:
                script: [gate, evidence]
                on:
                  green: code-review
                  red:
                    hold: The gate is red
              code-review:
                command: /aio:review-diff
                next: done
            """
        ).Loop!;
        Describe(Planner.Decide(In(before, "verifying") with { Scripts = [gate, blocked] })).ShouldBe($"hold evidence failed: {Why}, then a person chooses");
        Describe(Planner.Decide(In(before, "verifying") with { Scripts = [gate, skipped] })).ShouldBe("move code-review");
    }

    static ScriptResult Line(int exit, string line) => new(exit, System.Text.Json.JsonDocument.Parse(line).RootElement.Clone(), line);

    [Fact]
    public void A_failed_script_holds_with_its_reason() =>
        Describe(Planner.Decide(At("merging") with { Scripts = Verdicts("archive:fail") }))
            .ShouldBe("hold archive failed: the model said so, then a person chooses");

    [Fact]
    public void A_score_that_names_no_outcome_holds() =>
        // #593: a score is not special to the verdict; one that answers no outcome is a pass, and a
        // state whose on wants an outcome holds rather than guessing one.
        Describe(Planner.Decide(At("proposal-review") with { Scripts = Verdicts("risk-score:pass") }))
            .ShouldBe("hold proposal-review ran risk-score and none of them named an outcome its on knows, then a person chooses");

    [Fact]
    public void A_full_WIP_waits_before_any_script_runs()
    {
        Describe(Planner.Decide(At("spec-ready") with { LiveRuns = 4 })).ShouldBe("wait WIP 4 of 4");
        Describe(Planner.Decide(At("code-review") with { LiveRuns = 5 })).ShouldBe("wait WIP 5 of 4");
    }

    [Fact]
    public void A_full_WIP_does_not_stop_a_state_that_runs_no_agent() =>
        Describe(Planner.Decide(At("verifying") with { LiveRuns = 4 })).ShouldBe("scripts gate, evidence, quality");

    [Fact]
    public void Max_loops_spent_holds_instead_of_going_back()
    {
        var spent = At("code-review") with { Run = Ended("rejected"), Returns = new Dictionary<string, int> { ["code-review"] = 2 } };

        Describe(Planner.Decide(spent)).ShouldBe(
            "hold code-review has sent this PBI back 2 of 2 times (max_loops), and rejected would send it back again: the agent said so, then a person chooses"
        );
        Describe(Planner.Decide(spent with { Returns = new Dictionary<string, int> { ["code-review"] = 1 } })).ShouldBe("move in-progress");
    }

    [Fact]
    public void A_second_conflict_holds_at_merging_instead_of_entering_rebasing_again()
    {
        var second = At("merging") with { Scripts = Verdicts("archive:pass merge:conflict"), Returns = new Dictionary<string, int> { ["rebasing"] = 1 } };

        Describe(Planner.Decide(second)).ShouldBe(
            "hold rebasing has sent this PBI back 1 of 1 times (max_loops), so merging does not enter it again: conflict: the model said so, then a person chooses"
        );
    }

    [Fact]
    public void A_held_PBI_waits_and_says_why()
    {
        var held = At("verifying") with { Pbi = Pbi("verifying") with { Held = true } };

        Describe(Planner.Decide(held)).ShouldBe("wait held at verifying by a person");
        Describe(Planner.Decide(held with { Last = new(JournalKind.Held, "verifying", null, "El gate está en rojo: 3 tests") }))
            .ShouldBe("wait held at verifying: El gate está en rojo: 3 tests");
    }

    [Fact]
    public void An_approval_at_the_vendor_releases_a_hold_that_leads_somewhere()
    {
        var held = At("merge-review") with
        {
            Pbi = Pbi("merge-review") with { Held = true },
            Last = new(JournalKind.Held, "merge-review", "retro", "El riesgo del diff salió alto: 40 files"),
        };

        Describe(Planner.Decide(held)).ShouldBe("wait held at merge-review: El riesgo del diff salió alto: 40 files");
        Describe(Planner.Decide(held with { Approved = true })).ShouldBe("release retro: the pull request was approved at the vendor");
    }

    [Fact]
    public void A_hold_a_person_released_goes_where_it_led_or_does_the_state_again()
    {
        Describe(Planner.Decide(At("proposal-review") with { Last = new(JournalKind.Held, "proposal-review", "in-progress", "alto") }))
            .ShouldBe("move in-progress");
        Describe(Planner.Decide(At("verifying") with { Last = new(JournalKind.Held, "verifying", null, "rojo") }))
            .ShouldBe("scripts gate, evidence, quality");
    }

    // ── Whose PBI, and which runs ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_PBI_assigned_to_someone_else_is_not_touched()
    {
        Describe(Planner.Decide(At("in-progress") with { Pbi = Pbi("in-progress") with { Assignee = "someone" } }))
            .ShouldBe("ignore assigned to someone");
        Describe(Planner.Decide(At("in-progress") with { Pbi = Pbi("in-progress") with { Assignee = "AIO-BOT" } }))
            .ShouldStartWith("start");
    }

    [Theory]
    [InlineData("done")]
    [InlineData("needs-refinement")]
    [InlineData(null)]
    public void A_state_the_loop_does_not_run_is_not_touched(string? state) =>
        Describe(Planner.Decide(At("in-progress") with { Pbi = Pbi(state) })).ShouldStartWith("ignore");

    [Fact]
    public void A_failed_run_holds_with_its_cause_and_a_blocked_agent_with_its_reason()
    {
        var failed = new AskedRunStatus(Guid.Empty, true, new RunEnd(null, "exited without aio done", RunFailure.NoEndRecord, []));
        var blocked = new AskedRunStatus(Guid.Empty, true, new RunEnd(null, "the spec contradicts the issue", RunFailure.Blocked, []));

        Describe(Planner.Decide(At("in-progress") with { Run = failed }))
            .ShouldBe("hold the run failed (NoEndRecord): exited without aio done, then a person chooses");
        Describe(Planner.Decide(At("in-progress") with { Run = blocked }))
            .ShouldBe("hold the agent is blocked: the spec contradicts the issue, then a person chooses");
    }

    [Fact]
    public void A_run_in_flight_waits_and_an_unknown_one_holds()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000505");

        Describe(Planner.Decide(At("in-progress") with { Run = new(id, true, null) })).ShouldBe($"wait run {id} is in flight");
        Describe(Planner.Decide(At("in-progress") with { Run = new(id, false, null) }))
            .ShouldBe($"hold Runs has no run {id}, which the loop asked for at in-progress, then a person chooses");
    }

    [Fact]
    public void An_outcome_the_state_does_not_name_holds() =>
        Describe(Planner.Decide(At("code-review") with { Run = Ended("maybe") }))
            .ShouldBe("hold code-review produced maybe, which its on does not name: the agent said so, then a person chooses");

    [Theory]
    [InlineData("/aio:implement", "/aio:implement 7")]
    [InlineData("/rebase-safely", "/rebase-safely 7")]
    public void The_command_is_asked_for_as_the_loop_names_it(string command, string expected) =>
        Planner.Command(command, "7").ShouldBe(expected);

    // ── A state that names no agent (#546) ─────────────────────────────────────────────────────

    static readonly LoopDefinition Unnamed =
        LoopReader.Read(
            """
            name: unnamed
            agents:
              reviewer:
                provider: opencode
            states:
              in-progress:
                command: /aio:implement
                next: verifying
              pinned:
                command: /aio:implement
                model: claude-sonnet-5-5
                next: verifying
              code-review:
                agent: reviewer
                command: /aio:review-diff
                next: verifying
            """
        ).Loop!;

    [Fact]
    public void A_state_with_no_agent_starts_with_the_folders_agent_and_model()
    {
        var folder = new LoopAgent("claude-code", "claude-fable-5-1");

        Describe(Planner.Decide(new Situation(Unnamed, Pbi("in-progress"), Me, 0, null, null, new Dictionary<string, int>(), null, false, folder)))
            .ShouldBe("start claude-code claude-fable-5-1 /aio:implement 7 [next]");
        Describe(Planner.Decide(new Situation(Unnamed, Pbi("pinned"), Me, 0, null, null, new Dictionary<string, int>(), null, false, folder)))
            .ShouldBe("start claude-code claude-sonnet-5-5 /aio:implement 7 [next]");
    }

    [Fact]
    public void A_state_that_names_its_agent_does_not_take_the_folders()
    {
        var folder = new LoopAgent("claude-code", "claude-fable-5-1");

        Describe(Planner.Decide(new Situation(Unnamed, Pbi("code-review"), Me, 0, null, null, new Dictionary<string, int>(), null, false, folder)))
            .ShouldBe("start opencode  /aio:review-diff 7 [next]");
    }

    [Fact]
    public void A_state_with_no_agent_in_a_folder_with_none_holds_and_says_why() =>
        Describe(Planner.Decide(new Situation(Unnamed, Pbi("in-progress"), Me, 0, null, null, new Dictionary<string, int>(), null, false)))
            .ShouldBe("hold in-progress names no agent, and its folder has no default agent, then a person chooses");

    // ── The vendor steps come from pr, not from the names of states (#594) ─────────────────────────

    static readonly LoopDefinition Renamed =
        LoopReader.Read(
            """
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
                next: testing
              testing:
                script: gate
                on:
                  passed:
                    next: shipping
                    pr: ready
                  failed:
                    hold: Red
              shipping:
                script: merge
                on:
                  shipped:
                    next: finished
                    pr: close
                  conflict:
                    hold: Conflict
            """
        ).Loop!;

    [Theory]
    [InlineData("drafting", "next", "move building, then OpenDraftPullRequest")]
    [InlineData("building", "next", "move testing")]
    public void A_run_takes_the_pr_its_transition_declares(string state, string outcome, string expected) =>
        Describe(Planner.Decide(In(Renamed, state) with { Run = Ended(outcome) })).ShouldBe(expected);

    [Theory]
    [InlineData("testing", "gate:passed", "move shipping, then MarkReady")]
    [InlineData("shipping", "merge:shipped", "move finished, then Close")]
    public void A_script_takes_the_pr_its_transition_declares(string state, string verdicts, string expected) =>
        Describe(Planner.Decide(In(Renamed, state) with { Scripts = Verdicts(verdicts) })).ShouldBe(expected);

    [Fact]
    public void A_state_named_like_ours_without_pr_touches_nothing_at_the_vendor()
    {
        var bare = LoopReader.Read(
            """
            name: bare
            states:
              spec-ready:
                command: /aio:propose
                next: verifying
              verifying:
                script: gate
                on:
                  green: merging
              merging:
                script: merge
                on:
                  merged: done
            """
        ).Loop!;

        Describe(Planner.Decide(In(bare, "spec-ready") with { Run = Ended("next") })).ShouldBe("move verifying");
        Describe(Planner.Decide(In(bare, "verifying") with { Scripts = Verdicts("gate:green") })).ShouldBe("move merging");
        Describe(Planner.Decide(In(bare, "merging") with { Scripts = Verdicts("merge:merged") })).ShouldBe("move done");
    }

    static Situation In(LoopDefinition loop, string state) =>
        new(loop, Pbi(state), Me, 0, null, null, new Dictionary<string, int>(), null, false);

    // ── The photograph ─────────────────────────────────────────────────────────────────────────

    static Pbi Pbi(string? state) => new(new PbiRef("owner/repo", "7"), "El motor", state, false, null);

    static Situation At(string state) =>
        new(Loop, Pbi(state), Me, 0, null, null, new Dictionary<string, int>(), null, false);

    static AskedRunStatus Ended(string outcome) =>
        new(Guid.Empty, true, new RunEnd(outcome, "the agent said so", null, []));

    /// <summary><c>gate:green evidence:pass</c> — each script with the verdict it gave.</summary>
    static IReadOnlyList<ScriptVerdict> Verdicts(string written) =>
        [
            .. written
                .Split(' ')
                .Select(pair => pair.Split(':'))
                .Select(pair =>
                    pair[1] switch
                    {
                        "pass" => new ScriptVerdict(pair[0], VerdictKind.Pass, null, "the model said so"),
                        "wait" => new ScriptVerdict(pair[0], VerdictKind.Wait, null, "the model said so"),
                        "fail" => new ScriptVerdict(pair[0], VerdictKind.Fail, null, "the model said so"),
                        var outcome => new ScriptVerdict(pair[0], VerdictKind.Outcome, outcome, "the model said so"),
                    }
                ),
        ];

    static string Describe(Decision decision) =>
        decision switch
        {
            Ignore ignore => $"ignore {ignore.Why}",
            Wait wait => $"wait {wait.Why}",
            RunScripts scripts => $"scripts {string.Join(", ", scripts.Scripts)}",
            StartRun start => $"start {start.Provider} {start.Model} {start.Command} [{string.Join(", ", start.Outcomes)}]",
            Move { Step: VendorStep.None } move => $"move {move.To}",
            Move move => $"move {move.To}, then {move.Step}",
            Hold hold => $"hold {hold.Why}, then {hold.Next ?? "a person chooses"}",
            Release release => $"release {release.To}: {release.Why}",
            _ => decision.ToString()!,
        };
}
