using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #667. Running a state's scripts, on its own, against the scripts, the vendor and the harness held
/// in memory: a merge with no pull request, the assignment after a claim, <c>laya ensure</c> before a
/// score, and the one place a state's scripts share (#751). The order, the branch, the
/// <c>started</c> and <c>ran</c> lines, a wait and the output kept are RunOnceTests',
/// RunningScriptTests' and ScriptOutputTests' through the API (#668).
/// </summary>
public sealed class StateScriptsTests : IDisposable
{
    static readonly LoopDefinition Loop =
        LoopReader.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "loop.yaml"))).Loop!;

    readonly FakeScripts _scripts = new();
    readonly FakeRuns _runs;
    readonly FakeVendor _vendor = new();
    readonly FakeFolders _folders = new();
    readonly FakeHarness _harness = new();
    readonly Passes _passes = new();
    readonly SqliteConnection _connection = new("DataSource=:memory:");
    readonly LoopDbContext _database;
    readonly PassJournal _journal;
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));

    public StateScriptsTests()
    {
        _runs = new FakeRuns(_scripts);
        _connection.Open();
        _database = new LoopDbContext(new DbContextOptionsBuilder<LoopDbContext>().UseSqlite(_connection).Options);
        _database.Database.EnsureCreated();
        _journal = new PassJournal(_database, _clock);
    }

    [Fact]
    public async Task A_merge_without_a_pull_request_fails_before_it_runs()
    {
        var verdicts = await Scripts().RunAsync(["merge"], State("merging"), Folder(), _vendor, Pbi(6, "merging"), [], CancellationToken.None);

        verdicts.ShouldBe([new ScriptVerdict("merge", VerdictKind.Fail, null, "owner/repo#6 has no pull request to merge")]);
        _scripts.Ran.ShouldBeEmpty();
        Lines().ShouldBe(["ran merging: merge: owner/repo#6 has no pull request to merge"]);
    }

    [Fact]
    public async Task A_passing_claim_assigns_the_PBI_and_a_failing_script_stops_the_list()
    {
        var pbi = Pbi(4, "spec-ready");
        _vendor.Pbis.Add(pbi);
        _scripts.Answers["file-overlap"] = (0, """{"clear": true, "why": "no file shared with a PBI in flight."}""");
        _scripts.Answers["claim"] = (0, """{"claimed": true, "why": "change/4 pushed."}""");

        var verdicts = await Scripts().RunAsync(["file-overlap", "claim"], State("spec-ready"), Folder(), _vendor, pbi, [], CancellationToken.None);

        verdicts.Select(verdict => verdict.Kind).ShouldBe([VerdictKind.Pass, VerdictKind.Pass]);
        _vendor.Writes.ShouldBe(["assign #4"]);

        _vendor.Writes.Clear();
        _scripts.Answers["file-overlap"] = (1, """{"clear": false, "why": "git refused."}""");
        verdicts = await Scripts().RunAsync(["file-overlap", "claim"], State("spec-ready"), Folder(), _vendor, pbi, [], CancellationToken.None);

        verdicts.Select(verdict => verdict.Kind).ShouldBe([VerdictKind.Fail]);
        _vendor.Writes.ShouldBeEmpty();
        _scripts.Ran.Count(run => run.Script == "claim").ShouldBe(1, "the claim did not run after the failure");
    }

    /// <summary>
    /// #751. A state's scripts share one place: prepared by the first that runs and retired once
    /// after the last, whether the list ran to its end or stopped at the first that held.
    /// </summary>
    [Fact]
    public async Task A_states_place_is_prepared_once_and_retired_once_also_when_the_first_script_holds()
    {
        var pbi = Pbi(9, "verifying");
        _scripts.Answers["gate"] = (0, """{"outcome": "green", "why": "every test passed."}""");
        _scripts.Answers["evidence"] = (0, """{"outcome": "skipped", "why": "no screen changed."}""");

        var verdicts = await Scripts().RunAsync(["gate", "evidence", "quality"], State("verifying"), Folder(), _vendor, pbi, [], CancellationToken.None);

        verdicts.Count.ShouldBe(3);
        (_scripts.Prepared, _scripts.Retired).ShouldBe((1, 1));
        _scripts.Ran.Select(run => run.Branch).Distinct().ShouldBe(["change/9"]);

        _scripts.Answers["gate"] = (1, """{"why": "the build did not start."}""");
        verdicts = await Scripts().RunAsync(["gate", "evidence", "quality"], State("verifying"), Folder(), _vendor, pbi, [], CancellationToken.None);

        verdicts.Select(verdict => verdict.Kind).ShouldBe([VerdictKind.Fail]);
        _scripts.Ran.Count.ShouldBe(4, "nothing ran after the gate held");
        (_scripts.Prepared, _scripts.Retired).ShouldBe((2, 2));

        // A merge with no pull request runs nothing, so it prepares nothing either.
        await Scripts().RunAsync(["merge"], State("merging"), Folder(), _vendor, Pbi(6, "merging"), [], CancellationToken.None);
        (_scripts.Prepared, _scripts.Retired).ShouldBe((2, 2));
    }

    [Fact]
    public async Task Laya_ensure_runs_once_a_pass_in_the_folder_before_the_first_score_and_its_failure_is_only_logged()
    {
        // #759. laya is in its own folder, not beside the steps.
        Directory.CreateDirectory(Path.Combine(_folders.Path, ".aio", "scorers", "laya"));
        File.WriteAllText(Path.Combine(_folders.Path, ".aio", "scorers", "laya", "laya"), "#!/bin/sh\n");
        _scripts.Answers["risk-score"] = (0, """{"band": "unknown", "outcome": "unknown", "why": "no laya server on http://127.0.0.1:8317"}""");
        var scripts = Scripts();

        var first = await scripts.RunAsync(["risk-score"], State("proposal-review"), Folder(), _vendor, Pbi(1, "proposal-review"), [], CancellationToken.None);
        await scripts.RunAsync(["risk-score"], State("proposal-review"), Folder(), _vendor, Pbi(3, "proposal-review"), [], CancellationToken.None);

        _harness.Ran.Select(request => $"{request.Script} {string.Join(' ', request.Arguments)} in {request.WorkingDirectory}")
            .ShouldBe([$"laya ensure in {_folders.Path}"]);
        _scripts.Ran.Count.ShouldBe(2);
        first.Single().Outcome.ShouldBe("unknown");

        // A folder without laya, or a pass with no harness, asks for nothing.
        File.Delete(Path.Combine(_folders.Path, ".aio", "scorers", "laya", "laya"));
        await Scripts().RunAsync(["risk-score"], State("proposal-review"), Folder(), _vendor, Pbi(5, "proposal-review"), [], CancellationToken.None);
        _harness.Ran.Count.ShouldBe(1);
    }

    /// <summary>
    /// #729, #779. <c>loop.yaml</c> names a script, never a path: <c>script: gate</c> is the repository's
    /// <c>.aio/gate</c> and <c>script: claim</c> the package's <c>.aio/steps/claim</c>. A checkout that
    /// has not reinstalled since harness 36 runs from <c>.harness/</c>, and one from before harness 24
    /// from <c>.harness/scripts/</c>.
    /// </summary>
    [Fact]
    public void Script_gate_resolves_aio_gate_and_script_claim_resolves_aio_steps_claim()
    {
        var checkout = _folders.Path;
        foreach (var path in new[] { ".aio/gate", ".aio/steps/claim", ".aio/steps/gate", ".aio/archive" })
        {
            Write(checkout, path);
        }

        HarnessScripts.Find(checkout, "gate").ShouldBe(Path.Combine(checkout, ".aio/gate"));
        HarnessScripts.Find(checkout, "claim").ShouldBe(Path.Combine(checkout, ".aio/steps/claim"));
        HarnessScripts.Find(checkout, "archive").ShouldBe(Path.Combine(checkout, ".aio/archive"));
        HarnessScripts.Find(checkout, "merge").ShouldBeNull();

        // #759. laya is a tool, in its own folder under .aio/scorers/.
        HarnessScripts.Find(checkout, "laya").ShouldBeNull();
        Write(checkout, ".aio/scorers/laya/laya");
        HarnessScripts.Find(checkout, "laya").ShouldBe(Path.Combine(checkout, ".aio/scorers/laya/laya"));

        // The loop this repository declares names scripts, not paths.
        Loop.States.SelectMany(state => state.Scripts).ShouldAllBe(name => !name.Contains('/'));
    }

    /// <summary>
    /// #779. A checkout with harness 36 still finds each script where it had it: the repository's gate
    /// at the root of <c>.harness/</c>, <c>archive</c> in <c>.harness/repo/</c>, a step in
    /// <c>.harness/aio/</c>, and <c>laya</c> in <c>.harness/aio/scorers/laya/</c>; before 36, <c>laya</c>
    /// is beside the steps, and before 24 every script is in <c>.harness/scripts/</c>.
    /// </summary>
    [Fact]
    public void A_checkout_with_harness_36_or_older_finds_its_scripts_where_it_had_them()
    {
        var checkout = _folders.Path;
        foreach (var path in new[] { ".harness/gate", ".harness/aio/claim", ".harness/aio/gate", ".harness/scripts/claim", ".harness/scripts/merge", ".harness/repo/archive", ".harness/repo/gate" })
        {
            Write(checkout, path);
        }

        HarnessScripts.Find(checkout, "gate").ShouldBe(Path.Combine(checkout, ".harness/gate"));
        HarnessScripts.Find(checkout, "claim").ShouldBe(Path.Combine(checkout, ".harness/aio/claim"));
        HarnessScripts.Find(checkout, "merge").ShouldBe(Path.Combine(checkout, ".harness/scripts/merge"));
        HarnessScripts.Find(checkout, "archive").ShouldBe(Path.Combine(checkout, ".harness/repo/archive"));

        HarnessScripts.Find(checkout, "laya").ShouldBeNull();
        Write(checkout, ".harness/aio/laya");
        HarnessScripts.Find(checkout, "laya").ShouldBe(Path.Combine(checkout, ".harness/aio/laya"));
        Write(checkout, ".harness/aio/scorers/laya/laya");
        HarnessScripts.Find(checkout, "laya").ShouldBe(Path.Combine(checkout, ".harness/aio/scorers/laya/laya"));

        // The new shape wins over the old one in the same checkout, so an install half moved runs the new.
        Write(checkout, ".aio/steps/claim");
        Write(checkout, ".aio/scorers/laya/laya");
        HarnessScripts.Find(checkout, "claim").ShouldBe(Path.Combine(checkout, ".aio/steps/claim"));
        HarnessScripts.Find(checkout, "laya").ShouldBe(Path.Combine(checkout, ".aio/scorers/laya/laya"));
    }

    /// <summary>
    /// #779. <c>loop.yaml</c>, <c>version</c> and <c>gate</c> of a checkout that has not reinstalled
    /// are read from <c>.harness/</c>, so a repository on harness 36 keeps running.
    /// </summary>
    [Fact]
    public void The_repositorys_own_files_are_read_from_aio_or_else_from_harness()
    {
        var checkout = Directory.CreateTempSubdirectory("aio-own-").FullName;
        try
        {
            HarnessScripts.Own(checkout, "loop.yaml").ShouldBe(Path.Combine(checkout, ".aio", "loop.yaml"));

            Write(checkout, ".harness/loop.yaml");
            HarnessScripts.Own(checkout, "loop.yaml").ShouldBe(Path.Combine(checkout, ".harness", "loop.yaml"));

            Write(checkout, ".aio/loop.yaml");
            HarnessScripts.Own(checkout, "loop.yaml").ShouldBe(Path.Combine(checkout, ".aio", "loop.yaml"));
        }
        finally
        {
            Directory.Delete(checkout, recursive: true);
        }
    }

    static void Write(string checkout, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(checkout, path))!);
        File.WriteAllText(Path.Combine(checkout, path), "#!/bin/sh\n");
    }

    StateScripts Scripts() => new(_runs, _passes, _journal, _clock, NullLogger.Instance, _harness, null);

    LoopFolder Folder() => new(_folders.Path, "github", FakeFolders.Repository);

    static LoopState State(string name) => Loop.States.Single(state => state.Name == name);

    static Pbi Pbi(int id, string state) => new(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, false, FakeVendor.Me);

    /// <summary>What this pass wrote, as the Journey would print it.</summary>
    List<string> Lines() =>
        [.. _journal.Written.Select(line => $"{line.Kind.ToString().ToLowerInvariant()} {line.From}{(line.To is null ? "" : $" → {line.To}")}: {line.Why}")];


    public void Dispose()
    {
        _database.Dispose();
        _connection.Dispose();
    }
}
