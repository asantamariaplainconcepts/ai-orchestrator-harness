using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.PbiRuns;
using AiOrchestrator.Modules.Runs.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The runs (#506) through the real host: a real worktree prepared and retired by the
/// harness scripts, a real PTY recording to asciicast, this repository's own <c>aio</c> and the
/// SQLite file. The one stand-in is the agent CLI, a <c>sh -c</c> whose script is the run's command,
/// because the real one needs a sign-in and a model call per test.
/// </summary>
public sealed class PbiRunTests(PbiRunsFixture fixture) : IClassFixture<PbiRunsFixture>, IDisposable
{
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);
    static readonly string[] Outcomes = ["approved", "rejected"];

    readonly string _root = Directory.CreateTempSubdirectory("aio-pbi-runs-").FullName;

    string Checkout => Path.Combine(_root, "checkout");

    IRunRequests Requests => fixture.Services.GetRequiredService<IRunRequests>();

    RunBook Book => fixture.Services.GetRequiredService<RunBook>();

    /// <summary>
    /// Waits for every run to leave the live set, which is after its place is retired, so the
    /// checkout is not deleted under a <c>cleanup</c> still running.
    /// </summary>
    public void Dispose()
    {
        var live = fixture.Services.GetRequiredService<LiveRuns>();
        Until(() => Task.FromResult(live.Ids().Count), count => count == 0).GetAwaiter().GetResult();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_permission_mode_saved_through_the_api_is_the_one_the_next_run_starts_with()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("9");
        var agent = fixture.Services.GetServices<IAgentProvider>().OfType<StandInAgent>().Single();
        using var client = fixture.CreateClient();

        async Task<AgentPermissions> NextRun()
        {
            var runId = await Requests.StartAsync(
                new RunRequest(pbi, "code-review", Checkout, "change/9", StandInAgent.ProviderName, null, "aio done approved", Outcomes),
                CancellationToken.None
            );
            await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);
            return agent.Launched.Last();
        }

        try
        {
            using var yolo = await client.PutAsJsonAsync("/api/runs/settings", new { permissionMode = "yolo" });
            yolo.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await yolo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permissionMode").GetString().ShouldBe("yolo");
            (await NextRun()).ShouldBe(AgentPermissions.Yolo);

            // Only the mode was sent, so the stale threshold is what it was.
            using var auto = await client.PutAsJsonAsync("/api/runs/settings", new { permissionMode = "auto" });
            (await auto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("staleAfterSeconds").GetInt32().ShouldBe(3);
            (await NextRun()).ShouldBe(AgentPermissions.Auto);
        }
        finally
        {
            using var back = await client.PutAsJsonAsync("/api/runs/settings", new { permissionMode = "allow-list" });
        }

        (await NextRun()).ShouldBe(AgentPermissions.AllowList);
    }

    [Fact]
    public async Task A_state_placed_in_another_environment_starts_wrapped_and_records_and_ends_as_in_a_worktree()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Repository("11");
        var pbi = new PbiRef("acme/elsewhere", "11");
        var wrapping = fixture.Services.GetServices<IEnvironment>().OfType<WrappingEnvironment>().Single();
        var agent = fixture.Services.GetServices<IAgentProvider>().OfType<StandInAgent>().Single();
        using var client = fixture.CreateClient();

        using var chosen = await client.PutAsJsonAsync(
            "/api/runs/environments",
            new { repository = pbi.Repository, state = "code-review", environment = WrappingEnvironment.EnvironmentName }
        );
        chosen.StatusCode.ShouldBe(HttpStatusCode.OK);

        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "code-review", Checkout, "change/11", StandInAgent.ProviderName, null,
                "echo reviewing the diff; aio done approved --reason 'reviewed where the folder said'; sleep 120",
                Outcomes),
            CancellationToken.None
        );
        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        // The end record is read where AIO_DONE_FILE said, as for a worktree.
        status!.End!.Outcome.ShouldBe("approved");
        status.End.Reason.ShouldBe("reviewed where the folder said");
        status.End.Failure.ShouldBeNull();

        // The session started the wrapped launch: the wrapper's line, then the agent's, in one recording.
        var view = (await Book.ListAsync(pbi, CancellationToken.None)).Single();
        File.ReadLines(view.RecordingPath).First().ShouldContain("\"version\":2");
        var recording = File.ReadAllText(view.RecordingPath);
        recording.ShouldContain(WrappingEnvironment.Announcement);
        recording.IndexOf(WrappingEnvironment.Announcement, StringComparison.Ordinal)
            .ShouldBeLessThan(recording.IndexOf("reviewing the diff", StringComparison.Ordinal));
        wrapping.Entered.ShouldContain(launch => launch.FileName == "/bin/sh" && launch.Arguments.Last().Contains("reviewed where the folder said"));

        // The environment's own mode wins over the instance's allow-list, and its place is the one retired.
        agent.Launched.Last().ShouldBe(AgentPermissions.Yolo);
        (await PlacedIn(runId)).ShouldBe(WrappingEnvironment.EnvironmentName);
        await Until(() => Task.FromResult(wrapping.Retired.Count(place => place.WorkingDirectory == Checkout)), count => count > 0);
    }

    [Fact]
    public async Task Without_a_choice_a_state_runs_in_a_worktree_and_a_choice_holds_for_its_state_only()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Repository("12");
        var pbi = new PbiRef("acme/chooser", "12");
        using var client = fixture.CreateClient();

        async Task<string?> NextRun(string state)
        {
            var runId = await Requests.StartAsync(
                new RunRequest(pbi, state, Checkout, "change/12", StandInAgent.ProviderName, null, "aio done approved", Outcomes),
                CancellationToken.None
            );
            await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);
            await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);
            return await PlacedIn(runId);
        }

        (await NextRun("code-review")).ShouldBe("worktree");

        using var unknown = await client.PutAsJsonAsync(
            "/api/runs/environments",
            new { repository = pbi.Repository, state = "code-review", environment = "nowhere" }
        );
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await unknown.Content.ReadAsStringAsync()).ShouldContain("No environment is called 'nowhere'.");

        // The repository as the vendor may spell it: one repository, one choice.
        using var chosen = await client.PutAsJsonAsync(
            "/api/runs/environments",
            new { repository = "Acme/Chooser", state = "code-review", environment = WrappingEnvironment.EnvironmentName }
        );
        chosen.StatusCode.ShouldBe(HttpStatusCode.OK);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/runs/environments?repository={pbi.Repository}");
        var states = read.GetProperty("states").EnumerateArray().ToList();
        states.Count.ShouldBe(1);
        states[0].GetProperty("state").GetString().ShouldBe("code-review");
        states[0].GetProperty("environment").GetString().ShouldBe(WrappingEnvironment.EnvironmentName);

        (await NextRun("in-progress")).ShouldBe("worktree");
        (await NextRun("code-review")).ShouldBe(WrappingEnvironment.EnvironmentName);
    }

    /// <summary>The environment a run's row says prepared its place.</summary>
    async Task<string?> PlacedIn(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return (await database.PbiRuns.FindAsync(runId))?.Environment;
    }

    [Fact]
    public async Task A_run_the_engine_starts_ends_with_its_outcome_its_cost_and_its_recording()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("7");
        var clock = Stopwatch.StartNew();

        // An agent that, like interactive claude, stays open after it has said it is done.
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "code-review", Checkout, "change/7", StandInAgent.ProviderName, null,
                "echo reviewing the diff; aio done approved --reason 'the diff does what the issue asks'; sleep 120",
                Outcomes),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60), "the session is killed once the end record is there");
        status!.End!.Outcome.ShouldBe("approved");
        status.End.Reason.ShouldBe("the diff does what the issue asks");
        status.End.Failure.ShouldBeNull();
        status.Stale.ShouldBeFalse();
        (await Requests.ListLiveAsync(CancellationToken.None)).ShouldNotContain(live => live.RunId == runId);

        var view = (await Book.ListAsync(pbi, CancellationToken.None)).Single();
        view.Manual.ShouldBeFalse();
        view.InputTokens.ShouldBe(StandInAgent.Usage.InputTokens);
        view.OutputTokens.ShouldBe(StandInAgent.Usage.OutputTokens);
        view.CostUsd.ShouldBe(StandInAgent.Usage.CostUsd);
        view.EndRecord.ShouldNotBeNull().ShouldContain("\"approved\"");
        File.ReadAllText(view.RecordingPath).ShouldContain("reviewing the diff");
        File.ReadLines(view.RecordingPath).First().ShouldContain("\"version\":2");

        // Retired just after the row ends, and before the run leaves the live set.
        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);
        Directory.Exists(Path.Combine(Checkout, ".worktrees", "change-7")).ShouldBeFalse("the place is retired");
        // #697. Once it ended, its recording goes to aio/evidence from the folder's checkout, and the
        // loop is told so the journey comment lists it with that path.
        var path = $"7/{runId:N}/session.cast";
        status.Recording.ShouldBe(path);
        var evidence = fixture.Services.GetRequiredService<StubEvidence>();
        var pushed = await Until(
            () => Task.FromResult(evidence.Pushes.Where(push => push.Files.Any(file => file.Path == path)).ToList()),
            found => found.Count > 0
        );
        pushed[0].Checkout.ShouldBe(Checkout);
        pushed[0].Files.Single(file => file.Path == path).ShouldBe(new EvidenceFile(path, view.RecordingPath, $"7 · code-review · {runId:N}"));
        await Until(() => Task.FromResult(evidence.Ended.Contains(pbi)), told => told);
    }

    /// <summary>
    /// #697. A checkout without <c>.aio/version</c>, such as a client's repository, keeps its
    /// recordings local: nothing is pushed, and the loop is told the run ended all the same.
    /// </summary>
    [Fact]
    public async Task A_run_in_a_folder_without_the_harness_pushes_no_recording()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("21", harness: false);
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/21", StandInAgent.ProviderName, null, "echo working; aio done next --reason done", ["next"]),
            CancellationToken.None
        );

        await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);
        var evidence = fixture.Services.GetRequiredService<StubEvidence>();
        await Until(() => Task.FromResult(evidence.Ended.Contains(pbi)), told => told);

        evidence.Pushes.ShouldNotContain(push => push.Checkout == Checkout);
    }

    [Fact]
    public async Task A_run_whose_agent_never_calls_aio_done_fails_with_that_cause()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("8");

        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/8", StandInAgent.ProviderName, null, "echo forgot", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBe(RunFailure.NoEndRecord);
        status.End.Outcome.ShouldBeNull();
    }

    [Theory]
    [InlineData("Do you want to proceed?", RunFailure.WaitingForPerson, "The stand-in asked for a permission. Switch permissions to auto in Settings, then release.")]
    [InlineData("Login expired · Please run /login", RunFailure.NotLoggedIn, "The stand-in is not logged in. Run its login on this machine, then release.")]
    public async Task A_run_silent_on_a_phrase_of_its_cli_ends_waiting_for_a_person(string phrase, RunFailure cause, string why)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("20");
        var started = Stopwatch.StartNew();

        // #706. Silent after the phrase for longer than Runs:PersonWait, which is one second here.
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/20", StandInAgent.ProviderName, null, $"printf '%s\\n' '{phrase}'; sleep 30", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBe(cause);
        status.End.Reason.ShouldBe(why);
        started.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(1));
        started.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(25), "the session was killed, not left to finish its sleep");
    }

    [Fact]
    public async Task A_run_that_prints_a_phrase_and_goes_on_working_is_not_ended()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("21");

        // #706. The phrase scrolls out of what the stand-in reads, then a silence longer than
        // Runs:PersonWait, then the end record.
        var runId = await Requests.StartAsync(
            new RunRequest(
                pbi,
                "code-review",
                Checkout,
                "change/21",
                StandInAgent.ProviderName,
                null,
                "echo 'Do you want to proceed?'; for i in $(seq 1 30); do echo still working on step $i; sleep 0.05; done; sleep 2; aio done approved --reason 'went on'",
                Outcomes
            ),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBeNull(status.End.Reason);
        status.End.Outcome.ShouldBe("approved");
    }

    [Fact]
    public async Task A_provider_that_does_not_exist_fails_the_run_as_agent_not_found()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("9");

        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/9", "no-such-cli", null, "/aio:implement 9", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBe(RunFailure.AgentNotFound);
        status.End.Reason.ShouldNotBeNull().ShouldContain("no-such-cli");
    }

    [Fact]
    public async Task A_folder_that_is_not_a_repository_fails_the_run_in_the_environments_words()
    {
        var runId = await Requests.StartAsync(
            new RunRequest(new PbiRef("acme/widgets", "10"), "in-progress", _root, "change/10", StandInAgent.ProviderName, null, "true", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBe(RunFailure.EnvironmentFailed);
        status.End.Reason.ShouldNotBeNull().ShouldContain("is not a git repository");
    }

    [Fact]
    public async Task A_terminal_on_a_pbi_runs_the_shell_until_a_person_stops_it()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("12");

        var runId = await Book.OpenAsync(pbi, "in-progress", Checkout, "change/12", null, null, null, CancellationToken.None);

        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)?.Session), session => session is not null);
        (await Book.StopAsync(runId, CancellationToken.None)).ShouldBeTrue();

        var view = await Until(
            async () => (await Book.ListAsync(pbi, CancellationToken.None)).Single(),
            found => found.EndedAt is not null
        );

        view.Manual.ShouldBeTrue();
        view.Provider.ShouldBeNull();
        File.Exists(view.RecordingPath).ShouldBeTrue();
        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);
        (await Book.StopAsync(runId, CancellationToken.None)).ShouldBeFalse("a run that has left the live set has nothing to stop");
    }

    [Fact]
    public async Task A_loop_run_stopped_from_the_header_ends_as_stopped_by_a_person()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("17");

        // #511. An agent that would have said done, had a person not stopped it first.
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/17", StandInAgent.ProviderName, null,
                "echo implementing; sleep 120; aio done next", ["next"]),
            CancellationToken.None
        );
        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)?.Session), session => session is not null);

        using var client = fixture.CreateClient();
        using (var stopped = await client.PostAsync($"/api/runs/{runId}/stop", null))
        {
            stopped.StatusCode.ShouldBe(HttpStatusCode.NoContent, await stopped.Content.ReadAsStringAsync());
        }

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);
        status!.End!.Failure.ShouldBe(RunFailure.Stopped);
        status.End.Reason.ShouldBe("Stopped by a person.");
        status.End.Outcome.ShouldBeNull();

        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);
        using var again = await client.PostAsync($"/api/runs/{runId}/stop", null);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict, "a run that ended has no session to stop");
    }

    [Fact]
    public async Task A_manual_run_silent_past_the_threshold_reads_stale_and_reads_live_again_once_it_writes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("13");

        // Silent for five seconds against a three-second threshold, then writing steadily. A manual
        // run, because a loop run that silent is ended (#764).
        var runId = await Book.OpenAsync(pbi, "in-progress", Checkout, "change/13", StandInAgent.ProviderName, null,
            "echo started; sleep 5; while true; do echo working; sleep 0.2; done", CancellationToken.None);

        async Task<PbiRunView?> Read() => (await Book.ListAsync(pbi, CancellationToken.None)).SingleOrDefault(run => run.RunId == runId);

        try
        {
            var silent = await Until(Read, found => found?.Stale == true);
            silent!.EndedAt.ShouldBeNull();
            silent.LastOutputAt.ShouldNotBeNull();

            var writing = await Until(Read, found => found?.Stale == false);
            writing!.EndedAt.ShouldBeNull();
            writing.LastOutputAt!.Value.ShouldBeGreaterThan(silent.LastOutputAt!.Value);
        }
        finally
        {
            await Book.StopAsync(runId, CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_loop_run_silent_past_the_threshold_without_its_end_record_fails_waiting_for_input_with_its_screen()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("22");
        var started = Stopwatch.StartNew();

        // #764. The agent asks a person in words no adapter knows, then sits at its prompt.
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "retro", Checkout, "change/22", StandInAgent.ProviderName, null,
                "printf '%s\\n' 'The retro is written.' 'Allow the commit and push, or run it yourself.'; sleep 60", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBe(RunFailure.WaitingForInput);
        status.End.Reason.ShouldBe(
            "The agent wrote nothing for 3s and did not call aio done, so its session was stopped. Do what it asks, then release. "
                + "Its screen ended with:\nThe retro is written.\nAllow the commit and push, or run it yourself."
        );
        started.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(3));
        started.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30), "the session was stopped, not left to finish its sleep");
    }

    [Fact]
    public async Task A_loop_run_that_writes_for_longer_than_the_threshold_is_not_stopped()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("23");
        var started = Stopwatch.StartNew();

        // #764. Eight seconds of steady output against a three-second threshold, then the end record.
        var runId = await Requests.StartAsync(
            new RunRequest(pbi, "in-progress", Checkout, "change/23", StandInAgent.ProviderName, null,
                "for i in $(seq 1 40); do echo working on step $i; sleep 0.2; done; aio done next --reason 'wrote all along'", ["next"]),
            CancellationToken.None
        );

        var status = await Until(() => Requests.GetAsync(runId, CancellationToken.None), found => found?.End is not null);

        status!.End!.Failure.ShouldBeNull(status.End.Reason);
        status.End.Outcome.ShouldBe("next");
        started.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task The_engine_reaches_these_runs_and_not_the_stand_in_that_refuses()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IRunRequests>().ShouldBeOfType<RunBook>();

        using var client = fixture.CreateClient();
        var pass = await client.PostAsync("/api/loop/run-once", null);

        var said = await pass.Content.ReadAsStringAsync();
        pass.IsSuccessStatusCode.ShouldBeTrue(said);
        said.ShouldNotContain("RunsNotWired");
    }

    [Fact]
    public async Task A_script_of_the_loop_runs_on_the_pbis_branch_and_its_place_is_retired_after()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Says where it ran: the branch and the checkout's top.
        var where = Path.Combine(Checkout, ".aio", "where");
        Directory.CreateDirectory(Path.GetDirectoryName(where)!);
        await File.WriteAllTextAsync(
            where,
            """
            #!/bin/sh
            printf '{"branch": "%s", "top": "%s"}\n' "$(git branch --show-current)" "$(git rev-parse --show-toplevel)"

            """
        );
        File.SetUnixFileMode(where, (UnixFileMode)0b111_101_101);
        var pbi = await Repository("15");

        ScriptResult result;
        await using (var place = Requests.OpenScriptPlace(pbi, Checkout, "change/15"))
        {
            result = await place.RunAsync("where", ["15"], CancellationToken.None);
        }

        result.ExitCode.ShouldBe(0, result.Output);
        var line = result.Line.ShouldNotBeNull();
        line.GetProperty("branch").GetString().ShouldBe("change/15");
        line.GetProperty("top").GetString().ShouldNotBeNull().ShouldEndWith(Path.Combine(".worktrees", "change-15"));
        Directory.Exists(Path.Combine(Checkout, ".worktrees", "change-15")).ShouldBeFalse("the place is retired after the script");
    }

    /// <summary>
    /// #751. The scripts of a state run in one place: what the first leaves uncommitted, in a folder
    /// git ignores as the gate's reports are, is there for the second. Retired after each script, the
    /// worktree took the file with it and the second found nothing.
    /// </summary>
    [Fact]
    public async Task The_second_script_of_a_state_finds_what_the_first_left_uncommitted()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var leave = Path.Combine(Checkout, ".aio", "leave");
        var find = Path.Combine(Checkout, ".aio", "find");
        Directory.CreateDirectory(Path.GetDirectoryName(leave)!);
        await File.WriteAllTextAsync(leave, "#!/bin/sh\nmkdir -p artifacts && echo 91 > artifacts/coverage.txt\necho '{\"why\": \"left the report.\"}'\n");
        await File.WriteAllTextAsync(
            find,
            "#!/bin/sh\nif [ -f artifacts/coverage.txt ]; then echo '{\"found\": true}'; else echo '{\"found\": false}'; fi\n"
        );
        File.SetUnixFileMode(leave, (UnixFileMode)0b111_101_101);
        File.SetUnixFileMode(find, (UnixFileMode)0b111_101_101);
        var pbi = await Repository("22");
        await File.AppendAllTextAsync(Path.Combine(Checkout, ".gitignore"), "artifacts/\n");
        await Git(Checkout, "commit", "--quiet", "-am", "reports are not committed");

        ScriptResult found;
        await using (var place = Requests.OpenScriptPlace(pbi, Checkout, "change/22"))
        {
            (await place.RunAsync("leave", ["22"], CancellationToken.None)).ExitCode.ShouldBe(0);
            found = await place.RunAsync("find", ["22"], CancellationToken.None);
        }

        found.Line.ShouldNotBeNull().GetProperty("found").GetBoolean().ShouldBeTrue(found.Output);
        Directory.Exists(Path.Combine(Checkout, ".worktrees", "change-22")).ShouldBeFalse("the place is retired after the last script");
    }

    [Fact]
    public async Task A_script_whose_place_cannot_be_prepared_answers_no_line_and_the_environments_words()
    {
        await using var place = Requests.OpenScriptPlace(new PbiRef("acme/widgets", "16"), _root, "change/16");
        var result = await place.RunAsync("gate", ["16"], CancellationToken.None);

        result.ExitCode.ShouldBe(-1);
        result.Line.ShouldBeNull();
        result.Output.ShouldStartWith("change/16 could not be prepared for gate:");
        result.Output.ShouldContain("is not a git repository");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_run_the_server_lost_while_live_ends_from_its_end_record_the_first_time_it_is_read(bool recorded)
    {
        var run = PbiRun.Loop(
            new RunRequest(new PbiRef("acme/widgets", "14"), "code-review", _root, "change/14", StandInAgent.ProviderName, null, "x", Outcomes),
            _root,
            DateTimeOffset.UtcNow.AddMinutes(-5)
        );

        if (recorded)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(run.EndRecordPath)!);
            await File.WriteAllTextAsync(
                run.EndRecordPath,
                $$"""{"version": 1, "outcome": "rejected", "reason": "two tests fail", "at": "{{DateTimeOffset.UtcNow:O}}"}"""
            );
        }

        await fixture.With<RunsDbContext>(async database =>
        {
            database.PbiRuns.Add(run);
            await database.SaveChangesAsync();
        });

        var status = await Requests.GetAsync(run.Id, CancellationToken.None);

        if (recorded)
        {
            status!.End!.Outcome.ShouldBe("rejected");
            status.End.Reason.ShouldBe("two tests fail");
        }
        else
        {
            status!.End!.Failure.ShouldBe(RunFailure.NoEndRecord);
            status.End.Reason.ShouldNotBeNull().ShouldStartWith("The control plane stopped while the run was live.");
        }

        (await Requests.ListLiveAsync(CancellationToken.None)).ShouldNotContain(live => live.RunId == run.Id);
    }

    /// <summary>
    /// #508. A browser attaches to a live terminal over the WebSocket, reads it and types into it,
    /// and closing the socket leaves the run running; a second viewer gets what the first saw. The
    /// tray lists the session while it lives, and the Journey reads the run and its recording.
    /// </summary>
    [Fact]
    public async Task A_live_session_is_attached_over_a_websocket_and_stays_live_when_detached()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("15");
        var runId = await Book.OpenAsync(pbi, "in-progress", Checkout, "change/15", null, null, null, CancellationToken.None);
        var live = fixture.Services.GetRequiredService<LiveRuns>();
        await Until(() => Task.FromResult(live.Find(runId)?.Session), session => session is not null);

        try
        {
            using var client = fixture.CreateClient();
            var sessions = await client.GetFromJsonAsync<JsonElement>("/api/sessions");
            var session = sessions.EnumerateArray().Single(row => row.GetProperty("runId").GetGuid() == runId);
            session.GetProperty("manual").GetBoolean().ShouldBeTrue();
            session.GetProperty("pbi").GetString().ShouldBe("15");

            var sockets = fixture.Server.CreateWebSocketClient();
            var attach = new Uri(fixture.Server.BaseAddress, $"/api/sessions/{runId}/attach");

            using (var first = await sockets.ConnectAsync(attach, CancellationToken.None))
            {
                await first.SendAsync("{\"columns\": 100, \"rows\": 40}"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
                await first.SendAsync(Encoding.UTF8.GetBytes("echo attached-$((500 + 8))\r"), WebSocketMessageType.Binary, true, CancellationToken.None);
                (await ReadUntil(first, "attached-508")).ShouldContain("attached-508");

                await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing the tray", CancellationToken.None);
            }

            await Task.Delay(500);
            live.Find(runId)?.Session.ShouldNotBeNull("detaching stops nothing");
            (await Book.ListAsync(pbi, CancellationToken.None)).Single().EndedAt.ShouldBeNull();

            // A second viewer starts from the tail, so it sees what the first one typed.
            using (var second = await sockets.ConnectAsync(attach, CancellationToken.None))
            {
                (await ReadUntil(second, "attached-508")).ShouldContain("attached-508");
                await second.SendAsync("exit\r"u8.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);

                // The shell exits, and the server closes the socket saying why.
                var closed = await Drain(second);
                closed.ShouldBe("The session ended.");
            }

            var run = await Until(
                async () => (await client.GetFromJsonAsync<JsonElement>("/api/runs?repository=acme/widgets&pbi=15")).EnumerateArray().Single(),
                row => row.GetProperty("endedAt").ValueKind != JsonValueKind.Null
            );
            run.GetProperty("manual").GetBoolean().ShouldBeTrue();
            run.GetProperty("recorded").GetBoolean().ShouldBeTrue();

            using var recording = await client.GetAsync($"/api/runs/{runId}/recording");
            recording.StatusCode.ShouldBe(HttpStatusCode.OK);
            recording.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-asciicast");
            (await recording.Content.ReadAsStringAsync()).ShouldContain("attached-508");

            await Until(() => Task.FromResult(live.Find(runId)), found => found is null);
            (await client.GetFromJsonAsync<JsonElement>("/api/sessions")).EnumerateArray()
                .ShouldNotContain(row => row.GetProperty("runId").GetGuid() == runId);
        }
        finally
        {
            await Book.StopAsync(runId, CancellationToken.None);
        }
    }

    /// <summary>
    /// #516. Explore with agent on a PBI, through the API the header calls: a manual run of the state
    /// the vendor says the PBI is in, on its branch, with its recording, and nothing written back.
    /// </summary>
    [Fact]
    public async Task Explore_with_agent_on_a_pbi_is_a_manual_run_of_its_state_with_its_recording_and_moves_nothing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pbi = await Repository("16");
        var vendor = fixture.Services.GetRequiredService<StubVendor>();
        vendor.Arrange(new LoopFolder(Checkout, StubVendor.VendorName, pbi.Repository), new Pbi(pbi, "Explore me", "spec-ready", false, null));

        using var client = fixture.CreateClient();
        using var opened = await client.PostAsJsonAsync(
            "/api/pbi/sessions",
            new { repository = pbi.Repository, id = "16", provider = StandInAgent.ProviderName, model = "", command = "echo exploring; aio done approved" }
        );
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());
        var runId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();
        (await Requests.ListLiveAsync(CancellationToken.None)).ShouldNotContain(live => live.RunId == runId);

        var run = await Until(
            async () => (await client.GetFromJsonAsync<JsonElement>("/api/runs?repository=acme/widgets&pbi=16")).EnumerateArray().Single(),
            row => row.GetProperty("endedAt").ValueKind != JsonValueKind.Null
        );

        run.GetProperty("runId").GetGuid().ShouldBe(runId);
        run.GetProperty("manual").GetBoolean().ShouldBeTrue();
        run.GetProperty("state").GetString().ShouldBe("spec-ready");
        run.GetProperty("recorded").GetBoolean().ShouldBeTrue();
        (await Requests.GetAsync(runId, CancellationToken.None)).ShouldBeNull("the loop never sees a manual run");
        vendor.Writes.ShouldBeEmpty("a manual session moves nothing at the vendor");

        var row = (await Book.ListAsync(pbi, CancellationToken.None)).Single();
        row.End.ShouldBeNull("its aio done is not an outcome");
        row.EndRecord.ShouldNotBeNull().ShouldContain("\"approved\"");
        File.ReadAllText(row.RecordingPath).ShouldContain("exploring");
    }

    [Fact]
    public async Task A_session_with_an_agent_this_machine_has_no_provider_for_is_refused()
    {
        using var client = fixture.CreateClient();

        using var pbi = await client.PostAsJsonAsync("/api/pbi/sessions", new { repository = "acme/widgets", id = "1", provider = "nobody" });
        pbi.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await pbi.Content.ReadAsStringAsync()).ShouldContain("No agent provider is called 'nobody'");

        Directory.CreateDirectory(Checkout);
        using var folder = await client.PostAsJsonAsync("/api/folders/sessions", new { path = Checkout, provider = "nobody" });
        folder.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var missing = await client.PostAsJsonAsync("/api/folders/sessions", new { path = Path.Combine(_root, "gone") });
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadAsStringAsync()).ShouldContain("is not a directory");
    }

    /// <summary>
    /// #516. A terminal on a folder runs in the checkout, is in the tray with its folder and no PBI,
    /// stops from the same route as a run, and leaves no row behind.
    /// </summary>
    [Fact]
    public async Task A_terminal_on_a_folder_is_in_the_tray_and_leaves_nothing_behind()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Checkout);
        using var client = fixture.CreateClient();
        using var opened = await client.PostAsJsonAsync("/api/folders/sessions", new { path = Checkout });
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());
        var runId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();

        var session = (await client.GetFromJsonAsync<JsonElement>("/api/sessions")).EnumerateArray().Single(row => row.GetProperty("runId").GetGuid() == runId);
        session.GetProperty("pbi").ValueKind.ShouldBe(JsonValueKind.Null);
        session.GetProperty("provider").ValueKind.ShouldBe(JsonValueKind.Null);
        session.GetProperty("folderPath").GetString().ShouldBe(Checkout);
        session.GetProperty("manual").GetBoolean().ShouldBeTrue();

        using (var stopped = await client.PostAsync($"/api/runs/{runId}/stop", null))
        {
            stopped.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);
        (await client.GetFromJsonAsync<JsonElement>("/api/sessions")).EnumerateArray()
            .ShouldNotContain(row => row.GetProperty("runId").GetGuid() == runId);

        using var scope = fixture.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RunsDbContext>().PbiRuns.Count(run => run.Id == runId).ShouldBe(0);
    }

    /// <summary>#516. An agent on a folder runs its command in the checkout, and its end record goes with it.</summary>
    [Fact]
    public async Task An_agent_on_a_folder_runs_in_the_checkout_and_its_end_record_goes_with_it()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Repository("17");
        using var client = fixture.CreateClient();
        using var opened = await client.PostAsJsonAsync(
            "/api/folders/sessions",
            new { path = Checkout, provider = StandInAgent.ProviderName, command = "pwd > where.txt; aio done approved" }
        );
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());
        var runId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();

        await Until(() => Task.FromResult(fixture.Services.GetRequiredService<LiveRuns>().Find(runId)), found => found is null);

        File.ReadAllText(Path.Combine(Checkout, "where.txt")).Trim().ShouldEndWith("checkout");
        Directory.Exists(Path.Combine(Path.GetTempPath(), "aio-folder-sessions", runId.ToString("N"))).ShouldBeFalse();
    }

    [Fact]
    public async Task A_session_that_is_not_live_is_not_attached()
    {
        var sockets = fixture.Server.CreateWebSocketClient();
        var attach = new Uri(fixture.Server.BaseAddress, $"/api/sessions/{Guid.NewGuid()}/attach");

        await Should.ThrowAsync<InvalidOperationException>(() => sockets.ConnectAsync(attach, CancellationToken.None));

        using var client = fixture.CreateClient();
        using var plain = await client.GetAsync(attach);
        plain.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var recording = await client.GetAsync($"/api/runs/{Guid.NewGuid()}/recording");
        recording.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Reads binary frames until their text holds <paramref name="expected"/>.</summary>
    static async Task<string> ReadUntil(WebSocket socket, string expected)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        var seen = new StringBuilder();
        var buffer = new byte[16 * 1024];

        while (!seen.ToString().Contains(expected, StringComparison.Ordinal))
        {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            received.MessageType.ShouldBe(WebSocketMessageType.Binary, seen.ToString());
            seen.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
        }

        return seen.ToString();
    }

    /// <summary>Reads until the server closes the socket, and answers its reason.</summary>
    static async Task<string?> Drain(WebSocket socket)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        var buffer = new byte[16 * 1024];

        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                return received.CloseStatusDescription;
            }
        }
    }

    /// <summary>
    /// A checkout carrying this repository's <c>aio</c>, with no origin, so <c>prepare</c> branches
    /// from where it stands. Returns the PBI the test's run works on.
    /// </summary>
    /// <param name="harness">Whether the checkout carries <c>.aio/version</c>, which a push to aio/evidence needs (#697).</param>
    async Task<PbiRef> Repository(string id, bool harness = true)
    {
        Directory.CreateDirectory(Checkout);
        if (harness)
        {
            Directory.CreateDirectory(Path.Combine(Checkout, ".aio"));
            await File.WriteAllTextAsync(Path.Combine(Checkout, ".aio", "version"), "16\n");
        }

        await Git(Checkout, "init", "--quiet", "--initial-branch=master");
        await Git(Checkout, "config", "user.name", "PBI run tests");
        await Git(Checkout, "config", "user.email", "pbi-run-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");

        var aio = Path.Combine(Checkout, ".aio", "aio");
        Directory.CreateDirectory(Path.GetDirectoryName(aio)!);
        File.Copy(Path.Combine(ThisRepository(), ".aio", "aio"), aio);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(aio, (UnixFileMode)0b111_101_101);
        }

        await File.WriteAllTextAsync(Path.Combine(Checkout, ".gitignore"), ".worktrees/\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");

        return new PbiRef("acme/widgets", id);
    }

    static async Task<T> Until<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        using var deadline = new CancellationTokenSource(Deadline);

        while (true)
        {
            var value = await read();

            if (done(value))
            {
                return value;
            }

            await Task.Delay(100, deadline.Token);
        }
    }

    static async Task Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start)!;
        var error = await git.StandardError.ReadToEndAsync();
        await git.WaitForExitAsync();
        git.ExitCode.ShouldBe(0, error);
    }

    static string ThisRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".aio")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No .aio directory above the test binary.");
    }
}

/// <summary>
/// The Runs host for <see cref="PbiRunTests"/>: its own run directory, a three-second stale threshold
/// and a short grace, and the stand-in agent registered beside the real ones.
/// </summary>
public sealed class PbiRunsFixture : ApiServiceFixtureBase
{
    readonly string _runs = Directory.CreateTempSubdirectory("aio-pbi-run-files-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Runs:Directory", _runs);
        builder.UseSetting("Runs:StaleAfter", "00:00:03");
        builder.UseSetting("Runs:EndGrace", "00:00:00.500");
        builder.UseSetting("Runs:PersonWait", "00:00:01");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAgentProvider, StandInAgent>();

            // #612: a second environment beside the worktree, which only a folder's choice reaches.
            services.AddSingleton<IEnvironment, WrappingEnvironment>();

            // #697: where a recording is copied once its run ended, and the loop told, written down
            // rather than pushed: these checkouts have no origin.
            services.AddSingleton<StubEvidence>();
            services.RemoveAll<IEvidenceBranch>();
            services.AddSingleton<IEvidenceBranch>(provider => provider.GetRequiredService<StubEvidence>());
            services.RemoveAll<IRunEndings>();
            services.AddSingleton<IRunEndings>(provider => provider.GetRequiredService<StubEvidence>());

            // #516: the folder and the vendor a session on a PBI reads, standing in for GitHub.
            services.AddSingleton<StubVendor>();
            services.AddSingleton<IPbiProvider>(provider => provider.GetRequiredService<StubVendor>());
            services.AddSingleton<IFolderDirectory>(provider => provider.GetRequiredService<StubVendor>());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_runs, recursive: true);
    }
}

/// <summary>#697. Every push of recordings to aio/evidence and every run end the loop was told of.</summary>
sealed class StubEvidence : IEvidenceBranch, IRunEndings
{
    public ConcurrentQueue<(string Checkout, IReadOnlyList<EvidenceFile> Files)> Pushes { get; } = new();

    public ConcurrentQueue<PbiRef> Ended { get; } = new();

    public Task<EvidencePush> PushAsync(string checkout, string scratch, IReadOnlyList<EvidenceFile> files, CancellationToken ct)
    {
        Pushes.Enqueue((checkout, files));
        return Task.FromResult(new EvidencePush([.. files.Select(file => file.Path)], null));
    }

    public Task EndedAsync(PbiRef pbi, CancellationToken ct)
    {
        Ended.Enqueue(pbi);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An agent CLI that runs its command as a shell script, with what every provider gives a run: the
/// checkout's scripts first on the PATH, and <c>AIO_DONE_FILE</c>. It reports a fixed cost, so the
/// test can see the run store what its provider measured.
/// </summary>
sealed class StandInAgent : IAgentProvider
{
    public const string ProviderName = "stand-in";

    public static readonly AgentUsage Usage = new(4000, 200, 0.0115m);

    public string Name => ProviderName;

    /// <summary>The permission mode of every run this agent was launched for, in order (#578).</summary>
    public ConcurrentQueue<AgentPermissions> Launched { get; } = new();

    public SessionOptions Launch(AgentRequest request)
    {
        Launched.Enqueue(request.Permissions);
        return new(
            "/bin/sh",
            ["-c", request.Command],
            request.Place.WorkingDirectory,
            new Dictionary<string, string>
            {
                ["PATH"] = Path.Combine(request.Place.WorkingDirectory, ".aio") + Path.PathSeparator
                    + Environment.GetEnvironmentVariable("PATH"),
                ["AIO_DONE_FILE"] = request.EndRecordPath,
            },
            120,
            30,
            request.RecordingPath
        );
    }

    public async Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) =>
        new(File.Exists(request.EndRecordPath) ? await File.ReadAllTextAsync(request.EndRecordPath, ct) : null, Usage);

    public Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(new CliProbe("sh", "/bin/sh", null));

    /// <summary>#706. The two kinds of phrase a CLI waits on, looked for where Claude Code's adapter looks: at the end.</summary>
    public PersonWait? WaitsForPerson(string screen)
    {
        var recent = screen.Length > 200 ? screen[^200..] : screen;
        if (recent.Contains("Please run /login", StringComparison.Ordinal))
        {
            return new(PersonWaitKind.Login, "The stand-in is not logged in. Run its login on this machine, then release.");
        }

        return recent.Contains("Do you want to proceed?", StringComparison.Ordinal)
            ? new(PersonWaitKind.Permission, "The stand-in asked for a permission. Switch permissions to auto in Settings, then release.")
            : null;
    }
}

/// <summary>
/// An environment that is not a worktree (#612): its place is the checkout itself, and it enters it
/// the way <c>sbx exec</c> would, a process of its own around the launch, which here says one line
/// and then becomes the launch with the same arguments and variables. Its agents run in
/// <c>yolo</c>, as a sandbox's would.
/// </summary>
sealed class WrappingEnvironment : IEnvironment
{
    public const string EnvironmentName = "wrapping";

    public const string Announcement = "inside the wrapping environment";

    public string Name => EnvironmentName;

    public AgentPermissions? Permissions => AgentPermissions.Yolo;

    /// <summary>Every launch this environment was asked to enter, as the provider gave it.</summary>
    public ConcurrentQueue<SessionOptions> Entered { get; } = new();

    public ConcurrentQueue<RunPlace> Retired { get; } = new();

    public Task<RunPlace> PrepareAsync(PlaceRequest request, CancellationToken ct) =>
        Task.FromResult(new RunPlace(EnvironmentName, request.RepositoryPath));

    public SessionOptions Enter(RunPlace place, SessionOptions launch)
    {
        Entered.Enqueue(launch);
        return launch with
        {
            FileName = "/bin/sh",
            Arguments = ["-c", $"echo {Announcement}; exec \"$0\" \"$@\"", launch.FileName, .. launch.Arguments],
        };
    }

    public Task RetireAsync(RunPlace place, CancellationToken ct)
    {
        Retired.Enqueue(place);
        return Task.CompletedTask;
    }
}

/// <summary>
/// One folder and one PBI, as a vendor would answer for them. Only the reads a session on a PBI
/// makes answer; every write is noted and refused.
/// </summary>
sealed class StubVendor : VendorStub, IPbiProvider, IFolderDirectory
{
    public const string VendorName = "stub";

    LoopFolder? _folder;
    Pbi? _pbi;

    public ConcurrentQueue<string> Writes { get; } = new();

    public override string Name => VendorName;

    public void Arrange(LoopFolder folder, Pbi pbi) => (_folder, _pbi) = (folder, pbi);

    Task<IReadOnlyList<LoopFolder>> IFolderDirectory.ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoopFolder>>(_folder is null ? [] : [_folder]);

    public override Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Pbi>>(_pbi is null || query.State == PbiSearchState.Closed ? [] : [_pbi]);

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(
            _pbi?.Ref == pbi
                ? new PbiThread(_pbi, new Uri($"https://example.test/{pbi.Repository}/{pbi.Id}"), null, DateTimeOffset.UnixEpoch, "", [], [], null, [], [])
                : null
        );

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<PullRequestInfo?>(null);

    public override Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(new CliProbe("stub", null, null, "stub"));

    public override Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct) => Refuse<ReviewComment>($"review {pbi.Id}");

    public override Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct) => Refuse<Pbi>($"move {pbi.Id} {move}");

    public override Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct) =>
        Refuse<PullRequestInfo>($"open {draft.Branch}");

    public override Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<PullRequestReview?>(null);

    public override Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<PullRequestChange?>(null);

    public override Task<Written<ReviewThread>> ResolveReviewThreadAsync(PbiRef pbi, string threadId, CancellationToken ct) =>
        Refuse<ReviewThread>($"resolve {pbi.Id}");

    public override Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct) =>
        Refuse<SubmittedReview>($"review {pbi.Id} {verdict}");

    public override Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct) => Task.FromResult<byte[]?>(null);

    Task<Written<T>> Refuse<T>(string write)
        where T : class
    {
        Writes.Enqueue(write);
        throw new PbiProviderException($"The stub vendor does not write: {write}.");
    }
}
