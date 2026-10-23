using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.Files;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// Several runs of one repository prepared and retired at once (#631). Every worktree of a repository
/// shares its <c>.git/config</c>, and <c>git worktree add -b</c> writes the branch's upstream there, so
/// two prepares at once used to meet each other's <c>config.lock</c> and one failed. A real checkout
/// with a real origin, the same <c>prepare</c> and <c>cleanup</c> the app runs.
/// </summary>
public sealed class ConcurrentPlacesTests : IDisposable
{
    readonly GitCli _git = new();
    string _root = Directory.CreateTempSubdirectory("aio-concurrent-").FullName;

    string Checkout => Path.Combine(_root, "checkout");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Eight_worktrees_of_one_repository_prepared_at_once_are_all_prepared()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();

        // Two instances, as the app has: the worktree environment and the one inside sbx.
        WorktreeEnvironment[] environments = [new(), new()];
        var places = await Task.WhenAll(
            Enumerable.Range(1, 8).Select(n => Task.Run(() => environments[n % 2].PrepareAsync(Request(n), CancellationToken.None)))
        );

        places.Select(place => place.WorkingDirectory).ShouldBe(Enumerable.Range(1, 8).Select(Worktree));
        foreach (var n in Enumerable.Range(1, 8))
        {
            (await Git(Worktree(n), "branch", "--show-current")).Trim().ShouldBe($"change/{n}");
        }
    }

    [Fact]
    public async Task Prepares_and_cleanups_of_one_repository_at_once_all_finish()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        var environment = new WorktreeEnvironment();
        var retiring = new List<RunPlace>();
        foreach (var n in Enumerable.Range(1, 4))
        {
            retiring.Add(await environment.PrepareAsync(Request(n), CancellationToken.None));
        }

        await Task.WhenAll(
            [
                .. retiring.Select(place => Task.Run(() => environment.RetireAsync(place, CancellationToken.None))),
                .. Enumerable.Range(5, 4).Select(n => Task.Run(() => environment.PrepareAsync(Request(n), CancellationToken.None))),
            ]
        );

        foreach (var n in Enumerable.Range(1, 4))
        {
            Directory.Exists(Worktree(n)).ShouldBeFalse();
        }

        foreach (var n in Enumerable.Range(5, 4))
        {
            (await Git(Worktree(n), "branch", "--show-current")).Trim().ShouldBe($"change/{n}");
        }
    }

    [Fact]
    public async Task A_config_locked_by_another_process_is_tried_once_more_after_a_pause()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();

        // A person's git holding the config: the first worktree add makes the branch, fails on its
        // upstream, and the lock goes before the retry.
        var held = Path.Combine(Checkout, ".git", "config.lock");
        await File.WriteAllTextAsync(held, string.Empty);
        var prepare = Task.Run(() => new WorktreeEnvironment().PrepareAsync(Request(1), CancellationToken.None));
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            while (!File.Exists(Path.Combine(Checkout, ".git", "refs", "heads", "change", "1")))
            {
                await Task.Delay(10, deadline.Token);
            }
        }

        File.Delete(held);
        var place = await prepare;

        place.WorkingDirectory.ShouldBe(Worktree(1));
        (await Git(Worktree(1), "branch", "--show-current")).Trim().ShouldBe("change/1");
    }

    PlaceRequest Request(int n) => new(Checkout, $"change/{n}", "HEAD");

    string Worktree(int n) => Path.Combine(Checkout, ".worktrees", $"change-{n}");

    /// <summary>A checkout cloned from a bare origin, so prepare fetches and sets each branch's upstream.</summary>
    async Task Initialize()
    {
        var origin = Path.Combine(_root, "origin.git");
        await Git(_root, "init", "--quiet", "--bare", "--initial-branch=master", origin);
        await Git(_root, "clone", "--quiet", origin, Checkout);

        // macOS's temp directory is /var -> /private/var, and git names the real one.
        _root = Path.GetDirectoryName((await Git(Checkout, "rev-parse", "--show-toplevel")).Trim())!;
        await Git(Checkout, "config", "user.name", "Concurrent places tests");
        await Git(Checkout, "config", "user.email", "concurrent-places-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");
        await File.WriteAllTextAsync(Path.Combine(Checkout, ".gitignore"), ".worktrees/\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");
        await Git(Checkout, "push", "--quiet", "origin", "master");
    }

    async Task<string> Git(string directory, params string[] arguments)
    {
        var result = await _git.Run(directory, arguments);
        result.Succeeded.ShouldBeTrue(result.StandardError);
        return result.StandardOutput;
    }
}
