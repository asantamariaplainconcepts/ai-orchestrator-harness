using System.Diagnostics;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.Scripts;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The scripts of <c>merging</c> and <c>verifying</c> (#502) — <c>archive</c>, <c>merge</c>, the gate and
/// <c>evidence</c> — run from this repository's <c>.aio/</c> against a temporary checkout
/// with a bare <c>origin</c>. Git is real; <c>gh</c>, and <c>dotnet</c> and <c>pnpm</c> where a test
/// needs them, are scripts on PATH that answer from files and write down every call.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LoopScriptTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-loop-scripts-").FullName;

    string Work => Path.Combine(_root, "work");

    string Fake => Path.Combine(_root, "fake");

    public LoopScriptTests()
    {
        Directory.CreateDirectory(Fake);
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(_root, "clone", "--quiet", "origin.git", "work");
        Git(Work, "commit", "--quiet", "--allow-empty", "-m", "Initial revision");
        Git(Work, "push", "--quiet", "origin", "HEAD:refs/heads/main");
        Git(Work, "fetch", "--quiet");
        Git(Work, "switch", "--quiet", "-c", "change/7");

        File.WriteAllText(
            Path.Combine(Fake, "gh"),
            """
            #!/usr/bin/env bash
            echo "$*" >> "$FAKE/calls"
            case "$1 $2" in
              "pr view")
                case "$*" in *mergeCommit*) cat "$FAKE/merge-commit" 2>/dev/null; exit 0 ;; *baseRefName*) echo main; exit 0 ;; esac
                echo "$(cat "$FAKE/state") $(cat "$FAKE/head") change/7"; exit 0 ;;
              "pr merge")
                [ -f "$FAKE/conflicts" ] && { echo "Pull request #7 is not mergeable: the merge commit cannot be cleanly created." >&2; exit 1; }
                [ -f "$FAKE/lands" ] && echo MERGED > "$FAKE/state"; exit 0 ;;
              "issue comment")
                [ -f "$FAKE/no-upload" ] && { echo "HTTP 422: the upload of pbi-review.1440.light.png was refused" >&2; exit 1; }
                while [ $# -gt 0 ]; do [ "$1" = --body-file ] && cp "$2" "$FAKE/body"; shift; done
                echo "https://github.com/owner/repo/issues/502#issuecomment-123"; exit 0 ;;
              "issue view")
                case "$*" in *"--json state,body,url"*) cat "$FAKE/issue-$3.json"; exit 0 ;; esac
                [ -f "$FAKE/issue-$3.md" ] && cat "$FAKE/issue-$3.md" && exit 0 ;;
            esac
            echo "fake gh: $*" >&2; exit 1
            """
        );
        File.SetUnixFileMode(
            Path.Combine(Fake, "gh"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string Calls => File.Exists(Path.Combine(Fake, "calls")) ? File.ReadAllText(Path.Combine(Fake, "calls")) : string.Empty;

    void Write(string relative, string text)
    {
        var path = Path.Combine(Work, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    void Commit(string message)
    {
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", message);
    }

    [Fact]
    public async Task Archive_refuses_a_change_with_open_tasks_and_names_each_through_the_runner()
    {
        Write("openspec/changes/thing/proposal.md", "## Why\nx\n");
        Write(
            "openspec/changes/thing/tasks.md",
            "## 1. Work\n- [x] 1.1 Done\n- [ ] 1.2 Write the \"reader\"\n  - [ ] 1.3 Test it\n"
        );
        Commit("Propose thing");
        var script = Path.Combine(Work, ".aio", "archive");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "archive"), script);
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Carry the harness");

        var result = await new LocalScriptRunner().RunAsync(
            new ScriptRequest("archive", Work, ["--base", "origin/main"]),
            CancellationToken.None
        );

        result.ExitCode.ShouldBe(1, result.Output);
        var line = result.Line!.Value;
        line.GetProperty("ok").GetBoolean().ShouldBeFalse();
        line.GetProperty("archived").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("open").EnumerateArray().Select(task => task.GetString()).ShouldBe(
            ["tasks.md:3 1.2 Write the \"reader\"", "tasks.md:4 1.3 Test it"]
        );
        Directory.Exists(Path.Combine(Work, "openspec", "changes", "thing")).ShouldBeTrue();
    }

    [Fact]
    public void Archive_without_an_OpenSpec_change_has_nothing_to_refuse()
    {
        var (code, line) = Run("archive", "--base", "origin/main");

        code.ShouldBe(0);
        line.GetProperty("ok").GetBoolean().ShouldBeTrue();
        line.GetProperty("archived").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Merge_reads_the_pull_request_and_the_branch_back_after_gh_merged()
    {
        var head = PushBranch();
        File.WriteAllText(Path.Combine(Fake, "lands"), string.Empty);

        var (code, line) = Run("merge", "7", head);

        code.ShouldBe(0, line.ToString());
        line.GetProperty("outcome").GetString().ShouldBe("merged");
        line.GetProperty("merged").GetBoolean().ShouldBeTrue();
        line.GetProperty("observed").GetBoolean().ShouldBeTrue();
        line.GetProperty("state").GetString().ShouldBe("MERGED");
        line.GetProperty("branchDeleted").GetBoolean().ShouldBeTrue();
        Git(Work, "ls-remote", "origin", "refs/heads/change/7").ShouldBeEmpty();
        Calls.ShouldContain($"pr merge 7 --squash --match-head-commit {head}");
    }

    [Fact]
    public void Merge_releases_nothing_and_says_one_sentence()
    {
        var head = PushBranch();
        File.WriteAllText(Path.Combine(Fake, "lands"), string.Empty);
        // The vendor names a merge commit, the one thing the release step waited for before #652, and
        // merge runs from a folder whose release leaves a mark if anything runs it.
        File.WriteAllText(Path.Combine(Fake, "merge-commit"), head);
        var scripts = Directory.CreateDirectory(Path.Combine(_root, "scripts")).FullName;
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "steps", "merge"), Path.Combine(scripts, "merge"));
        File.WriteAllText(Path.Combine(scripts, "release"), $"#!/usr/bin/env bash\ntouch '{Path.Combine(Fake, "released")}'\n");

        var (code, stdout, stderr) = Process(Work, "bash", [Path.Combine(scripts, "merge"), "7", head]);

        code.ShouldBe(0, stdout + stderr);
        using var document = JsonDocument.Parse(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last());
        var line = document.RootElement;
        line.EnumerateObject().Select(field => field.Name).ShouldBe(["outcome", "merged", "observed", "state", "branchDeleted", "why"]);
        line.GetProperty("why").GetString().ShouldBe($"Pull request 7 reads back as merged at {head}, and change/7 is gone from origin.");
        File.Exists(Path.Combine(Fake, "released")).ShouldBeFalse("merge ran release");
        Calls.ShouldNotContain("mergeCommit");
    }

    [Fact]
    public void Merge_that_gh_accepted_but_the_vendor_does_not_show_is_not_claimed()
    {
        var head = PushBranch();

        var (code, line) = Run("merge", "7", head);

        code.ShouldBe(1);
        // No outcome: a failure the loop cannot route holds the PBI with the why.
        line.TryGetProperty("outcome", out _).ShouldBeFalse();
        line.GetProperty("merged").GetBoolean().ShouldBeFalse();
        line.GetProperty("observed").GetBoolean().ShouldBeFalse();
        line.GetProperty("state").GetString().ShouldBe("OPEN");
        Git(Work, "ls-remote", "origin", "refs/heads/change/7").ShouldNotBeEmpty();
    }

    [Fact]
    public void Merge_that_gh_cannot_make_cleanly_answers_conflict()
    {
        var head = PushBranch();
        File.WriteAllText(Path.Combine(Fake, "conflicts"), string.Empty);

        var (code, line) = Run("merge", "7", head);

        code.ShouldBe(1);
        line.GetProperty("outcome").GetString().ShouldBe("conflict", line.ToString());
        line.GetProperty("merged").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString()!.ShouldContain("not mergeable");
    }

    [Fact]
    public void Merge_refuses_a_head_that_moved_after_approval_and_sends_nothing_to_gh()
    {
        var approved = PushBranch();
        Write("src/thing.txt", "more work after the approval\n");
        Commit("Push again after the approval");
        Git(Work, "push", "--quiet", "origin", "HEAD:refs/heads/change/7");
        var moved = Git(Work, "rev-parse", "HEAD");
        File.WriteAllText(Path.Combine(Fake, "head"), moved);
        File.WriteAllText(Path.Combine(Fake, "lands"), string.Empty);

        var (code, line) = Run("merge", "7", approved);

        code.ShouldBe(1);
        line.TryGetProperty("outcome", out _).ShouldBeFalse();
        line.GetProperty("merged").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe(
            $"Pull request 7's head moved to {moved} after {approved} was approved. Approve the new head; nothing was merged."
        );
        Calls.ShouldNotContain("pr merge");
        Git(Work, "ls-remote", "origin", "refs/heads/change/7").ShouldStartWith(moved);
    }

    /// <summary>
    /// Two clones of one origin run <c>claim 42</c> at once, on one machine and under one git identity:
    /// the closest two claimants can be, since nothing but the clone tells their claim commits apart.
    /// </summary>
    [Fact]
    public async Task Two_clones_claiming_one_issue_at_once_get_one_claim_and_one_refusal()
    {
        var other = Path.Combine(_root, "other");
        Git(_root, "clone", "--quiet", "origin.git", "other");
        Git(Work, "switch", "--quiet", "--detach", "origin/main");
        Git(other, "switch", "--quiet", "--detach", "origin/main");

        var answers = await Task.WhenAll(
            Task.Run(() => RunIn(Work, "claim", ["42"])),
            Task.Run(() => RunIn(other, "claim", ["42"]))
        );

        answers.Count(answer => answer.Line.GetProperty("claimed").GetBoolean())
            .ShouldBe(1, string.Join(" | ", answers.Select(answer => answer.Line.ToString())));
        var winner = answers.Single(answer => answer.Line.GetProperty("claimed").GetBoolean());
        var loser = answers.Single(answer => !answer.Line.GetProperty("claimed").GetBoolean());
        winner.Code.ShouldBe(0);
        winner.Line.GetProperty("why").GetString().ShouldBe("change/42 created at origin.");
        loser.Code.ShouldBe(1);
        loser.Line.GetProperty("why").GetString().ShouldBe(
            "change/42 already exists at origin, and not from this clone: someone else holds #42."
        );
        Git(Work, "ls-remote", "origin", "refs/heads/change/42").ShouldNotBeEmpty();
    }

    /// <summary>
    /// #741. The claim's commit is made with <c>commit-tree</c>, which takes its committer from the
    /// environment the engine starts every script with: on GitHub it reads as the claim's.
    /// </summary>
    [Fact]
    public void The_claims_commit_is_committed_by_the_claim()
    {
        var (code, line) = Run("claim", ["42", "origin/main"], CommitMark.Script("claim"));

        code.ShouldBe(0, line.ToString());
        Git(Work, "log", "-1", "--format=%cn <%ce>", "origin/change/42").ShouldBe("aio script claim <noreply@aio.invalid>");
        Git(Work, "log", "-1", "--format=%an", "origin/change/42").ShouldBe("Loop script test", "the author is left alone");
    }

    /// <summary>
    /// #741. A rebase writes its own committer on every commit it replays, and in the loop that is the
    /// <c>rebasing</c> run's: with <c>keep-committer</c> run after each, a run's commit stays the run's and a
    /// person's stays without a mark.
    /// </summary>
    [Fact]
    public void A_rebase_with_keep_committer_leaves_each_commit_its_committer()
    {
        Write("src/run.txt", "the run's work\n");
        Git(Work, "add", "src/run.txt");
        Process(Work, "git", ["commit", "--quiet", "-m", "The run's commit"], CommitMark.Run(Guid.Parse("00000000-0000-0000-0000-0000000000aa")));
        Write("src/person.txt", "the person's work\n");
        Commit("The person's commit");
        Git(Work, "switch", "--quiet", "-c", "upstream", "origin/main");
        Write("README.md", "the trunk moved\n");
        Commit("The trunk moves");
        Git(Work, "switch", "--quiet", "change/7");

        var (code, stdout, stderr) = Process(
            Work,
            "git",
            ["rebase", "--quiet", "-x", Path.Combine(ThisRepository.Root(), "scripts", "keep-committer"), "upstream"],
            CommitMark.Run(Guid.Parse("00000000-0000-0000-0000-0000000000bb"))
        );

        code.ShouldBe(0, stdout + stderr);
        Git(Work, "log", "--format=%cn|%an|%s", "upstream..change/7").Split('\n').ShouldBe(
            [
                "Loop script test|Loop script test|The person's commit",
                "aio run 00000000-0000-0000-0000-0000000000aa|Loop script test|The run's commit",
            ]
        );
        Git(Work, "merge-base", "--is-ancestor", "upstream", "change/7").ShouldBeEmpty("the branch is on the moved trunk");
    }

    [Fact]
    public void Cleanup_refuses_a_worktree_with_uncommitted_work_and_leaves_it_on_disk()
    {
        var worktree = Path.Combine(Work, ".worktrees", "change-9");
        Git(Work, "worktree", "add", "--quiet", "-b", "change/9", worktree);
        File.WriteAllText(Path.Combine(worktree, "unsaved.txt"), "the run's last edit\n");

        var (code, line) = Run("cleanup", "9", "--worktree-only");

        code.ShouldBe(1);
        line.GetProperty("removed").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString()!.ShouldContain("contains modified or untracked files");
        File.ReadAllText(Path.Combine(worktree, "unsaved.txt")).ShouldBe("the run's last edit\n");
        Git(Work, "worktree", "list").ShouldContain(worktree);
    }

    /// <summary>
    /// #697. The orphan branch of recordings sits at origin beside the claims, and a local copy of it
    /// beside the PBI's branch: <c>claim</c>, <c>merge</c> and <c>cleanup</c> only ever name
    /// <c>change/&lt;n&gt;</c>, so none of them deletes it or reads it as a claim.
    /// </summary>
    [Fact]
    public void Claim_merge_and_cleanup_leave_the_evidence_branch_alone()
    {
        Git(Work, "switch", "--quiet", "--orphan", "aio/evidence");
        Write("7/0123456789abcdef0123456789abcdef/session.cast", "{\"version\": 2, \"width\": 80, \"height\": 6}\n");
        Commit("7 · in-progress · 0123456789abcdef0123456789abcdef");
        Git(Work, "push", "--quiet", "origin", "aio/evidence");
        var evidence = Git(Work, "rev-parse", "aio/evidence");
        Git(Work, "switch", "--quiet", "change/7");

        var head = PushBranch();
        File.WriteAllText(Path.Combine(Fake, "lands"), string.Empty);
        var (claimed, claim) = Run("claim", "42", "origin/main");
        var (merged, merge) = Run("merge", "7", head);
        Git(Work, "worktree", "add", "--quiet", "-b", "change/9", Path.Combine(Work, ".worktrees", "change-9"), "origin/main");
        Git(Work, "push", "--quiet", "origin", "change/9");
        var (cleaned, cleanup) = Run("cleanup", "9");

        claimed.ShouldBe(0, claim.ToString());
        merged.ShouldBe(0, merge.ToString());
        cleaned.ShouldBe(0, cleanup.ToString());
        Git(Work, "ls-remote", "origin", "refs/heads/aio/evidence").ShouldStartWith(evidence);
        Git(Work, "rev-parse", "refs/heads/aio/evidence").ShouldBe(evidence);
        Git(Work, "ls-remote", "origin", "refs/heads/change/*").ShouldNotContain("aio/evidence");
        Git(Work, "ls-remote", "origin", "refs/heads/change/*").ShouldContain("refs/heads/change/42");
        Git(Work, "ls-remote", "origin", "refs/heads/change/7").ShouldBeEmpty();
        cleanup.GetProperty("branchDeleted").GetBoolean().ShouldBeTrue(cleanup.ToString());
        Directory.Exists(Path.Combine(Work, ".worktrees", "change-9")).ShouldBeFalse();
    }

    /// <summary>
    /// #709. What to capture is the evidence in the repository's own <c>.aio/config.json</c>: without it, and with
    /// the package's example as it ships, every line commented, nothing runs and the why says so. #733:
    /// nothing was asked for, so it is skipped and exits 0.
    /// </summary>
    [Fact]
    public void Evidence_without_its_configuration_is_skipped_and_says_so()
    {
        Write("src/frontend/features/pbi/tabs/ReviewTab.tsx", "export {};\n");
        Commit("Touch the Review tab");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("skipped");
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe("Nothing published: this repository has no .aio/config.json, so it says nothing to capture.");

        Write(".aio/config.json", File.ReadAllText(Path.Combine(ThisRepository.Root(), "src", "modules", "Workspace", "AiOrchestrator.Modules.Workspace", "Features", "Harness", "Package", "config.json")));
        Commit("Install the package's example");

        (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("skipped");
        line.GetProperty("why").GetString().ShouldBe("Nothing published: .aio/config.json (evidence) names no paths.");
        Calls.ShouldNotContain("pr view");
        Calls.ShouldNotContain("issue comment");
    }

    [Fact]
    public void Evidence_without_a_frontend_change_is_skipped_and_says_so()
    {
        OwnConfiguration();
        Write("README.md", "changed\n");
        Commit("Touch the readme");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("skipped");
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe("Nothing published: nothing in src/frontend/** changed against origin/main.");
        Calls.ShouldNotContain("dotnet");
        Calls.ShouldNotContain("issue comment");
    }

    /// <summary>
    /// #598, and #709 with the evidence in this repository's own <c>config.json</c>: a change inside
    /// <c>features/pbi/</c> installs the frontend when it has no <c>node_modules</c>, runs
    /// ScreenCaptureTests and attaches only the PBI's screens to one comment that opens with the marker
    /// the Review tab looks for.
    /// </summary>
    [Fact]
    public void Evidence_attaches_the_screens_the_change_touched_to_one_comment()
    {
        OwnConfiguration();
        Captures();
        Write("src/frontend/features/pbi/tabs/ReviewTab.tsx", "export {};\n");
        Commit("Touch the Review tab");
        var head = Git(Work, "rev-parse", "HEAD");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(0, line.ToString());
        line.GetProperty("outcome").GetString().ShouldBe("passed");
        line.GetProperty("published").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("comment").GetString().ShouldBe("https://github.com/owner/repo/issues/502#issuecomment-123");
        line.GetProperty("why").GetString().ShouldBe($"Published 1 capture of {head[..7]} in https://github.com/owner/repo/issues/502#issuecomment-123.");
        Calls.ShouldContain("pnpm --dir src/frontend install --frozen-lockfile");
        Calls.ShouldContain("dotnet test src/tests/AiOrchestrator.EndToEndTests --filter FullyQualifiedName~ScreenCaptureTests");
        var comment = Calls.Split('\n').Single(call => call.StartsWith("issue comment 502", StringComparison.Ordinal));
        comment.ShouldContain("/src/artifacts/design/screens/pbi-review.1440.light.png#pbi-review 1440x900 light");
        comment.ShouldNotContain("inbox");
        File.ReadAllText(Path.Combine(Fake, "body")).ShouldStartWith($"<!-- aio:evidence {head} -->\n");
    }

    /// <summary>#733. The captures were asked for and gh could not take them: blocked, exit 1.</summary>
    [Fact]
    public void Evidence_that_gh_cannot_attach_is_blocked_and_says_why()
    {
        OwnConfiguration();
        Captures();
        File.WriteAllText(Path.Combine(Fake, "no-upload"), string.Empty);
        Write("src/frontend/shared/styles.css", "body {}\n");
        Commit("Touch every screen");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(1);
        line.GetProperty("outcome").GetString().ShouldBe("blocked");
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("comment").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("why").GetString().ShouldBe("Nothing published: gh could not attach: HTTP 422: the upload of pbi-review.1440.light.png was refused");
        // Outside the feature folders: every screen was sent.
        Calls.ShouldContain("inbox 1440x900 light");
    }

    /// <summary>#733. The repository's capture ran and its tests failed: failed, exit 1.</summary>
    [Fact]
    public void Evidence_whose_captures_fail_is_failed_and_says_which_test_failed()
    {
        OwnConfiguration();
        Captures();
        File.WriteAllText(Path.Combine(Fake, "red"), string.Empty);
        Write("src/frontend/features/pbi/tabs/ReviewTab.tsx", "export {};\n");
        Commit("Touch the Review tab");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(1);
        line.GetProperty("outcome").GetString().ShouldBe("failed");
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe(
            "Nothing published: the capture failed: Failed AiOrchestrator.EndToEndTests.ScreenCaptureTests.Every_route_is_captured [3 s]"
        );
        Calls.ShouldNotContain("issue comment");
    }

    /// <summary>
    /// #709. Another repository's app under Aspire: a change outside <c>paths</c> starts nothing; a
    /// change inside starts the AppHost isolated, shoots each route in both themes at both widths
    /// against the resource's http URL, with <c>tomorrow</c> as a date, stops the AppHost and attaches
    /// the eight captures to one comment.
    /// </summary>
    [Fact]
    public void Evidence_starts_an_aspire_app_and_shoots_each_route_in_both_themes_at_both_widths()
    {
        Shots();
        Write(".aio/config.json", Evidence("""
            {
              "paths": ["frontend/**"],
              "start": {"aspire": "App.AppHost/App.AppHost.csproj", "resource": "webfrontend"},
              "routes": ["/", "/?day=tomorrow"]
            }
            """));
        Write("Server/Program.cs", "// changed\n");
        Commit("Touch the server");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("skipped");
        line.GetProperty("why").GetString().ShouldBe("Nothing published: nothing in frontend/** changed against origin/main.");
        Calls.ShouldNotContain("aspire");

        Write("frontend/src/App.tsx", "export {};\n");
        Commit("Touch the frontend");
        var head = Git(Work, "rev-parse", "HEAD");

        (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("passed");
        line.GetProperty("why").GetString().ShouldBe($"Published 8 captures of {head[..7]} in https://github.com/owner/repo/issues/502#issuecomment-123.");
        var apphost = Path.Combine(Git(Work, "rev-parse", "--show-toplevel"), "App.AppHost", "App.AppHost.csproj");
        var calls = Calls.Split('\n');
        calls.ShouldContain($"aspire start --isolated --apphost {apphost} --non-interactive --nologo");
        calls.ShouldContain(call => call.StartsWith("aspire describe webfrontend --format Json", StringComparison.Ordinal));
        calls.Count(call => call.Contains(" screenshot ", StringComparison.Ordinal)).ShouldBe(8);
        calls.ShouldContain("npx --yes playwright screenshot --color-scheme dark --viewport-size 390,844 --wait-for-timeout 1500 http://localhost:7000/ " + Shot(calls, "0.390.dark"));
        var day = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var comment = calls.Single(call => call.StartsWith("issue comment 502", StringComparison.Ordinal));
        comment.ShouldContain($"#/?day={day} 1440x900 light");
        comment.ShouldContain("#/ 390x844 dark");
        // Stopped after the shots, before the comment.
        Array.FindIndex(calls, call => call.StartsWith("aspire stop", StringComparison.Ordinal)).ShouldBeGreaterThan(
            Array.FindLastIndex(calls, call => call.Contains(" screenshot ", StringComparison.Ordinal))
        );
        File.ReadAllText(Path.Combine(Fake, "body")).ShouldStartWith($"<!-- aio:evidence {head} -->\n");
    }

    /// <summary>
    /// #733. The change asked for captures and the app did not start: blocked, exit 1, with aspire's
    /// own words, and nothing is shot or published.
    /// </summary>
    [Fact]
    public void Evidence_whose_app_does_not_start_is_blocked()
    {
        Shots();
        Executable("aspire", """
            #!/usr/bin/env bash
            echo "aspire $*" >> "$FAKE/calls"
            [ "$1" = start ] && { echo "error: the AppHost failed to build." >&2; exit 4; }
            exit 0
            """);
        Write(".aio/config.json", Evidence("""
            {
              "paths": ["frontend/**"],
              "start": {"aspire": "App.AppHost/App.AppHost.csproj", "resource": "webfrontend"},
              "routes": ["/"]
            }
            """));
        Write("frontend/src/App.tsx", "export {};\n");
        Commit("Touch the frontend");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(1);
        line.GetProperty("outcome").GetString().ShouldBe("blocked");
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe("Nothing published: aspire start failed: error: the AppHost failed to build.");
        Calls.ShouldNotContain(" screenshot ");
        Calls.ShouldNotContain("issue comment");
    }

    /// <summary>
    /// #709. An app started by a command runs in the background until its URL answers, and is gone
    /// once the shots are taken.
    /// </summary>
    [Fact]
    public void Evidence_starts_a_command_until_its_url_answers_and_stops_it()
    {
        Shots();
        Executable("curl", """
            #!/usr/bin/env bash
            echo "curl $*" >> "$FAKE/calls"
            [ -f "$FAKE/serving" ]
            """);
        Write(".aio/config.json", Evidence($$"""
            // An app of its own.
            {
              "paths": ["web/**/*.ts"],
              "start": {"command": "echo $$ > '{{Fake}}/pid'; sleep 1; touch '{{Fake}}/serving'; sleep 60", "url": "http://localhost:8080"},
              "routes": ["/"]
            }
            """));
        Write("web/src/app.ts", "export {};\n");
        Commit("Touch the app");

        var (code, line) = Run("evidence", "502");

        code.ShouldBe(0);
        line.GetProperty("outcome").GetString().ShouldBe("passed");
        line.GetProperty("published").GetBoolean().ShouldBeTrue(line.ToString());
        Calls.ShouldContain("curl --silent --output /dev/null --max-time 2 http://localhost:8080");
        Calls.Split('\n').Count(call => call.Contains(" screenshot ", StringComparison.Ordinal)).ShouldBe(4);
        var pid = int.Parse(File.ReadAllText(Path.Combine(Fake, "pid")).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        for (var tries = 0; tries < 50 && Alive(pid); tries++)
        {
            Thread.Sleep(100);
        }
        Alive(pid).ShouldBeFalse($"the app's shell {pid} outlived evidence");
    }

    static bool Alive(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    static string Shot(string[] calls, string name) =>
        calls.Single(call => call.Contains(" screenshot ", StringComparison.Ordinal) && call.EndsWith($"{name}.png", StringComparison.Ordinal)).Split(' ')[^1];

    /// <summary>A <c>config.json</c> whose only key is <c>evidence</c>, as the repository would write it.</summary>
    static string Evidence(string json) => $"{{\n\"evidence\":\n{json}\n}}\n";

    /// <summary>This repository's own <c>.aio/config.json</c>, committed in the checkout.</summary>
    void OwnConfiguration()
    {
        Write(".aio/config.json", File.ReadAllText(Path.Combine(ThisRepository.Root(), ".aio", "config.json")));
        Commit("Say what to capture");
    }

    /// <summary>An <c>aspire</c> whose resource serves on 7000, and an <c>npx</c> whose screenshot writes the PNG it names.</summary>
    void Shots()
    {
        Executable("aspire", """
            #!/usr/bin/env bash
            echo "aspire $*" >> "$FAKE/calls"
            [ "$1" = describe ] && echo '{"resources":[{"name":"webfrontend-x","urls":[{"name":"https","url":"https://localhost:7001"},{"name":"http","url":"http://localhost:7000"}]}]}'
            exit 0
            """);
        Executable("npx", """
            #!/usr/bin/env bash
            echo "npx $*" >> "$FAKE/calls"
            [ "$3" = screenshot ] && printf png > "${@: -1}"
            exit 0
            """);
    }

    /// <summary>
    /// A <c>pnpm</c> that installs and a <c>dotnet</c> that plays ScreenCaptureTests: two screens with
    /// their sidecars, the PBI's Review tab and the inbox, or a failed run when <c>$FAKE/red</c> exists.
    /// </summary>
    void Captures()
    {
        Executable("pnpm", """
            #!/usr/bin/env bash
            echo "pnpm $*" >> "$FAKE/calls"
            mkdir -p src/frontend/node_modules
            """);
        Executable("dotnet", """
            #!/usr/bin/env bash
            echo "dotnet $*" >> "$FAKE/calls"
            if [ -f "$FAKE/red" ]; then
              echo "  Failed AiOrchestrator.EndToEndTests.ScreenCaptureTests.Every_route_is_captured [3 s]"
              echo "Test Run Failed."; exit 1
            fi
            out=src/artifacts/design/screens; mkdir -p "$out"
            shot() {
              printf 'png' > "$out/$1.1440.light.png"
              echo "{\"screen\":\"$1\",\"module\":\"$2\",\"viewport\":{\"width\":1440,\"height\":900},\"theme\":\"light\"}" > "$out/$1.1440.light.json"
            }
            shot pbi-review features/pbi/tabs/ReviewTab
            shot inbox features/inbox/InboxScreen
            """);
    }

    void Executable(string name, string text)
    {
        var path = Path.Combine(Fake, name);
        File.WriteAllText(path, text);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>The body of #575 as /aio:refine left it on 2026-10-09: its path sits inside a sentence
    /// under a bold lead-in, not in a Seam section (#576).</summary>
    const string Issue575 = """
        **Qué es cierto ahora.** El comando `/aio:refine` corre sobre una issue y termina con `.aio/aio done ready` o `gaps`. `.ai/commands/aio/refine.md` solo pide comentar en dos casos: cuando el run edita el cuerpo (línea 33, «say in one comment what you added») y cuando queda un gap para una persona (línea 35). Un run que termina en `done ready` sin tocar el cuerpo no deja nada en la issue: el cierre queda solo en la bitácora del loop y en la memoria del run, y `gh issue view <n> --comments` sale vacío.

        **Qué debería ser cierto.** Todo run del refiner, sea cual sea su resultado (`ready` o `gaps`), deja un comentario de cierre en la issue: qué gaps se cerraron editando el cuerpo y cuáles quedan para una persona (o que no hacía falta cerrar ninguno). Así, quien abra la issue no tiene que adivinar por dónde pasó el estado.

        **Dónde cae.** En el propio comando `.ai/commands/aio/refine.md` (y en lo que haga falta del módulo del loop para que el comentario salga); no toca ninguno de los cinco puertos.

        **El check.** Se lanza el refiner sobre una issue que ya cumple el DoR y cuyo cuerpo no hay que editar. Cuando termina en `done ready` y la issue pasa a `status:ready-for-proposal`, `gh issue view <n> --comments` muestra un comentario de cierre del run, además de las líneas de bitácora de siempre. Un run que termina en `gaps` sigue dejando un único comentario dirigido a quien abrió la issue, no dos.

        **Qué no es.** No cambia cómo el motor mueve la issue entre estados, ni el formato del DoR, ni los demás comandos. Es una prueba del flujo completo del loop: que una issue pequeña pase de refine a merge sin holds.
        """;

    [Fact]
    public void File_overlap_finds_the_paths_of_575_and_clears_it_when_nothing_else_touches_them()
    {
        File.WriteAllText(Path.Combine(Fake, "issue-575.md"), Issue575);
        // gh answers a pull request's number as an issue: an open prototype on change/457 is not in flight.
        Claim(457, """{"state": "OPEN", "body": "", "url": "https://github.com/owner/repo/pull/457"}""");

        var (code, line) = Run("file-overlap", "575");

        code.ShouldBe(0);
        line.TryGetProperty("wait", out _).ShouldBeFalse();
        line.GetProperty("clear").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("hold").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe("Nothing else is in flight.");
    }

    [Fact]
    public void File_overlap_keeps_575_waiting_while_another_issue_in_flight_names_its_file()
    {
        File.WriteAllText(Path.Combine(Fake, "issue-575.md"), Issue575);
        Claim(
            576,
            """{"state": "OPEN", "body": "## Where it lands\n\n`.ai/commands/aio/refine.md`\n`.aio/context.md`\n", "url": "https://github.com/owner/repo/issues/576"}"""
        );

        var (code, line) = Run("file-overlap", "575");

        code.ShouldBe(0);
        line.GetProperty("wait").GetBoolean().ShouldBeTrue();
        line.GetProperty("clear").GetBoolean().ShouldBeFalse();
        line.GetProperty("hold").GetBoolean().ShouldBeFalse();
        line.GetProperty("why").GetString().ShouldBe("#576 is in flight and declares .ai/commands/aio/refine.md too.");
    }

    [Theory]
    [InlineData("**What is true now.** The page takes 4 s.\n\n**The check.** Open it.\n")]
    [InlineData("## Where it lands\n\nThe refine command, somewhere.\n")]
    public void File_overlap_holds_an_issue_whose_files_cannot_be_compared(string body)
    {
        File.WriteAllText(Path.Combine(Fake, "issue-575.md"), body);

        var (code, line) = Run("file-overlap", "575");

        // A hold exits 1, so the engine holds the PBI with the why.
        code.ShouldBe(1);
        line.TryGetProperty("wait", out _).ShouldBeFalse();
        line.GetProperty("clear").GetBoolean().ShouldBeFalse();
        line.GetProperty("hold").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("why").GetString()!.ShouldContain("cannot be compared");
    }

    // #655. The message names the section as the repository's installed template does, so an issue
    // written in Spanish is told to add Dónde cae; without a template it gets both names.
    [Theory]
    [InlineData("## Qué pasa hoy\n\n<!-- Lo que es cierto. -->\n\n## Dónde cae\n\n<!-- Un path por línea. -->\n", "#575 has no Dónde cae section")]
    [InlineData("## What is true now\n\n## Where it lands\n", "#575 has no Where it lands section")]
    [InlineData(null, "#575 has no Dónde cae / Where it lands section")]
    public void File_overlap_names_the_section_as_the_installed_template_does(string? template, string expected)
    {
        if (template is not null)
        {
            Write(".aio/templates/issue.md", template);
        }
        File.WriteAllText(Path.Combine(Fake, "issue-575.md"), "**What is true now.** The page takes 4 s.\n");

        var (code, line) = Run("file-overlap", "575");

        code.ShouldBe(1);
        line.GetProperty("why").GetString().ShouldBe($"{expected}, so its files cannot be compared. /aio:refine adds one.");
    }

    // #655. Against a Claude Code configuration of the test's own, never the person's: two trusted
    // folders that are gone, one trusted that is on disk, one gone but never trusted, and keys beside.
    [Fact]
    public void Trust_sweep_lists_only_the_trusted_folders_that_are_gone_and_removes_only_those()
    {
        var config = Directory.CreateDirectory(Path.Combine(_root, "claude-config")).FullName;
        var file = Path.Combine(config, ".claude.json");
        var here = Directory.CreateDirectory(Path.Combine(_root, "kept")).FullName;
        var gone = Path.Combine(_root, "gone", "checkout");
        var scratch = Path.Combine(_root, "scratchpad", "measure-repo");
        var untrusted = Path.Combine(_root, "never-trusted");
        var before = new System.Text.Json.Nodes.JsonObject
        {
            ["numStartups"] = 41,
            ["oauthAccount"] = new System.Text.Json.Nodes.JsonObject { ["emailAddress"] = "person@example.test" },
            ["projects"] = new System.Text.Json.Nodes.JsonObject
            {
                [here] = new System.Text.Json.Nodes.JsonObject { ["hasTrustDialogAccepted"] = true, ["allowedTools"] = new System.Text.Json.Nodes.JsonArray("Bash") },
                [gone] = new System.Text.Json.Nodes.JsonObject { ["hasTrustDialogAccepted"] = true },
                [scratch] = new System.Text.Json.Nodes.JsonObject { ["hasTrustDialogAccepted"] = true, ["lastSessionId"] = "a7c1" },
                [untrusted] = new System.Text.Json.Nodes.JsonObject { ["hasTrustDialogAccepted"] = false },
            },
        }.ToJsonString();
        File.WriteAllText(file, before);
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var variables = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = config };

        var (code, line) = Run("trust-sweep", [], variables);

        code.ShouldBe(0, line.ToString());
        line.GetProperty("file").GetString().ShouldBe(file);
        line.GetProperty("trusted").GetInt32().ShouldBe(3);
        line.GetProperty("gone").GetInt32().ShouldBe(2);
        line.GetProperty("removed").GetInt32().ShouldBe(0);
        line.GetProperty("paths").EnumerateArray().Select(path => path.GetString()).ShouldBe([gone, scratch]);
        File.ReadAllText(file).ShouldBe(before, "without --remove nothing is written");

        (code, line) = Run("trust-sweep", ["--remove"], variables);

        code.ShouldBe(0, line.ToString());
        line.GetProperty("removed").GetInt32().ShouldBe(2);
        line.GetProperty("why").GetString().ShouldBe("Removed 2 trust entries for folders that are gone; 1 remain.");
        var after = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
        var expected = System.Text.Json.Nodes.JsonNode.Parse(before)!;
        expected["projects"]!.AsObject().Remove(gone);
        expected["projects"]!.AsObject().Remove(scratch);
        System.Text.Json.Nodes.JsonNode.DeepEquals(after, expected).ShouldBeTrue(after.ToJsonString());
        File.GetUnixFileMode(file).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(config).ShouldBe([file], "no temporary file is left beside it");

        (code, line) = Run("trust-sweep", ["--remove"], variables);
        line.GetProperty("gone").GetInt32().ShouldBe(0);
        line.GetProperty("why").GetString().ShouldBe("All 1 trusted folders are on disk.");
    }

    /// <summary>
    /// #590. An implement ran <c>aspire do ci</c> inside its own session, the tests inherited the run's
    /// <c>AIO_DONE_FILE</c>, and one of them wrote <c>merged</c> into that run's end record. The gate
    /// starts <c>aspire</c> without it, so a run that calls the gate cannot be ended by its tests.
    /// </summary>
    [Fact]
    public void Gate_does_not_pass_the_run_end_record_to_aspire()
    {
        Write("src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj", "<Project />\n");
        File.WriteAllText(
            Path.Combine(Fake, "aspire"),
            """
            #!/usr/bin/env bash
            echo "${AIO_DONE_FILE-unset}" > "$FAKE/aspire-saw"
            echo "1/1 steps succeeded"
            """
        );
        File.SetUnixFileMode(
            Path.Combine(Fake, "aspire"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
        var record = Path.Combine(_root, "done.json");
        File.WriteAllText(record, "the run's record\n");

        var (code, line) = Run("gate", ["590"], new Dictionary<string, string> { ["AIO_DONE_FILE"] = record });

        code.ShouldBe(0, line.ToString());
        line.GetProperty("outcome").GetString().ShouldBe("green");
        line.GetProperty("go").GetBoolean().ShouldBeTrue();
        File.ReadAllText(Path.Combine(Fake, "aspire-saw")).Trim().ShouldBe("unset");
        File.ReadAllText(record).ShouldBe("the run's record\n");
    }

    /// <summary>
    /// #638. The package's stub (<c>src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/gate</c>), with its <c>aspire do ci</c> line uncommented, keeps
    /// the command's whole output in a log it names in the why, with what <c>aspire</c> counted and the
    /// first failing line: on 11 October a dogfood PBI held on "aspire do ci failed" and nothing said what
    /// had failed.
    /// </summary>
    [Fact]
    public void Package_gate_stub_keeps_the_commands_output_in_a_log_the_why_names()
    {
        Executable("aspire", """
            #!/usr/bin/env bash
            echo "aspire $*" >> "$FAKE/calls"
            echo "12:01:01 ✓ ci-build succeeded"
            echo "12:01:02 ✗ ci-test failed: 3 tests \"failed\""
            echo "9/10 steps succeeded"
            exit 1
            """);
        var stub = File.ReadAllText(Path.Combine(ThisRepository.Root(), "src", "modules", "Workspace", "AiOrchestrator.Modules.Workspace", "Features", "Harness", "Package", "gate"))
            .Replace("# run aspire do ci --apphost <the AppHost .csproj>", "run aspire do ci --apphost src/App.csproj", StringComparison.Ordinal);
        Write(".aio/gate", stub);
        File.SetUnixFileMode(Path.Combine(Work, ".aio", "gate"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Commit("Carry the package's gate");
        var head = Git(Work, "rev-parse", "--short", "HEAD");

        // Run resolves a rooted script path as given, so this runs the checkout's gate and not this repository's.
        var (code, line) = Run(Path.Combine(Work, ".aio", "gate"), ["7"], new Dictionary<string, string> { ["TMPDIR"] = _root });

        code.ShouldBe(1, line.ToString());
        line.GetProperty("outcome").GetString().ShouldBe("red");
        line.GetProperty("go").GetBoolean().ShouldBeFalse();
        var why = line.GetProperty("why").GetString()!;
        why.ShouldStartWith("aspire do ci failed (exit 1): ✗ ci-test failed: 3 tests  failed  (9/10 steps succeeded). Log: ");
        var log = why[(why.IndexOf("Log: ", StringComparison.Ordinal) + 5)..];
        Path.GetFileName(log).ShouldStartWith($"aio-gate-{head}-");
        Path.GetDirectoryName(log).ShouldBe(_root);
        File.ReadAllText(log).ShouldBe("12:01:01 ✓ ci-build succeeded\n12:01:02 ✗ ci-test failed: 3 tests \"failed\"\n9/10 steps succeeded\n");
        Calls.ShouldContain("aspire do ci --apphost src/App.csproj");
    }

    const string NuGetKey = "oy2-the-persons-key-4f1c";

    /// <summary>
    /// #681, #724. <c>release</c> asks the feed, packs, pushes to nuget.org and tags as soon as the push is
    /// accepted: the fake nuget.org never lists the push, and the feed is not asked again after it. Every
    /// fake echoes the key it was handed, so the key reaches a line only if the script lets it through.
    /// </summary>
    [Fact]
    public void Release_packs_pushes_then_tags_without_waiting_for_the_feed_and_never_prints_the_key()
    {
        Releasable();
        var head = Git(Work, "rev-parse", "HEAD");

        var (code, stdout, stderr) = Process(
            Work,
            Path.Combine(Work, "scripts", "release"),
            ["HEAD"],
            new Dictionary<string, string> { ["NUGET_API_KEY"] = NuGetKey }
        );

        code.ShouldBe(0, stdout + stderr);
        stdout.ShouldNotContain(NuGetKey);
        stderr.ShouldNotContain(NuGetKey);
        using var document = JsonDocument.Parse(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last());
        var line = document.RootElement;
        line.GetProperty("published").GetBoolean().ShouldBeTrue();
        line.GetProperty("observed").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("version").GetString().ShouldBe("0.2.18");
        line.GetProperty("feed").GetString().ShouldBe("https://api.nuget.org/v3/index.json");
        line.GetProperty("tag").GetString().ShouldBe("v0.2.18");
        line.GetProperty("release").GetString().ShouldBe("https://github.com/owner/repo/releases/tag/v0.2.18");
        line.GetProperty("why").GetString()!.ShouldStartWith("nuget.org accepted aio 0.2.18; it lists it once it validates the package, usually 15 to 30 minutes");
        Git(Work, "ls-remote", "origin", "refs/tags/v0.2.18").ShouldStartWith(head);
        var steps = Calls.Split('\n').Where(call => call is "pack" || call.StartsWith("dotnet nuget push", StringComparison.Ordinal) || call.StartsWith("curl", StringComparison.Ordinal) || call.StartsWith("release create", StringComparison.Ordinal)).ToList();
        steps[0].ShouldStartWith("curl"); // the feed is asked first, and does not list 0.2.18
        steps.SkipWhile(call => call.StartsWith("curl", StringComparison.Ordinal)).First().ShouldBe("pack");
        var push = steps.FindIndex(call => call.StartsWith("dotnet nuget push", StringComparison.Ordinal));
        steps[push].ShouldBe($"dotnet nuget push {Work}/artifacts/aio.0.2.18.nupkg --source https://api.nuget.org/v3/index.json --api-key {NuGetKey}");
        steps.FindIndex(call => call.StartsWith("release create v0.2.18", StringComparison.Ordinal)).ShouldBeGreaterThan(push);
        steps.FindLastIndex(call => call.StartsWith("curl", StringComparison.Ordinal)).ShouldBeLessThan(push, "the feed was read after the push");
    }

    [Fact]
    public void Release_whose_push_is_refused_tags_nothing()
    {
        Releasable();
        File.WriteAllText(Path.Combine(Fake, "refuse"), "");

        var (code, line) = RunIn(Work, Path.Combine(Work, "scripts", "release"), ["HEAD"]);

        code.ShouldBe(1, line.ToString());
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("tag").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("why").GetString()!.ShouldContain("dotnet nuget push refused aio 0.2.18");
        Calls.ShouldContain("dotnet nuget push");
        Calls.ShouldNotContain("release create");
        Git(Work, "ls-remote", "origin", "refs/tags/v0.2.18").ShouldBeEmpty();
    }

    /// <summary>
    /// #711. nuget.org listed 0.3.0 and 0.3.2 after release had stopped waiting, and the rerun said the
    /// version was published and stopped without a tag. Now the rerun makes the tag and the release.
    /// </summary>
    [Fact]
    public void Release_of_a_version_the_feed_already_has_tags_it_without_packing_or_pushing()
    {
        Releasable();
        var head = Git(Work, "rev-parse", "HEAD");
        var feed = Published();

        var (code, line) = RunIn(Work, Path.Combine(Work, "scripts", "release"), ["--source", feed, "HEAD"]);

        code.ShouldBe(0, line.ToString());
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("observed").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("tag").GetString().ShouldBe("v0.2.18");
        line.GetProperty("release").GetString().ShouldBe("https://github.com/owner/repo/releases/tag/v0.2.18");
        Calls.ShouldNotContain("pack");
        Calls.ShouldNotContain("nuget push");
        Calls.ShouldContain("release create v0.2.18");
        Git(Work, "ls-remote", "origin", "refs/tags/v0.2.18").ShouldStartWith(head);
    }

    [Fact]
    public void Release_of_a_version_published_and_tagged_on_the_commit_says_it_is_done()
    {
        Releasable();
        var head = Git(Work, "rev-parse", "HEAD");
        var feed = Published();
        Git(Work, "push", "--quiet", "origin", "HEAD:refs/tags/v0.2.18");
        File.WriteAllText(Path.Combine(Fake, "released"), "");

        var (code, line) = RunIn(Work, Path.Combine(Work, "scripts", "release"), ["--source", feed, "HEAD"]);

        code.ShouldBe(0, line.ToString());
        line.GetProperty("published").GetBoolean().ShouldBeFalse();
        line.GetProperty("observed").GetBoolean().ShouldBeTrue();
        line.GetProperty("tag").GetString().ShouldBe("v0.2.18");
        line.GetProperty("why").GetString()!.ShouldContain("Nothing was left to do");
        Calls.ShouldNotContain("pack");
        Calls.ShouldNotContain("nuget push");
        Calls.ShouldNotContain("release create");
        Git(Work, "ls-remote", "origin", "refs/tags/v0.2.18").ShouldStartWith(head);
    }

    [Fact]
    public void Release_of_a_version_tagged_on_another_commit_stops_and_moves_nothing()
    {
        Releasable();
        var before = Git(Work, "rev-parse", "HEAD~1");
        var feed = Published();
        Git(Work, "push", "--quiet", "origin", "HEAD~1:refs/tags/v0.2.18");

        var (code, line) = RunIn(Work, Path.Combine(Work, "scripts", "release"), ["--source", feed, "HEAD"]);

        code.ShouldBe(1, line.ToString());
        line.GetProperty("observed").GetBoolean().ShouldBeFalse();
        line.GetProperty("tag").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("why").GetString()!.ShouldContain($"origin's v0.2.18 is on {before}");
        Calls.ShouldNotContain("pack");
        Calls.ShouldNotContain("nuget push");
        Calls.ShouldNotContain("release create");
        Git(Work, "ls-remote", "origin", "refs/tags/v0.2.18").ShouldStartWith(before);
    }

    /// <summary>A folder feed that already holds aio 0.2.18.</summary>
    string Published()
    {
        var feed = Directory.CreateDirectory(Path.Combine(_root, "feed")).FullName;
        File.WriteAllText(Path.Combine(feed, "aio.0.2.18.nupkg"), "published before");
        return feed;
    }

    /// <summary>
    /// A checkout carrying <c>release</c> and a <c>pack</c> that writes an empty package, with a <c>dotnet</c>
    /// that reads version 0.2.18 and accepts the push unless <c>$FAKE/refuse</c> exists, a <c>curl</c> that
    /// plays a nuget.org that has not listed 0.2.18 yet, and a <c>gh</c> whose release create pushes the tag to origin. All of them echo the key.
    /// </summary>
    void Releasable()
    {
        Write("src/Directory.Build.props", "<Project><PropertyGroup><Version>0.2.18</Version></PropertyGroup></Project>\n");
        Write("scripts/release", File.ReadAllText(Path.Combine(ThisRepository.Root(), "scripts", "release")));
        Write("scripts/pack", """
            #!/usr/bin/env bash
            echo pack >> "$FAKE/calls"
            echo "restoring with ${NUGET_API_KEY:-no key}" >&2
            repo="$(cd "$(dirname "$0")/.." && pwd)"
            mkdir -p "$repo/artifacts" && printf nupkg > "$repo/artifacts/aio.0.2.18.nupkg"
            echo "packed with ${NUGET_API_KEY:-no key}"
            echo "$repo/artifacts/aio.0.2.18.nupkg"
            """);
        foreach (var script in new[] { "release", "pack" })
        {
            File.SetUnixFileMode(Path.Combine(Work, "scripts", script), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Commit("Raise the version (#681)");
        Executable("dotnet", """
            #!/usr/bin/env bash
            echo "dotnet $*" >> "$FAKE/calls"
            case "$1" in
              msbuild) echo 0.2.18 ;;
              nuget) echo "Pushing with $*"; echo "warn: $*" >&2; [ ! -f "$FAKE/refuse" ] || { echo "403 Forbidden"; exit 1; } ;;
            esac
            """);
        Executable("curl", """
            #!/usr/bin/env bash
            echo "curl $*" >> "$FAKE/calls"
            case "$*" in
              *v3/index.json*) echo '{"resources":[{"@id":"https://api.nuget.org/v3-flatcontainer/","@type":"PackageBaseAddress/3.0.0"}]}' ;;
              *v3-flatcontainer/aio/index.json*) echo '{"versions":["0.2.16","0.2.17"]}' ;;
              *) exit 22 ;;
            esac
            """);
        Executable("gh", """
            #!/usr/bin/env bash
            case "$1 $2" in
              "release view") [ -f "$FAKE/released" ] && echo "https://github.com/owner/repo/releases/tag/$3" && exit 0; exit 1 ;;
              "release create")
                echo "release create $3" >> "$FAKE/calls"
                while [ $# -gt 0 ]; do [ "$1" = --target ] && target="$2"; shift; done
                git push --quiet origin "$target:refs/tags/v0.2.18" && touch "$FAKE/released"
                echo "https://github.com/owner/repo/releases/tag/v0.2.18"; exit 0 ;;
            esac
            echo "fake gh: $*" >&2; exit 1
            """);
    }

    void Claim(int issue, string json)
    {
        Git(Work, "push", "--quiet", "origin", $"HEAD:refs/heads/change/{issue}");
        File.WriteAllText(Path.Combine(Fake, $"issue-{issue}.json"), json);
    }

    string PushBranch()
    {
        Write("src/thing.txt", "work\n");
        Commit("Do the work");
        Git(Work, "push", "--quiet", "origin", "HEAD:refs/heads/change/7");
        var head = Git(Work, "rev-parse", "HEAD");
        File.WriteAllText(Path.Combine(Fake, "state"), "OPEN");
        File.WriteAllText(Path.Combine(Fake, "head"), head);
        return head;
    }

    (int Code, JsonElement Line) Run(string script, params string[] arguments) => Run(script, arguments, null);

    (int Code, JsonElement Line) Run(string script, string[] arguments, IReadOnlyDictionary<string, string>? variables) =>
        RunIn(Work, script, arguments, variables);

    (int Code, JsonElement Line) RunIn(
        string directory,
        string script,
        string[] arguments,
        IReadOnlyDictionary<string, string>? variables = null
    )
    {
        var (code, stdout, stderr) = Process(
            directory,
            Script(script),
            arguments,
            variables
        );
        var last = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        last.ShouldNotBeNull($"{script} printed no line: {stderr}");
        using var document = JsonDocument.Parse(last);
        return (code, document.RootElement.Clone());
    }

    /// <summary>A script of this checkout: a step the engine finds by its name, or one of <c>scripts/</c>, which only a person runs.</summary>
    static string Script(string name) =>
        HarnessScripts.Find(ThisRepository.Root(), name)
        ?? new[] { Path.Combine(ThisRepository.Root(), "scripts", name) }.FirstOrDefault(File.Exists)
        ?? throw new FileNotFoundException($"This checkout has no script {name}.");

    string Git(string directory, params string[] arguments)
    {
        var (code, stdout, stderr) = Process(directory, "git", arguments);
        code.ShouldBe(0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    (int Code, string Stdout, string Stderr) Process(
        string directory,
        string file,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? variables = null
    )
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["PATH"] = $"{Fake}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
        start.Environment["FAKE"] = Fake;
        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Loop script test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "loop@example.test";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        foreach (var (name, value) in variables ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{file} did not finish in 60 s");
        }

        return (process.ExitCode, stdout.Result, stderr.Result);
    }
}
