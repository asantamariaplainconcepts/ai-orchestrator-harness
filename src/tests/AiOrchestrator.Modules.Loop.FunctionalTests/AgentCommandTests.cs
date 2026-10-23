using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Loop.Definition;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #519. The agent commands against the loop that runs them: a command writes its part and says its
/// outcome, and everything that touches the vendor's state is the engine's. Read where the files
/// live, <c>.ai/commands/aio/</c>, so the test checks what an agent is actually handed.
/// </summary>
public sealed partial class AgentCommandTests
{
    static readonly string Commands = Path.Combine(Root(), ".ai", "commands", "aio");

    /// <summary>What this repository adds to the package's commands (#618): the plan as an OpenSpec change, its ports, its files.</summary>
    static readonly string Context = Path.Combine(Root(), ".aio", "context.md");

    /// <summary>
    /// What the engine does at a transition, and a command doing it as well would race it: moving a
    /// label, opening, readying or merging a PR, approving it (which releases <c>merge-review</c>),
    /// claiming, and pushing telemetry. The feedback script goes with the repeated block it lived in.
    /// </summary>
    static readonly string[] Forbidden =
    [
        ".aio/aio/status",
        ".aio/steps/claim",
        ".aio/steps/feedback",
        "gh pr ready",
        "gh pr merge",
        "gh pr create",
        "--approve",
        "--verdict approve",
        "--verdict request-changes",
        "--add-label",
        "--remove-label",
        "push-session-report",
    ];

    [GeneratedRegex(@"\.aio/aio done ([a-z][a-z0-9-]*)")]
    private static partial Regex AioDone();

    [Fact]
    public void No_command_moves_a_label_a_pull_request_or_telemetry()
    {
        var found = Directory
            .EnumerateFiles(Commands, "*.md")
            .SelectMany(file =>
                File.ReadAllLines(file)
                    .Select((line, index) => (file, line, number: index + 1))
            )
            .SelectMany(at =>
                Forbidden
                    .Where(word => at.line.Contains(word, StringComparison.Ordinal))
                    .Select(word => $"{Path.GetFileName(at.file)}:{at.number} names {word}")
            )
            .ToList();

        Directory.EnumerateFiles(Commands, "*.md").Count().ShouldBe(6);
        found.ShouldBeEmpty();
    }

    /// <summary>
    /// Each state with a <c>/aio:*</c> command, against its file: the last section is <c>End</c>, it
    /// names every outcome the state's <c>on</c> knows, and no <c>aio done</c> anywhere in the file
    /// names one the state does not — <c>blocked</c> aside, which every run may say. A state with
    /// <c>next</c> knows the one outcome <c>next</c>.
    /// </summary>
    [Fact]
    public void Every_agent_state_command_ends_with_an_outcome_its_state_knows()
    {
        var loop = LoopReader.Read(File.ReadAllText(Path.Combine(Root(), ".aio", "loop.yaml"))).Loop.ShouldNotBeNull();
        var states = loop.States.Where(state => state.Command?.StartsWith("/aio:", StringComparison.Ordinal) == true).ToList();

        states.Select(state => state.Command).ShouldBe(["/aio:refine", "/aio:propose", "/aio:implement", "/aio:review-diff", "/aio:review"]);

        foreach (var state in states)
        {
            var file = Path.Combine(Commands, $"{state.Command!["/aio:".Length..]}.md");
            File.Exists(file).ShouldBeTrue($"{state.Name} runs {state.Command}, and {file} does not exist");

            var text = File.ReadAllText(file);
            var known = state.Transitions.Select(to => to.Outcome ?? "next").ToHashSet(StringComparer.Ordinal);

            var named = AioDone().Matches(text).Select(match => match.Groups[1].Value).ToList();
            named.Where(outcome => outcome != "blocked" && !known.Contains(outcome))
                .ShouldBeEmpty($"{state.Command} names outcomes {state.Name} does not know");

            var end = text[text.LastIndexOf("\n## ", StringComparison.Ordinal)..];
            end.ShouldStartWith("\n## End\n", customMessage: $"{state.Command} does not end with its End section");
            var ending = AioDone().Matches(end).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            known.Except(ending).ShouldBeEmpty($"{state.Command}'s End does not name every outcome of {state.Name}");
        }
    }

    /// <summary>
    /// #588. A change reached <c>merging</c> with three boxes open in its <c>tasks.md</c>, and only the
    /// archive caught it. The commands are the package's since #618, so their Ends say a plan step left
    /// undone is <c>blocked</c> naming it, never <c>next</c>, and <c>rejected</c> in the review; what this
    /// repository adds, <c>.aio/context.md</c>, says the same of <c>tasks.md</c>'s boxes and keeps
    /// the gate and post-merge checks out of it.
    /// </summary>
    [Fact]
    public void An_open_task_blocks_the_implement_and_rejects_the_review()
    {
        var implement = End(Path.Combine(Commands, "implement.md"));
        implement.ShouldContain("`next` is only for a branch that does every line of the plan");
        implement.ShouldContain(".aio/aio done blocked --reason \"plan step not done: ");
        End(Path.Combine(Commands, "review-diff.md")).ShouldContain("--problems \"plan step not done: <the step>\"");
        File.ReadAllText(Path.Combine(Commands, "propose.md")).ShouldContain("no check that needs the change merged");

        var context = File.ReadAllText(Context);
        context.ShouldContain("`next` is only for a `tasks.md` with every box ticked");
        context.ShouldContain(".aio/aio done blocked --reason \"open task ");
        context.ShouldContain("a box left open in `tasks.md`, whatever the task");
        context.ShouldContain("--problems \"tasks.md:<line> — open task <number>\"");
        context.ShouldContain("no task to run the gate");
        context.ShouldContain("no check that needs the change merged");
    }

    /// <summary>
    /// #590. An implement ran <c>aspire do ci</c> in its own session, its tests inherited the run's
    /// <c>AIO_DONE_FILE</c>, and one of them ended the run with <c>merged</c>. The implement command
    /// says not to run the gate, none of its command blocks runs it, and the context says it of
    /// <c>aspire do ci</c> by name.
    /// </summary>
    [Fact]
    public void Implement_does_not_run_the_gate()
    {
        var file = Path.Combine(Commands, "implement.md");
        var text = File.ReadAllText(file);
        text.ShouldContain("Do not run the gate", customMessage: file);

        var blocks = CodeBlock().Matches(text).Select(match => match.Value).ToList();
        blocks.ShouldNotBeEmpty(file);
        blocks.ShouldAllBe(block => !block.Contains("gate") && !block.Contains("aspire do ci"), file);

        File.ReadAllText(Context).ShouldContain("The implement does not run `aspire do ci` either");
    }

    /// <summary>
    /// #765. Auto mode's classifier judges a commit against what the person asked for, which is the
    /// command's text, and does not take a file the agent read as asking. The retros of #757 and #758
    /// were denied as an unrequested commit while review.md said "Commit nothing" and only
    /// <c>.aio/context.md</c> asked for one. So review.md asks for the commit itself, in a command
    /// block of the same git commands the implement's pushes run, and still commits nothing else.
    /// </summary>
    [Fact]
    public void The_review_asks_for_the_one_commit_a_retro_file_needs()
    {
        var file = Path.Combine(Commands, "review.md");
        var text = File.ReadAllText(file);

        text.ShouldNotContain("Commit nothing:", customMessage: file);
        text.ShouldContain("When `.aio/context.md` puts the retro in a file on the branch instead", customMessage: file);
        text.ShouldContain("Commit nothing else", customMessage: file);

        var commit = CodeBlock().Matches(text).Select(match => match.Value).Single(block => block.Contains("git commit"));
        commit.ShouldContain("git add <the file>");
        commit.ShouldContain("git push origin HEAD");
        commit.ShouldNotContain("--no-verify");
        File.ReadAllText(Path.Combine(Commands, "implement.md")).ShouldContain("git push origin HEAD");

        File.ReadAllText(Context).ShouldContain("`/aio:review` appends its entry to `docs/process/retro-log.md` on the branch");
    }

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex CodeBlock();

    /// <summary>
    /// #633. The Definition of Ready was a five-point list written twice, in refine and in explore. It
    /// is the repository's issue template now: both commands name it and carry no list of their own,
    /// so a repository that changes the template changes what both ask for.
    /// </summary>
    [Theory]
    [InlineData("refine.md")]
    [InlineData("explore.md")]
    public void Refine_and_explore_read_the_definition_of_ready_from_the_issue_template(string command)
    {
        var text = File.ReadAllText(Path.Combine(Commands, command));

        text.ShouldContain("read `.aio/templates/issue.md`", customMessage: command);
        text.ShouldContain("each section of the template answers what its help line asks", customMessage: command);
        foreach (var point in new[] { "What is true now", "What should be true instead", "What it is not", "five" })
        {
            text.ShouldNotContain(point, customMessage: command);
        }
        NumberedList().IsMatch(text).ShouldBeFalse(command);
    }

    /// <summary>
    /// #635. An idea too big for one change was split by a person acting as orchestrator. Explore does
    /// it now: an epic of <c>epic.md</c> whose sub-issues are of <c>issue.md</c>, cut so that as many as
    /// possible share no file, with an arrow between two that do, created only on confirmation, epic
    /// first, then each sub-issue under it, then the epic again with the real numbers. Moving them
    /// into the loop is a question of its own.
    /// </summary>
    [Fact]
    public void Explore_cuts_a_big_idea_into_an_epic_whose_sub_issues_share_few_files()
    {
        // One line, so the wrapping of the prose does not decide what the test finds.
        var text = Whitespace().Replace(File.ReadAllText(Path.Combine(Commands, "explore.md")), " ");

        text.ShouldContain("read `.aio/templates/issue.md`");
        text.ShouldContain("`.aio/templates/epic.md`");
        text.ShouldContain("as many sub-issues as possible share no file");
        text.ShouldContain("`file-overlap`");
        text.ShouldContain("`i<number>`");
        text.ShouldContain("an arrow means the second waits for the first");
        text.ShouldContain("Create only when they confirm");

        var epic = text.IndexOf("aio issue create --title \"<title>\" --body-file .aio/epic.md", StringComparison.Ordinal);
        var child = text.IndexOf("aio issue create --title \"<title>\" --parent <epic> --body-file .aio/issue.md", StringComparison.Ordinal);
        var edit = text.IndexOf("aio issue edit <epic> --body-file .aio/epic.md", StringComparison.Ordinal);
        var move = text.IndexOf("aio issue move <n> ready-for-proposal", StringComparison.Ordinal);
        epic.ShouldBeGreaterThan(0);
        child.ShouldBeGreaterThan(epic, "each sub-issue is filed under an epic that already exists");
        edit.ShouldBeGreaterThan(child, "the epic's graph gets the real numbers once the sub-issues have them");
        move.ShouldBeGreaterThan(edit, "moving into the loop is asked after everything is created");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^\d\. \*\*", RegexOptions.Multiline)]
    private static partial Regex NumberedList();

    static string End(string file)
    {
        var text = File.ReadAllText(file);
        return text[text.LastIndexOf("\n## End\n", StringComparison.Ordinal)..];
    }

    /// <summary>
    /// #575. A refine that ended <c>ready</c> without editing the body left nothing on the issue. The
    /// refiner posts one closing comment before its End, and it is the only place it asks for one, so
    /// a run that edits the body and leaves a gap posts one comment, not two.
    /// </summary>
    [Theory]
    [InlineData(".ai/commands/aio/refine.md")]
    public void The_refiner_posts_one_closing_comment_before_its_end(string path)
    {
        var text = File.ReadAllText(Path.Combine(Root(), path));
        var end = text.IndexOf("\n## End\n", StringComparison.Ordinal);

        Regex.Matches(text, "aio issue comment").Count.ShouldBe(1, $"{path} asks for a comment in more than one place");
        text.IndexOf("aio issue comment", StringComparison.Ordinal).ShouldBeLessThan(end);
    }

    /// <summary>
    /// #575, #634, #651. In <c>allow-list</c> anything the list does not name asks, and an unattended
    /// run cannot answer, so <c>aio issue</c> and <c>aio pr</c> are on the list by their path. Not by
    /// the bare name, which since #678 is also the app's .NET tool. <c>gh issue</c>, <c>gh</c> as a
    /// whole and <c>gh api</c> stay off it, and so do the <c>gh pr</c> reads the commands no longer make.
    /// </summary>
    [Fact]
    public void The_allow_list_lets_the_agent_reach_its_issue_through_aio_and_nothing_wider()
    {
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), ".claude", "settings.json")));
        var allowed = settings.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(rule => rule.GetString()!).ToList();

        allowed.ShouldContain("Bash(.aio/aio issue:*)");
        allowed.ShouldContain("Bash(.aio/aio pr:*)");
        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(aio ", StringComparison.Ordinal));
        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(gh issue", StringComparison.Ordinal));
        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(gh pr view", StringComparison.Ordinal) || rule.StartsWith("Bash(gh pr diff", StringComparison.Ordinal) || rule.StartsWith("Bash(gh pr checks", StringComparison.Ordinal));
        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(gh:", StringComparison.Ordinal) || rule.StartsWith("Bash(gh api", StringComparison.Ordinal));
    }

    /// <summary>
    /// #634, #651. The commands reach their issue through <c>aio issue</c> and its pull request through
    /// <c>aio pr</c>, which go through the folder's vendor adapter, so they read the same on any vendor.
    /// Neither the six nor this repository's context names <c>gh issue</c>, <c>gh pr</c> or <c>gh api</c>:
    /// a <c>gh api</c> in the context stopped a propose in <c>allow-list</c> (#644).
    /// </summary>
    [Fact]
    public void No_command_calls_gh()
    {
        var found = Directory
            .EnumerateFiles(Commands, "*.md")
            .Append(Context)
            .SelectMany(file => File.ReadAllLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(at => at.line.Contains("gh issue", StringComparison.Ordinal) || at.line.Contains("gh pr", StringComparison.Ordinal) || at.line.Contains("gh api", StringComparison.Ordinal))
            .Select(at => $"{Path.GetFileName(at.file)}:{at.number}")
            .ToList();

        found.ShouldBeEmpty();
        Directory
            .EnumerateFiles(Commands, "*.md")
            .Count(file => File.ReadAllText(file).Contains(".aio/aio issue ", StringComparison.Ordinal))
            .ShouldBe(6, "every command reads or writes its issue through aio issue");
        Directory
            .EnumerateFiles(Commands, "*.md")
            .Where(file => File.ReadAllText(file).Contains(".aio/aio pr ", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToList()
            .ShouldBe(["implement.md", "review-diff.md", "review.md"], "the commands that read the pull request read it through aio pr");
    }

    /// <summary>
    /// #710. The refines of aio-dogfood#30 and #31 took the body out of <c>aio issue view</c>'s JSON
    /// with <c>python3</c>, and that is what asked for a permission. The five commands of a PBI read
    /// their issue as text with <c>--markdown</c>, and every <c>aio issue view</c> they name carries it.
    /// </summary>
    [Theory]
    [InlineData("refine.md")]
    [InlineData("propose.md")]
    [InlineData("implement.md")]
    [InlineData("review-diff.md")]
    [InlineData("review.md")]
    public void A_command_reads_its_issue_as_markdown_and_needs_no_python_or_jq(string command)
    {
        var text = File.ReadAllText(Path.Combine(Commands, command));

        text.ShouldContain(".aio/aio issue view $ARGUMENTS --markdown");
        IssueView().Matches(text).ShouldAllBe(view => view.Value.EndsWith("--markdown", StringComparison.Ordinal));
        text.ShouldNotContain("python3");
        text.ShouldNotContain("jq ");
    }

    [GeneratedRegex(@"aio issue view \S+(?: --markdown)?")]
    private static partial Regex IssueView();

    /// <summary>
    /// #644, #634. Every body an agent posts is a literal path: a path in a shell variable stopped a
    /// refine at a permission prompt nobody could answer. Each is under <c>.aio/</c>, where
    /// <c>aio issue</c> removes it once posted, so the worktree stays clean for <c>cleanup</c>.
    /// </summary>
    [Fact]
    public void Every_body_file_is_a_literal_path_under_harness()
    {
        var bodies = Directory
            .EnumerateFiles(Commands, "*.md")
            .SelectMany(file => BodyFile().Matches(File.ReadAllText(file)).Select(match => match.Groups["path"].Value))
            .ToList();

        bodies.Count.ShouldBe(9, "refine's edit and comment, propose's and review's comments, explore's issue, epic, sub-issue and epic edit, review-diff's review");
        bodies.ShouldAllBe(path => path.StartsWith(".aio/", StringComparison.Ordinal) && !path.Contains('$') && !path.Contains('<'));
    }

    [GeneratedRegex(@"aio (?:issue (?:edit|comment|create)|pr review)[^`\n]*?--body-file (?<path>[^\s`]+)")]
    private static partial Regex BodyFile();

    static string Root() =>
        SelfCheckout.Root() ?? throw new DirectoryNotFoundException("These tests read this checkout's own .ai and .aio, and there is none.");
}
