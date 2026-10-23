using System.Text;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.Infrastructure.Files;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #566. <see cref="GitBranchPublisher"/> against real git: a checkout whose <c>origin</c> is a bare
/// repository in the temporary directory. What matters is what is not written: the default branch,
/// the checkout's branch and its uncommitted work, and a file the repository already made its own.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BranchPublisherTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-publish-").FullName;
    readonly GitCli _git = new();
    readonly GitBranchPublisher _publisher;

    string Origin => Path.Combine(_root, "origin.git");

    string Work => Path.Combine(_root, "work");

    public BranchPublisherTests()
    {
        _publisher = new GitBranchPublisher(_git);
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(Origin, "symbolic-ref", "HEAD", "refs/heads/main");
        Git(_root, "clone", "--quiet", "origin.git", "work");
        Git(Work, "config", "user.name", "Test");
        Git(Work, "config", "user.email", "test@example.com");
        Git(Work, "switch", "--quiet", "-c", "main");
        File.WriteAllText(Path.Combine(Work, "README.md"), "A repository.\n");
        Directory.CreateDirectory(Path.Combine(Work, ".aio"));
        File.WriteAllText(Path.Combine(Work, ".aio", "gate"), "#!/usr/bin/env bash\ndotnet test\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Initial revision");
        Git(Work, "push", "--quiet", "origin", "main");
        Git(Work, "switch", "--quiet", "-c", "feature/x");
        File.WriteAllText(Path.Combine(Work, "notes.txt"), "uncommitted\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    static BranchPublication Publication(string branch = "harness/install") =>
        new(
            branch,
            "Install harness 2",
            [
                new(".aio/version", Encoding.UTF8.GetBytes("2\n"), false, null),
                new(".aio/gate", Encoding.UTF8.GetBytes("#!/usr/bin/env bash\nexit 1\n"), true, null, KeepExisting: true),
                new(".aio/steps/merge", Encoding.UTF8.GetBytes("#!/usr/bin/env bash\n"), true, null),
                new(".opencode/command", [], false, "../.ai/commands"),
            ]
        );

    [Fact]
    public async Task The_files_land_on_their_branch_from_the_default_one_and_nowhere_else()
    {
        var main = Git(Origin, "rev-parse", "refs/heads/main");

        var published = await _publisher.PublishAsync(Work, Publication(), CancellationToken.None);

        published.Refusal.ShouldBeNull();
        published.Base.ShouldBe("main");
        published.Commit.ShouldBe(Git(Origin, "rev-parse", "refs/heads/harness/install"));
        published.Kept.ShouldBe([".aio/gate"]);

        Git(Origin, "rev-parse", "refs/heads/main").ShouldBe(main, "the default branch is not written");
        Git(Origin, "rev-parse", "refs/heads/harness/install^").ShouldBe(main, "one commit on top of the default branch");
        Git(Origin, "show", "harness/install:.aio/version").ShouldBe("2");
        Git(Origin, "show", "harness/install:.aio/gate").ShouldContain("dotnet test", Case.Sensitive, "the repository's own gate is kept");
        Git(Origin, "ls-tree", "harness/install", ".aio/steps/merge").ShouldStartWith("100755");
        Git(Origin, "ls-tree", "harness/install", ".aio/version").ShouldStartWith("100644");
        Git(Origin, "ls-tree", "harness/install", ".opencode/command").ShouldStartWith("120000");
        Git(Origin, "show", "harness/install:.opencode/command").ShouldBe("../.ai/commands");

        // The checkout is as it was: its branch, its uncommitted file, and no worktree left behind.
        Git(Work, "symbolic-ref", "--short", "HEAD").ShouldBe("feature/x");
        File.ReadAllText(Path.Combine(Work, "notes.txt")).ShouldBe("uncommitted\n");
        File.Exists(Path.Combine(Work, ".aio", "version")).ShouldBeFalse();
        Git(Work, "worktree", "list", "--porcelain").Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task A_second_publish_starts_again_from_the_default_branch()
    {
        await _publisher.PublishAsync(Work, Publication(), CancellationToken.None);
        File.WriteAllText(Path.Combine(Work, "README.md"), "Moved on.\n");
        Git(Work, "commit", "--quiet", "-am", "Later");
        Git(Work, "push", "--quiet", "origin", "HEAD:main");

        var again = await _publisher.PublishAsync(Work, Publication(), CancellationToken.None);

        again.Refusal.ShouldBeNull();
        Git(Origin, "rev-parse", "refs/heads/harness/install^").ShouldBe(Git(Origin, "rev-parse", "refs/heads/main"));
    }

    [Fact]
    public async Task Nothing_to_propose_and_the_default_branch_are_refusals_with_nothing_pushed()
    {
        var onto = await _publisher.PublishAsync(Work, Publication("main"), CancellationToken.None);

        onto.Commit.ShouldBeNull();
        onto.Refusal.ShouldBe("main is origin's default branch, and nothing is written there.");

        await _publisher.PublishAsync(Work, Publication(), CancellationToken.None);
        Git(Work, "fetch", "--quiet", "origin");
        Git(Work, "push", "--quiet", "origin", "origin/harness/install:refs/heads/main");

        var same = await _publisher.PublishAsync(Work, Publication(), CancellationToken.None);

        same.Commit.ShouldBeNull();
        same.Refusal.ShouldBe("origin/main already has every file as this version writes it, so there is nothing to propose.");
    }

    string Git(string directory, params string[] arguments)
    {
        var result = _git.Run(directory, arguments).GetAwaiter().GetResult();
        result.Succeeded.ShouldBeTrue($"git {string.Join(' ', arguments)}: {result.StandardError}");
        return result.StandardOutput.Trim();
    }
}
