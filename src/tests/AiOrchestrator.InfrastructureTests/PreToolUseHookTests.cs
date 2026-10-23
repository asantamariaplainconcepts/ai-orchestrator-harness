using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The agent's PreToolUse hook (#504): <c>.claude/hooks/pre-tool-use</c> refuses
/// <c>--no-verify</c>, every merge that does not go through <c>.aio/steps/merge</c>, the git commands
/// that tread on a worktree, a stash stack or a remote shared with another session, and a WebFetch
/// of a vendor's site (#731). The
/// hook runs for real, with <c>node</c>, against this checkout's own file and with the JSON Claude
/// Code hands it, so a test against a copy cannot pass while the file says something else.
/// </summary>
public sealed class PreToolUseHookTests
{
    [Theory]
    [InlineData("git commit --no-verify -m 'skip'")]
    [InlineData("git commit -m 'skip' --no-verify")]
    [InlineData("git commit --no-ver -m 'a prefix git accepts'")]
    [InlineData("git commit -n -m 'skip'")]
    [InlineData("git commit -anm 'a cluster'")]
    [InlineData("git -C src commit --no-verify -m 'skip'")]
    [InlineData("cd src && git commit --no-verify -m 'skip'")]
    [InlineData("GIT_AUTHOR_NAME=x git commit --no-verify -m 'skip'")]
    public void A_commit_that_skips_the_hooks_is_refused(string command) =>
        Refused(command).ShouldContain("--no-verify");

    [Theory]
    [InlineData("git -c core.hooksPath=/dev/null commit -m 'skip'")]
    [InlineData("git -c core.hookspath=.nohooks push origin HEAD")]
    [InlineData("git -C src -c core.hooksPath=x commit -am 'skip'")]
    public void A_commit_or_push_that_points_the_hooks_elsewhere_is_refused(string command) =>
        Refused(command).ShouldContain("core.hooksPath");

    [Theory]
    [InlineData("git push --no-verify")]
    [InlineData("git push -u origin change/504 --no-verify")]
    [InlineData("git fetch -q && git push --no-verify origin HEAD")]
    public void A_push_that_skips_the_hooks_is_refused(string command) =>
        Refused(command).ShouldContain("git push again without it");

    [Theory]
    [InlineData("gh pr merge 42")]
    [InlineData("gh pr merge 42 --squash --delete-branch")]
    [InlineData("gh pr merge --auto --squash 42")]
    [InlineData("gh pr -R octo/harness merge 42")]
    [InlineData("gh api -X PUT repos/octo/harness/pulls/42/merge")]
    [InlineData("gh api --method=put /repos/octo/harness/pulls/42/merge -f merge_method=squash")]
    [InlineData("gh api repos/octo/harness/pulls/42/merge -f merge_method=squash")]
    [InlineData("gh api graphql -f query='mutation { mergePullRequest(input: {pullRequestId: \"x\"}) { clientMutationId } }'")]
    [InlineData("gh api graphql -f query='mutation { enablePullRequestAutoMerge(input: {pullRequestId: \"x\"}) { clientMutationId } }'")]
    [InlineData("bash -c 'gh pr merge 42'")]
    [InlineData("bash -lc \"git commit --no-verify -m skip\"")]
    [InlineData("echo $(gh pr merge 42)")]
    [InlineData("rtk gh pr merge 42")]
    [InlineData("/opt/homebrew/bin/gh pr merge 42")]
    public void A_merge_outside_the_script_is_refused(string command)
    {
        var said = Refused(command);

        if (!command.Contains("--no-verify", StringComparison.Ordinal))
        {
            said.ShouldContain(".aio/steps/merge <pull request>");
        }
    }

    [Theory]
    [InlineData(".aio/steps/merge 42")]
    [InlineData(".aio/steps/merge 42 1a2b3c4d")]
    [InlineData("bash .aio/steps/merge 42 1a2b3c4d")]
    [InlineData("git commit -m 'Block gh pr merge and --no-verify in the hook'")]
    [InlineData("git commit -m '-n is no longer allowed'")]
    [InlineData("git commit --amend --no-edit")]
    [InlineData("git push -n")]
    [InlineData("git push --dry-run origin HEAD")]
    [InlineData("git push -u origin change/504")]
    [InlineData("git merge --no-ff main")]
    [InlineData("git -c core.hooksPath=x log --oneline")]
    [InlineData("git -c user.name=x commit -m 'another setting'")]
    [InlineData("gh pr view 42 --json state")]
    [InlineData("gh pr create --base v2 --title 'gh pr merge'")]
    [InlineData("gh api repos/octo/harness/pulls/42/merge")]
    [InlineData("gh api -X GET repos/octo/harness/pulls/42/merge")]
    [InlineData("gh api repos/octo/harness/pulls/42 -X PATCH -f title=x")]
    [InlineData("grep -rn 'gh pr merge' .aio")]
    [InlineData("echo \"git commit --no-verify\"")]
    [InlineData("git add src/a.cs src/b.cs")]
    [InlineData("git push --force-with-lease")]
    [InlineData("git push --force-with-lease=change/12 origin change/12")]
    [InlineData("git clean -f -- tmp/")]
    [InlineData("git clean -n")]
    [InlineData("git checkout -- src/a.cs")]
    [InlineData("git restore src/a.cs")]
    [InlineData("git reset --soft HEAD~1")]
    [InlineData("git stash list")]
    [InlineData("git commit -m 'Refuse git add -A and git reset --hard in the hook'")]
    [InlineData("cat > notes.md <<'EOF'\nThe hook refuses git commit --no-verify and git add -A.\nEOF")]
    [InlineData("cat > notes.md <<EOF\ngit reset --hard\nEOF\ngit status")]
    [InlineData("cat <<-EOF > notes.md\n\tgh pr merge 42\n\tEOF")]
    [InlineData("git commit -F- <<\"EOF\"\ngit add -A\nEOF")]
    [InlineData("git commit -m \"$(cat <<'EOF'\nStop git push --force\nEOF\n)\"")]
    public void Everything_else_runs(string command) => Run(Call("Bash", command)).ShouldBe((0, ""));

    [Theory]
    [InlineData("git add -A", "git add <path> <path>")]
    [InlineData("git add --all", "git add <path> <path>")]
    [InlineData("git add .", "git add <path> <path>")]
    [InlineData("git -C src add -Av", "git add <path> <path>")]
    [InlineData("git commit -a -m 'all'", "Stage named paths first")]
    [InlineData("git commit -am 'a cluster without n'", "Stage named paths first")]
    [InlineData("git commit --all -m 'all'", "Stage named paths first")]
    [InlineData("git reset --hard", "git checkout -- <path>")]
    [InlineData("git reset --hard origin/master", "git checkout -- <path>")]
    [InlineData("git clean -fd", "git clean -f -- <path>")]
    [InlineData("git clean -f tmp/", "git clean -f -- <path>")]
    [InlineData("git checkout .", "git checkout -- <path>")]
    [InlineData("git checkout -- .", "git checkout -- <path>")]
    [InlineData("git restore .", "git restore <path>")]
    [InlineData("git stash drop", "git stash apply <sha>")]
    [InlineData("git stash drop stash@{0}", "git stash apply <sha>")]
    [InlineData("git stash clear", "git stash apply <sha>")]
    [InlineData("git push --force", "--force-with-lease")]
    [InlineData("git push -f origin change/12", "--force-with-lease")]
    [InlineData("git push -uf origin change/12", "--force-with-lease")]
    [InlineData("cat > notes.md <<'EOF'\ntext\nEOF\ngit add -A", "git add <path> <path>")]
    [InlineData("bash -c 'git reset --hard'", "git checkout -- <path>")]
    public void A_command_that_treads_on_shared_work_is_refused(string command, string instead) =>
        Refused(command).ShouldContain(instead);

    /// <summary>
    /// The default branch is the remote's <c>HEAD</c>, read with git in the directory Claude Code
    /// names as the call's <c>cwd</c>: here a repository of the test's own whose <c>origin/HEAD</c>
    /// is <c>origin/master</c>, on <c>master</c> or on a work branch.
    /// </summary>
    [Theory]
    [InlineData("git push origin master", "master", true)]
    [InlineData("git push origin HEAD:master", "change/12", true)]
    [InlineData("git push origin refs/heads/master", "change/12", true)]
    [InlineData("git push", "master", true)]
    [InlineData("git push -u origin HEAD", "master", true)]
    [InlineData("git push origin change/12", "master", false)]
    [InlineData("git push -u origin change/12", "change/12", false)]
    [InlineData("git push", "change/12", false)]
    [InlineData("git push -u origin HEAD", "change/12", false)]
    public void A_push_of_the_default_branch_is_refused(string command, string on, bool refused)
    {
        var repository = Directory.CreateTempSubdirectory("aio-pre-tool-use-").FullName;
        try
        {
            Git(repository, "init", "--quiet", "-b", on);
            Git(repository, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/master");

            if (refused)
            {
                Refused(command, repository).ShouldContain("Push your work branch");
            }
            else
            {
                Run(Call("Bash", command, repository)).ShouldBe((0, ""));
            }
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://github.com/octo/harness/issues/731", true)]
    [InlineData("https://api.github.com/repos/octo/harness/pulls/42", true)]
    [InlineData("https://dev.azure.com/octo/harness/_workitems/edit/7", true)]
    [InlineData("https://octo.visualstudio.com/harness/_git/harness", true)]
    [InlineData("https://octo.atlassian.net/browse/AIO-7", true)]
    [InlineData("https://gitlab.com/octo/harness/-/issues/7", true)]
    [InlineData("https://learn.microsoft.com/en-us/dotnet/aspire/", false)]
    [InlineData("https://notgithub.com/octo", false)]
    public void A_web_fetch_of_a_vendor_is_refused(string url, bool refused)
    {
        var call = new JsonObject
        {
            ["tool_name"] = "WebFetch",
            ["tool_input"] = new JsonObject { ["url"] = url, ["prompt"] = "What does it say?" },
        };

        var (code, said) = Run(call.ToJsonString());

        if (refused)
        {
            code.ShouldBe(2, said);
            said.ShouldContain(".aio/aio issue");
            said.ShouldContain("Nothing was run.");
        }
        else
        {
            (code, said).ShouldBe((0, ""));
        }
    }

    [Fact]
    public void A_tool_other_than_bash_is_not_read()
    {
        var call = new JsonObject
        {
            ["tool_name"] = "Write",
            ["tool_input"] = new JsonObject { ["file_path"] = "x.sh", ["content"] = "gh pr merge 42" },
        };

        Run(call.ToJsonString()).ShouldBe((0, ""));
    }

    [Fact]
    public void The_settings_register_the_hook_for_bash_and_web_fetch()
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(ThisRepository.Root(), ".claude", "settings.json")))!;

        foreach (var tool in new[] { "Bash", "WebFetch" })
        {
            var entry = settings["hooks"]!["PreToolUse"]!.AsArray().Single(e => (string?)e!["matcher"] == tool)!;
            ((string?)entry["hooks"]![0]!["command"]).ShouldBe("\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/pre-tool-use");
        }
    }

    [Fact]
    public void The_allow_list_lets_a_run_call_aio_done_and_never_merge()
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(ThisRepository.Root(), ".claude", "settings.json")))!;
        string[] allowed = [.. settings["permissions"]!["allow"]!.AsArray().Select(rule => (string)rule!)];
        string[] denied = [.. settings["permissions"]!["deny"]!.AsArray().Select(rule => (string)rule!)];

        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(aio ", StringComparison.Ordinal));
        allowed.ShouldContain("Bash(.aio/aio done:*)");
        allowed.ShouldNotContain(rule => rule.StartsWith("Bash(gh pr merge", StringComparison.Ordinal) || rule.StartsWith("Bash(gh:", StringComparison.Ordinal) || rule.StartsWith("Bash(gh api", StringComparison.Ordinal));
        denied.ShouldContain("Bash(gh pr merge:*)");
    }

    /// <summary>
    /// The other guardrail, <c>.githooks/pre-commit</c>, run by git itself from this checkout's file in a
    /// repository of the test's own whose trunk is <c>master</c>.
    /// </summary>
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_commit_on_master_is_refused_with_the_command_that_creates_a_worktree()
    {
        var repository = Directory.CreateTempSubdirectory("aio-pre-commit-").FullName;
        try
        {
            Git(repository, "init", "--quiet", "-b", "master");
            Git(repository, "config", "core.hooksPath", Path.Combine(ThisRepository.Root(), ".githooks"));
            File.WriteAllText(Path.Combine(repository, "a.txt"), "a\n");
            Git(repository, "add", "a.txt");

            var (code, said) = Git(repository, "commit", "--quiet", "-m", "On the trunk");

            code.ShouldBe(1, said);
            said.ShouldBe(
                "This repository works in worktrees: commit on a branch, not master.\n"
                    + "Create one with: git worktree add ../.worktrees/change/<n> -b change/<n> master"
            );
            Git(repository, "rev-parse", "--verify", "-q", "HEAD").Code.ShouldNotBe(0, "nothing was committed");

            Git(repository, "switch", "--quiet", "-c", "change/1");
            Git(repository, "commit", "--quiet", "-m", "On a branch").ShouldBe((0, ""));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    static (int Code, string Said) Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
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
        start.Environment.Remove("AIORCH_UNGUARDED");
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Hook test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "hook@example.test";

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(TimeSpan.FromSeconds(30)).ShouldBeTrue($"git {string.Join(' ', arguments)} did not finish");

        return (process.ExitCode, (output.Result + error.Result).Trim());
    }

    static string Refused(string command, string? cwd = null)
    {
        var (code, said) = Run(Call("Bash", command, cwd));

        code.ShouldBe(2, $"'{command}' ran: {said}");
        said.ShouldContain("Nothing was run.");
        return said;
    }

    /// <summary>
    /// The call as Claude Code hands it. Its <c>cwd</c> is the temporary directory unless a test names
    /// one, so a push is never read against whichever branch this checkout happens to be on.
    /// </summary>
    static string Call(string tool, string command, string? cwd = null) =>
        new JsonObject
        {
            ["session_id"] = "test",
            ["hook_event_name"] = "PreToolUse",
            ["cwd"] = cwd ?? Path.GetTempPath(),
            ["tool_name"] = tool,
            ["tool_input"] = new JsonObject { ["command"] = command },
        }.ToJsonString(new JsonSerializerOptions());

    static (int Code, string Said) Run(string stdin)
    {
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(ThisRepository.Root(), ".claude", "hooks", "pre-tool-use"));

        using var process = Process.Start(start)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The hook did not answer in 30 seconds.");
        }

        return (process.ExitCode, (output.Result + error.Result).Trim());
    }
}
