using System.Text.Json.Nodes;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.AgentProviders;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The trust Claude Code keeps in <c>.claude.json</c>: read before a run (#606), and taken back where
/// this control plane wrote it before #608. Against a file of the test's own, never the user's.
/// </summary>
public sealed class ClaudeTrustTests : IDisposable
{
    // Real, because the keys are: macOS's temp directory is /var -> /private/var.
    readonly string _root = NativePaths.RealPath(Directory.CreateTempSubdirectory("aio-claude-trust-").FullName)!;

    string Config => Path.Combine(_root, ".claude.json");

    string Worktree => Directory.CreateDirectory(Path.Combine(_root, "repo", ".worktrees", "change-1")).FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_directory_that_is_not_a_linked_worktree_has_no_main_checkout()
    {
        var worktree = Worktree;
        ClaudeTrust.MainCheckout(worktree).ShouldBeNull();

        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /somewhere/modules/sub\n");
        ClaudeTrust.MainCheckout(worktree).ShouldBeNull();
    }

    // ── Reading it (#606) ──────────────────────────────────────────────────────────────────────

    /// <summary>A repository: a directory with a <c>.git</c> of its own, where Claude Code stops looking up.</summary>
    string Repository(string name)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, "projects", name)).FullName;
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return path;
    }

    void Trusting(params string[] paths) =>
        File.WriteAllText(
            Config,
            new JsonObject
            {
                ["numStartups"] = 41,
                ["projects"] = new JsonObject(paths.Select(path => KeyValuePair.Create(path, (JsonNode?)new JsonObject { ["hasTrustDialogAccepted"] = true }))),
            }.ToJsonString()
        );

    [Fact]
    public void A_trusted_folder_reads_trusted_and_the_file_is_not_written()
    {
        var folder = Repository("dogfood");
        Trusting(folder);
        var before = File.ReadAllText(Config);

        var trust = new ClaudeCodeProvider(configFile: Config).Trust(folder);

        trust.Trusted.ShouldBeTrue();
        trust.Remedy.ShouldBeNull();
        File.ReadAllText(Config).ShouldBe(before);
    }

    [Fact]
    public void A_folder_inside_a_trusted_one_reads_trusted_up_to_the_repository_top()
    {
        var checkout = Repository("dogfood");
        var below = Directory.CreateDirectory(Path.Combine(checkout, "src", "app")).FullName;
        var worktree = Directory.CreateDirectory(Path.Combine(checkout, ".worktrees", "change-2")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {Path.Combine(checkout, ".git", "worktrees", "change-2")}\n");
        Trusting(checkout);

        var provider = new ClaudeCodeProvider(configFile: Config);

        provider.Trust(below).Trusted.ShouldBeTrue("a directory of a trusted repository");
        provider.Trust(worktree).Trusted.ShouldBeTrue("a linked worktree whose main checkout is trusted");
    }

    [Fact]
    public void A_folder_without_trust_reads_untrusted_with_the_command_that_answers_it()
    {
        var folder = Repository("dogfood");
        Trusting(Path.Combine(_root, "elsewhere"));

        var trust = new ClaudeCodeProvider(configFile: Config).Trust(folder);

        trust.Trusted.ShouldBeFalse();
        trust.Why.ShouldBe($"Claude Code does not trust {folder}, so a run there would wait on its trust question. Run claude there once and accept it.");
        trust.Remedy.ShouldBe($"cd '{folder}' && claude");
    }

    [Fact]
    public void A_repository_under_a_trusted_parent_that_is_no_repository_reads_untrusted()
    {
        // 10 October: ~/projects was trusted, aio-dogfood below it was not, and Claude Code asked.
        var folder = Repository("dogfood");
        Trusting(Path.Combine(_root, "projects"));

        new ClaudeCodeProvider(configFile: Config).Trust(folder).Trusted.ShouldBeFalse();
    }

    [Fact]
    public void A_missing_or_broken_file_trusts_nothing()
    {
        var folder = Repository("dogfood");
        var provider = new ClaudeCodeProvider(configFile: Config);

        provider.Trust(folder).Trusted.ShouldBeFalse();
        File.Exists(Config).ShouldBeFalse("reading never creates the file");

        File.WriteAllText(Config, "{ not json");
        provider.Trust(folder).Trusted.ShouldBeFalse();
    }

    // ── Taking it back (#546) ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// An entry as a launch before #608 left it: trusted, with the mark naming the run's directory and
    /// what the entry held before (<c>created</c> when there was none).
    /// </summary>
    static JsonObject Marked(string forDirectory, JsonObject? existing = null)
    {
        JsonObject mark = new() { ["for"] = forDirectory };
        var entry = existing ?? [];

        if (existing is null)
        {
            mark["created"] = true;
        }
        else if (existing["hasTrustDialogAccepted"] is { } was)
        {
            mark["was"] = was.DeepClone();
        }

        entry["hasTrustDialogAccepted"] = true;
        entry["aiOrchestratorTrust"] = mark;
        return entry;
    }

    [Fact]
    public void Retiring_a_worktree_leaves_the_file_as_it_was_before_it_was_trusted()
    {
        const string Before = """{"numStartups": 41, "projects": {"/elsewhere": {"allowedTools": ["Bash"], "hasTrustDialogAccepted": true}}}""";
        var worktree = LinkedWorktree();
        var checkout = Path.GetDirectoryName(Path.GetDirectoryName(worktree))!;
        var written = JsonNode.Parse(Before)!;
        written["projects"]![worktree] = Marked(worktree);
        written["projects"]![checkout] = Marked(worktree);
        // What Claude Code keeps about a project it ran in goes with the entry the launch created.
        written["projects"]![worktree]!["lastSessionId"] = "a7c1";
        File.WriteAllText(Config, written.ToJsonString());

        ClaudeTrust.Retire(Config, worktree).ShouldBeTrue();

        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Config)), JsonNode.Parse(Before)).ShouldBeTrue(File.ReadAllText(Config));
    }

    [Fact]
    public void An_entry_that_was_there_keeps_what_it_held_and_gets_its_answer_back()
    {
        var worktree = LinkedWorktree();
        var checkout = Path.GetDirectoryName(Path.GetDirectoryName(worktree))!;
        JsonObject CheckoutEntry() => new() { ["allowedTools"] = new JsonArray("Bash"), ["hasTrustDialogAccepted"] = false };
        JsonObject WorktreeEntry() => new() { ["history"] = new JsonArray() };
        JsonObject Before() => new() { ["projects"] = new JsonObject { [checkout] = CheckoutEntry(), [worktree] = WorktreeEntry() } };
        File.WriteAllText(
            Config,
            new JsonObject { ["projects"] = new JsonObject { [checkout] = Marked(worktree, CheckoutEntry()), [worktree] = Marked(worktree, WorktreeEntry()) } }.ToJsonString()
        );

        ClaudeTrust.Retire(Config, worktree).ShouldBeTrue();

        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Config)), Before()).ShouldBeTrue(File.ReadAllText(Config));
    }

    [Fact]
    public void Retiring_takes_back_the_marked_entry_and_leaves_the_unmarked_ones_and_another_directorys_untouched()
    {
        var worktree = LinkedWorktree();
        var checkout = Path.GetDirectoryName(Path.GetDirectoryName(worktree))!;
        var other = Path.Combine(_root, "other");
        File.WriteAllText(
            Config,
            new JsonObject
            {
                ["projects"] = new JsonObject
                {
                    [worktree] = Marked(worktree),
                    [checkout] = new JsonObject { ["hasTrustDialogAccepted"] = true },
                    [other] = Marked(other),
                },
            }.ToJsonString()
        );

        ClaudeTrust.Retire(Config, worktree).ShouldBeTrue();

        var projects = JsonNode.Parse(File.ReadAllText(Config))!["projects"]!.AsObject();
        projects.ContainsKey(worktree).ShouldBeFalse("the entry with the mark for this worktree goes");
        JsonNode.DeepEquals(projects[checkout], new JsonObject { ["hasTrustDialogAccepted"] = true }).ShouldBeTrue("an entry without the mark stays as it was");
        JsonNode.DeepEquals(projects[other], Marked(other)).ShouldBeTrue("an entry marked for another directory stays");
        var unchanged = File.ReadAllText(Config);

        ClaudeTrust.Retire(Config, worktree).ShouldBeFalse();
        File.ReadAllText(Config).ShouldBe(unchanged);
    }

    [Fact]
    public void Retiring_with_no_file_writes_none()
    {
        ClaudeTrust.Retire(Config, Worktree).ShouldBeFalse();
        File.Exists(Config).ShouldBeFalse();
    }

    [Fact]
    public void Retiring_keeps_the_file_readable_only_by_its_owner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var worktree = Worktree;
        File.WriteAllText(Config, new JsonObject { ["projects"] = new JsonObject { [worktree] = Marked(worktree) } }.ToJsonString());
        File.SetUnixFileMode(Config, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        ClaudeTrust.Retire(Config, worktree).ShouldBeTrue();

        File.GetUnixFileMode(Config).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>A directory whose <c>.git</c> file names a main checkout, as <c>git worktree add</c> leaves it.</summary>
    string LinkedWorktree()
    {
        var worktree = Worktree;
        var checkout = Path.GetDirectoryName(Path.GetDirectoryName(worktree))!;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {Path.Combine(checkout, ".git", "worktrees", "change-1")}\n");
        return worktree;
    }
}
