using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.Infrastructure.Files;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #697. <see cref="GitEvidenceBranch"/> against real git: a checkout and a second clone of one bare
/// <c>origin</c> in the temporary directory, as two instances of the control plane on two machines.
/// What matters is that every run lands as its own commit and file, that a push from the other clone
/// in between is fetched and built on rather than refused for good, and that the checkout is as it was.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class EvidenceBranchTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-evidence-").FullName;
    readonly GitCli _git = new();
    readonly GitEvidenceBranch _branch;

    string Origin => Path.Combine(_root, "origin.git");

    string Work => Path.Combine(_root, "work");

    string Other => Path.Combine(_root, "other");

    string Scratch => Path.Combine(_root, "data", "runs", ".evidence");

    public EvidenceBranchTests()
    {
        _branch = new GitEvidenceBranch(_git);
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(Origin, "symbolic-ref", "HEAD", "refs/heads/main");
        foreach (var clone in new[] { "work", "other" })
        {
            Git(_root, "clone", "--quiet", "origin.git", clone);
            Git(Path.Combine(_root, clone), "config", "user.name", "Test");
            Git(Path.Combine(_root, clone), "config", "user.email", "test@example.com");
        }

        Git(Work, "switch", "--quiet", "-c", "main");
        File.WriteAllText(Path.Combine(Work, "README.md"), "A repository.\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Initial revision");
        Git(Work, "push", "--quiet", "origin", "main");
        Git(Other, "pull", "--quiet", "origin", "main");
        Git(Work, "switch", "--quiet", "-c", "change/12");
        File.WriteAllText(Path.Combine(Work, "notes.txt"), "uncommitted\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Two_runs_are_two_commits_and_a_push_from_another_clone_in_between_is_built_on()
    {
        var first = Recording("12", "first run\r\n");
        var second = Recording("12", "second run\r\n");
        var third = Recording("14", "the other instance's run\r\n");

        var one = await _branch.PushAsync(Work, Scratch, [first], CancellationToken.None);
        one.Refusal.ShouldBeNull();
        one.Pushed.ShouldBe([first.Path]);

        // The other instance pushes its run; this checkout still knows the branch as its own push left it.
        var other = await new GitEvidenceBranch(_git).PushAsync(Other, Scratch, [third], CancellationToken.None);
        other.Refusal.ShouldBeNull();
        other.Pushed.ShouldBe([third.Path]);

        // The next run hands the first again, as the push after a failed one does: only the new one goes.
        var two = await _branch.PushAsync(Work, Scratch, [first, second], CancellationToken.None);
        two.Refusal.ShouldBeNull();
        two.Pushed.ShouldBe([second.Path]);

        Git(Origin, "log", "--format=%s", "aio/evidence").Split('\n').ShouldBe([second.Message, third.Message, first.Message]);
        Git(Origin, "ls-tree", "-r", "--name-only", "aio/evidence").Split('\n').Order().ShouldBe(new[] { first.Path, second.Path, third.Path }.Order());
        Git(Origin, "show", $"aio/evidence:{second.Path}").ShouldContain("second run");
        Git(Origin, "rev-list", "--max-parents=0", "aio/evidence").ShouldBe(Git(Origin, "rev-list", "--reverse", "aio/evidence").Split('\n')[0]);
        Git(Origin, "merge-base", "--is-ancestor", "main", "aio/evidence", allowFailure: true).ShouldBe("1", "the branch is an orphan, with nothing of main");

        // The checkout is as it was, and no worktree is left behind.
        Git(Work, "symbolic-ref", "--short", "HEAD").ShouldBe("change/12");
        File.ReadAllText(Path.Combine(Work, "notes.txt")).ShouldBe("uncommitted\n");
        Git(Work, "worktree", "list", "--porcelain").Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)).ShouldBe(1);
        Directory.EnumerateFileSystemEntries(Scratch).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_branch_that_has_every_file_is_not_pushed_again()
    {
        var run = Recording("12", "run\r\n");
        (await _branch.PushAsync(Work, Scratch, [run], CancellationToken.None)).Refusal.ShouldBeNull();
        var head = Git(Origin, "rev-parse", "aio/evidence");

        var again = await _branch.PushAsync(Work, Scratch, [run], CancellationToken.None);

        again.Refusal.ShouldBeNull();
        again.Pushed.ShouldBeEmpty();
        Git(Origin, "rev-parse", "aio/evidence").ShouldBe(head);
    }

    [Fact]
    public async Task A_remote_that_cannot_be_reached_is_a_refusal_and_nothing_else()
    {
        Git(Work, "remote", "set-url", "origin", Path.Combine(_root, "gone.git"));

        var pushed = await _branch.PushAsync(Work, Scratch, [Recording("12", "run\r\n")], CancellationToken.None);

        pushed.Pushed.ShouldBeEmpty();
        pushed.Refusal.ShouldNotBeNull().ShouldStartWith("origin/aio/evidence could not be fetched:");
        Git(Work, "symbolic-ref", "--short", "HEAD").ShouldBe("change/12");
    }

    /// <summary>A recording on this machine, as a run's directory keeps it, and where it goes on the branch.</summary>
    EvidenceFile Recording(string pbi, string output)
    {
        var runId = Guid.NewGuid();
        var source = Path.Combine(_root, "data", "runs", runId.ToString("N"), "session.cast");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, $"{{\"version\": 2, \"width\": 80, \"height\": 6}}\n[0.1, \"o\", \"{output.Replace("\r\n", "\\r\\n", StringComparison.Ordinal)}\"]\n");
        return new EvidenceFile(EvidenceBranch.RecordingOf(pbi, runId), source, $"{pbi} · in-progress · {runId:N}");
    }

    string Git(string directory, params string[] arguments) => Git(directory, arguments, allowFailure: false);

    string Git(string directory, string a, string b, string c, string d, bool allowFailure) => Git(directory, [a, b, c, d], allowFailure);

    /// <summary>What git printed, or its exit code when <paramref name="allowFailure"/> and it failed.</summary>
    string Git(string directory, string[] arguments, bool allowFailure)
    {
        var run = _git.Run(directory, arguments).GetAwaiter().GetResult();
        if (allowFailure && !run.Succeeded)
        {
            return $"{run.ExitCode}";
        }

        run.Succeeded.ShouldBeTrue($"git {string.Join(' ', arguments)}: {run.StandardError}");
        return run.StandardOutput.Trim();
    }
}
