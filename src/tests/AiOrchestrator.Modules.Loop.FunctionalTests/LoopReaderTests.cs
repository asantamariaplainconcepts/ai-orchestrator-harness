using AiOrchestrator.Modules.Loop.Definition;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #498. The loop format (openspec/specs/loop-format): the repository's own loop read, then each
/// rule broken once, checking the message a person would read.
/// </summary>
public sealed class LoopReaderTests
{
    static readonly string RepositoryLoop = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "loop.yaml"));

    static readonly string[] Declared =
    [
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
    ];

    static LoopDefinition Valid(string yaml)
    {
        var reading = LoopReader.Read(yaml);
        reading.Problems.ShouldBeEmpty();
        return reading.Loop.ShouldNotBeNull();
    }

    static string Problem(string yaml)
    {
        var reading = LoopReader.Read(yaml);
        reading.Loop.ShouldBeNull();
        return reading.Problems.ShouldHaveSingleItem();
    }

    static string Transitions(LoopState state) =>
        string.Join(
            " | ",
            state.Transitions.Select(to =>
                $"{to.Outcome ?? "next"} -> {to.Target ?? "?"}{(to.Hold is null ? "" : $" (hold: {to.Hold})")}{(to.Pr is VendorStep.None ? "" : $", then {to.Pr}")}"
            )
        );

    [Fact]
    public void The_repository_loop_reads_into_its_states_and_agents()
    {
        var loop = Valid(RepositoryLoop);

        loop.Name.ShouldBe("spec-driven-issue-loop");
        loop.Wip.ShouldBe(4);
        loop.Agents.Keys.ShouldBe(["refiner", "coder", "reviewer"]);
        loop.Agents["reviewer"].ShouldBe(new LoopAgent("claude-code", "claude-sonnet-5-5"));
        loop.States.Select(state => state.Name).ShouldBe(Declared);
        loop.Undeclared.ShouldBe(["needs-refinement", "done"]);

        var spec = loop.States[1];
        spec.Scripts.ShouldBe(["file-overlap", "claim"]);
        spec.Agent.ShouldBe("coder");
        spec.Command.ShouldBe("/aio:propose");
        loop.States[2].Score.ShouldBe("risk-score");
        loop.States[5].MaxLoops.ShouldBe(2);
        loop.States[9].MaxLoops.ShouldBe(1);
    }

    /// <summary>The retro goes before the merge, and a conflict goes to rebasing once.</summary>
    [Fact]
    public void The_repository_loop_validates_with_its_transitions()
    {
        var loop = Valid(RepositoryLoop);

        loop.States.Select(state => state.Name).ShouldBe(Declared);
        loop.Undeclared.ShouldBe(["needs-refinement", "done"]);
        loop.States.Select(state => $"{state.Name}: {Transitions(state)}")
            .ShouldBe([
                "ready-for-proposal: ready -> spec-ready | gaps -> needs-refinement",
                "spec-ready: next -> proposal-review, then OpenDraftPullRequest",
                "proposal-review: low -> in-progress | high -> in-progress (hold: El score de riesgo salió alto)",
                "in-progress: next -> verifying",
                "verifying: green -> code-review, then MarkReady | red -> ? (hold: El gate está en rojo) | blocked -> ? (hold: La evidencia hacía falta y no se pudo tomar) | failed -> ? (hold: Las capturas de la evidencia fallaron)",
                "code-review: rejected -> in-progress | approved -> merge-review",
                "merge-review: low -> retro | high -> retro (hold: El riesgo del diff salió alto)",
                "retro: next -> merging",
                "merging: merged -> done, then Close | conflict -> rebasing",
                "rebasing: next -> merging",
            ]);
        loop.States.Single(state => state.Name == "verifying").Scripts.ShouldBe(["gate", "evidence", "quality"]);
        loop.States.Single(state => state.Name == "rebasing").MaxLoops.ShouldBe(1);
    }

    [Fact]
    public void A_back_edge_without_max_loops_is_refused()
    {
        var yaml = RepositoryLoop.Replace(
            "    command: /aio:review-diff\n    max_loops: 2\n",
            "    command: /aio:review-diff\n",
            StringComparison.Ordinal
        );

        Problem(yaml)
            .ShouldBe("states.code-review: goes back to in-progress without max_loops; a state that can go back says how many times");
    }

    [Fact]
    public void A_hold_whose_next_goes_back_counts_as_going_back()
    {
        var yaml = RepositoryLoop.Replace(
            "        hold: El gate está en rojo\n",
            "        hold: El gate está en rojo\n        next: in-progress\n",
            StringComparison.Ordinal
        );

        Problem(yaml).ShouldStartWith("states.verifying: goes back to in-progress without max_loops");
    }

    [Fact]
    public void A_state_with_an_agent_and_a_score_is_refused()
    {
        var yaml = RepositoryLoop.Replace(
            "    score: risk-score\n",
            "    score: risk-score\n    agent: reviewer\n",
            StringComparison.Ordinal
        );

        Problem(yaml).ShouldBe("states.proposal-review: runs an agent and a score; a state runs one or the other");
    }

    [Fact]
    public void A_score_beside_a_command_for_the_default_agent_is_refused_too()
    {
        var yaml = RepositoryLoop.Replace(
            "    score: diff-risk\n",
            "    score: diff-risk\n    command: /aio:review-diff\n",
            StringComparison.Ordinal
        );

        Problem(yaml).ShouldBe("states.merge-review: runs an agent and a score; a state runs one or the other");
    }

    [Fact]
    public void A_nested_on_is_refused()
    {
        var yaml = RepositoryLoop.Replace(
            "      red:\n        hold: El gate está en rojo\n",
            "      red:\n        on:\n          flaky: verifying\n          broken: in-progress\n",
            StringComparison.Ordinal
        );

        Problem(yaml)
            .ShouldBe(
                "states.verifying.on.red: on does not nest — a state produces one outcome, and an "
                    + "outcome leads to a state or to a hold (found on)"
            );
    }

    [Fact]
    public void A_hold_without_its_reason_is_refused()
    {
        var yaml = RepositoryLoop.Replace(
            "        hold: El gate está en rojo\n",
            "        hold:\n",
            StringComparison.Ordinal
        );

        Problem(yaml).ShouldBe("states.verifying.on.red: a hold carries its reason, which is what the person reads");
    }

    [Fact]
    public void An_agent_not_declared_in_agents_is_refused()
    {
        var yaml = RepositoryLoop.Replace("    agent: reviewer\n", "    agent: auditor\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.code-review: agent 'auditor' is not declared in agents");
    }

    [Fact]
    public void A_provider_that_is_not_an_installed_cli_is_refused()
    {
        var yaml = RepositoryLoop.Replace("  reviewer:\n    provider: claude-code\n", "  reviewer:\n    provider: gemini\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("agents.reviewer: provider 'gemini' is not one of claude-code, copilot, opencode, codex");
    }

    [Fact]
    public void A_state_may_change_the_provider_and_the_model_of_its_agent()
    {
        var yaml = RepositoryLoop.Replace(
            "    command: /rebase-safely\n",
            "    command: /rebase-safely\n    provider: codex\n    model: gpt-6\n",
            StringComparison.Ordinal
        );

        var rebasing = Valid(yaml).States.Single(state => state.Name == "rebasing");
        rebasing.Provider.ShouldBe("codex");
        rebasing.Model.ShouldBe("gpt-6");
    }

    [Fact]
    public void A_key_outside_the_format_is_named()
    {
        var yaml = RepositoryLoop.Replace("    max_loops: 1\n", "    max_loop: 1\n", StringComparison.Ordinal);

        var problems = LoopReader.Read(yaml).Problems;
        problems.ShouldContain(problem => problem.StartsWith("states.rebasing.max_loop: not a key of the loop format", StringComparison.Ordinal));
    }

    [Fact]
    public void A_pr_value_outside_the_three_is_refused_like_an_unknown_key()
    {
        var yaml = RepositoryLoop.Replace("        pr: ready\n", "        pr: merge\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.verifying.on.green.pr: 'merge' is not a value of the loop format; it takes draft, ready, close");
    }

    [Fact]
    public void A_pr_beside_next_is_read_in_the_short_form_too()
    {
        var yaml = RepositoryLoop.Replace("    pr: draft\n", "    pr: drafted\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.spec-ready.pr: 'drafted' is not a value of the loop format; it takes draft, ready, close");
    }

    [Fact]
    public void A_pr_on_a_state_with_on_is_refused()
    {
        var yaml = RepositoryLoop.Replace("    max_loops: 2\n", "    max_loops: 2\n    pr: ready\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.code-review.pr: goes beside next; under on, it goes on the outcome that takes it");
    }

    [Fact]
    public void A_pr_on_a_hold_is_refused()
    {
        var yaml = RepositoryLoop.Replace(
            "        hold: El gate está en rojo\n",
            "        hold: El gate está en rojo\n        pr: close\n",
            StringComparison.Ordinal
        );

        Problem(yaml).ShouldBe("states.verifying.on.red.pr: a hold moves nothing at the vendor; pr goes on an outcome that leads to a state");
    }

    [Fact]
    public void An_outcome_mapping_with_neither_next_nor_hold_is_refused()
    {
        var yaml = RepositoryLoop.Replace("        next: done\n        pr: close\n", "        pr: close\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.merging.on.merged: leads to a state with next, or to a hold with its reason");
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var yaml = RepositoryLoop
            .Replace("    agent: reviewer\n", "    agent: auditor\n", StringComparison.Ordinal)
            .Replace("        hold: El gate está en rojo\n", "        hold:\n", StringComparison.Ordinal)
            .Replace("  reviewer:\n    provider: claude-code\n", "  reviewer:\n    provider: gemini\n", StringComparison.Ordinal);

        // The hold is a shape problem and stops the cross-state rules, which need every state read.
        LoopReader.Read(yaml).Problems.ShouldBe(["states.verifying.on.red: a hold carries its reason, which is what the person reads"]);

        var rules = RepositoryLoop
            .Replace("    agent: reviewer\n", "    agent: auditor\n", StringComparison.Ordinal)
            .Replace("  reviewer:\n    provider: claude-code\n", "  reviewer:\n    provider: gemini\n", StringComparison.Ordinal);
        LoopReader.Read(rules).Problems.Count.ShouldBe(2);
    }

    /// <summary>#595. Where a person's Request changes sends the PBI, and which tab is marked, per state.</summary>
    [Fact]
    public void The_repository_loop_declares_back_and_tab_on_its_review_states()
    {
        var loop = Valid(RepositoryLoop);

        loop.States.Where(state => state.Back is not null || state.Tab is not null)
            .Select(state => $"{state.Name}: back {state.Back}, tab {state.Tab}")
            .ShouldBe([
                "proposal-review: back spec-ready, tab proposal",
                "code-review: back in-progress, tab review",
                "merge-review: back in-progress, tab review",
            ]);
    }

    [Fact]
    public void A_back_to_a_state_the_loop_does_not_run_is_refused()
    {
        var yaml = RepositoryLoop.Replace("    back: spec-ready\n", "    back: drafting\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.proposal-review.back: 'drafting' is not a state of the loop");
    }

    [Fact]
    public void A_tab_the_PBI_screen_does_not_have_is_refused()
    {
        var yaml = RepositoryLoop.Replace("    tab: proposal\n", "    tab: design\n", StringComparison.Ordinal);

        Problem(yaml).ShouldBe("states.proposal-review.tab: 'design' is not one of journey, issue, proposal, review, files");
    }

    [Fact]
    public void Text_that_is_not_yaml_names_its_line()
    {
        Problem("name: x\nstates: [unclosed\n").ShouldStartWith("line ");
    }

    [Fact]
    public void A_loop_without_states_is_refused()
    {
        Problem("name: empty\n").ShouldBe("states: the loop declares no states");
    }
}
