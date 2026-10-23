using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.Infrastructure.Files;

namespace AiOrchestrator.InfrastructureTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GitInSpanishCollection
{
    /// <summary>
    /// Alone, because the language is this process's environment and every other test in the
    /// assembly would run its git in Spanish as well.
    /// </summary>
    public const string Name = "git in Spanish";
}

/// <summary>
/// #762. On a machine in Spanish git says "no se pudo encontrar ref remota" where the code looks for
/// "couldn't find remote ref", and the first push to a repository's <c>aio/evidence</c> read as a
/// refusal, so its first recording never reached the branch. <see cref="GitCli"/> runs git in the C
/// locale, whatever the process's.
/// </summary>
[Collection(GitInSpanishCollection.Name)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GitInSpanishTests : IDisposable
{
    static readonly string[] Variables = ["LANG", "LC_ALL"];

    readonly string _root = Directory.CreateTempSubdirectory("aio-spanish-").FullName;
    readonly GitCli _git = new();
    readonly Dictionary<string, string?> _before = Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    string Origin => Path.Combine(_root, "origin.git");

    string Work => Path.Combine(_root, "work");

    public GitInSpanishTests()
    {
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(Origin, "symbolic-ref", "HEAD", "refs/heads/main");
        Git(_root, "clone", "--quiet", "origin.git", "work");
        Git(Work, "config", "user.name", "Test");
        Git(Work, "config", "user.email", "test@example.com");
        Git(Work, "switch", "--quiet", "-c", "main");
        File.WriteAllText(Path.Combine(Work, "README.md"), "A repository.\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Initial revision");
        Git(Work, "push", "--quiet", "origin", "main");

        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(name, "es_ES.UTF-8");
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _before)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task The_first_push_to_an_evidence_branch_that_does_not_exist_creates_it()
    {
        var source = Path.Combine(_root, "data", "runs", "first", "session.cast");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "{\"version\": 2, \"width\": 80, \"height\": 6}\n[0.1, \"o\", \"first run\"]\n");
        var recording = new EvidenceFile(EvidenceBranch.RecordingOf("12", Guid.NewGuid()), source, "12 · in-progress · first");

        var pushed = await new GitEvidenceBranch(_git).PushAsync(
            Work,
            Path.Combine(_root, "data", "runs", ".evidence"),
            [recording],
            CancellationToken.None
        );

        pushed.Refusal.ShouldBeNull();
        pushed.Pushed.ShouldBe([recording.Path]);
        Git(Origin, "ls-tree", "-r", "--name-only", EvidenceBranch.Name).ShouldBe(recording.Path);
    }

    string Git(string directory, params string[] arguments)
    {
        var run = _git.Run(directory, arguments).GetAwaiter().GetResult();
        run.Succeeded.ShouldBeTrue($"git {string.Join(' ', arguments)}: {run.StandardError}");
        return run.StandardOutput.Trim();
    }
}
