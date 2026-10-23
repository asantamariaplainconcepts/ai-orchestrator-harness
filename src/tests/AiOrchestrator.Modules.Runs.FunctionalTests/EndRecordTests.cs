using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Runs.Features.PbiRuns;
using Json.Schema;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The end record of a run (#499): <c>.aio/aio done</c> writes it, its schema in
/// <c>.aio/schemas/done.v1.json</c> says what it may hold, and <see cref="EndRecords"/> reads it
/// as untrusted input. The script runs for real, with <c>node</c>, against this checkout's own file,
/// so a test against a copy cannot pass while the file says something else.
/// </summary>
public sealed class EndRecordTests : IDisposable
{
    static readonly string[] Outcomes = ["approved", "rejected"];

    readonly string _directory = Directory.CreateTempSubdirectory("aio-done-").FullName;

    readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow.AddSeconds(-1);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_reason_and_every_problem_reach_the_run_end()
    {
        Aio("done", "rejected", "--reason", "Two tests are missing", "--problems", "No test for A", "--problems", "No test for B")
            .Code.ShouldBe(0);

        var end = EndRecords.Read(_directory, Outcomes, _startedAt);

        end.Outcome.ShouldBe("rejected");
        end.Reason.ShouldBe("Two tests are missing");
        end.Failure.ShouldBeNull();
        end.Problems.ShouldBe(["No test for A", "No test for B"]);
    }

    [Fact]
    public void Blocked_fails_the_run_with_the_agents_reason()
    {
        Aio("done", "blocked", "--reason", "The issue names a file that does not exist").Code.ShouldBe(0);

        var end = EndRecords.Read(_directory, Outcomes, _startedAt);

        end.Failure.ShouldBe(RunFailure.Blocked);
        end.Outcome.ShouldBeNull();
        end.Reason.ShouldBe("The issue names a file that does not exist");
    }

    [Fact]
    public void A_missing_record_fails_the_run_as_no_end_record()
    {
        var end = EndRecords.Read(_directory, Outcomes, _startedAt);

        end.Failure.ShouldBe(RunFailure.NoEndRecord);
        end.Reason!.ShouldContain("aio done");
    }

    [Fact]
    public void A_record_from_an_earlier_run_is_no_end_record()
    {
        Aio("done", "approved").Code.ShouldBe(0);

        var end = EndRecords.Read(_directory, Outcomes, DateTimeOffset.UtcNow.AddMinutes(1));

        end.Failure.ShouldBe(RunFailure.NoEndRecord);
        end.Reason!.ShouldContain("before this run started");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"version": 1, "outcome": "approved"}""")]
    [InlineData("""{"version": 2, "outcome": "approved", "at": "2099-01-01T00:00:00Z"}""")]
    [InlineData("""{"version": 1, "outcome": "Approved", "at": "2099-01-01T00:00:00Z"}""")]
    [InlineData("""{"version": 1, "outcome": "approved", "at": "yesterday"}""")]
    [InlineData("""{"version": 1, "outcome": "approved", "at": "2099-01-01T00:00:00Z", "cost": 3}""")]
    [InlineData("""{"version": 1, "outcome": "blocked", "at": "2099-01-01T00:00:00Z"}""")]
    public void A_malformed_record_fails_the_run_as_invalid(string written)
    {
        Write(written);

        var end = EndRecords.Read(_directory, Outcomes, _startedAt);

        end.Failure.ShouldBe(RunFailure.InvalidEndRecord, end.Reason);
        end.Reason!.ShouldContain("done.json");
    }

    [Fact]
    public void An_outcome_the_state_does_not_know_fails_the_run_and_names_it()
    {
        Aio("done", "merged").Code.ShouldBe(0);

        var end = EndRecords.Read(_directory, Outcomes, _startedAt);

        end.Failure.ShouldBe(RunFailure.UnknownOutcome);
        end.Outcome.ShouldBeNull();
        end.Reason.ShouldBe("The end record names 'merged', and the state knows approved, rejected.");
    }

    /// <summary>
    /// #590. An implement ran the gate inside its own session, so this process had the run's
    /// <c>AIO_DONE_FILE</c>, and the <c>aio done merged</c> above wrote into that run's end record
    /// and ended it. With the variable set on this process, every test here still writes only its
    /// own directory.
    /// </summary>
    [Fact]
    public void A_run_end_record_in_this_process_environment_is_left_alone()
    {
        var record = Path.Combine(_directory, "the-run-done.json");
        File.WriteAllText(record, "the run's record\n");
        var before = Environment.GetEnvironmentVariable("AIO_DONE_FILE");
        Environment.SetEnvironmentVariable("AIO_DONE_FILE", record);
        try
        {
            Aio("done", "merged").Code.ShouldBe(0);
            Aio("done", "approved", "--reason", "Fine").Code.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIO_DONE_FILE", before);
        }

        File.ReadAllText(record).ShouldBe("the run's record\n");
        EndRecords.Read(_directory, Outcomes, _startedAt).Outcome.ShouldBe("approved");
    }

    /// <summary>
    /// The drift test. Every field the schema declares is one the command can write, and every
    /// record the command writes validates: a property added to either side alone fails here.
    /// </summary>
    [Fact]
    public void The_command_writes_exactly_the_fields_the_schema_declares()
    {
        Aio("done", "approved", "--reason", "Fine", "--problems", "One").Code.ShouldBe(0);
        var written = File.ReadAllText(RecordFile);

        Validate(written).IsValid.ShouldBeTrue();

        var declared = SchemaDocument()["properties"]!.AsObject().Select(property => property.Key).Order();
        var produced = JsonNode.Parse(written)!.AsObject().Select(property => property.Key).Order();
        produced.ShouldBe(declared);
    }

    /// <summary>
    /// The other direction: what the command refuses, the schema refuses too. Each case is the
    /// record the command would have written had it not refused, and the schema must reject it.
    /// </summary>
    [Theory]
    [InlineData("Approved", null)]
    [InlineData("9lives", null)]
    [InlineData("blocked", null)]
    public void What_the_command_refuses_the_schema_refuses(string outcome, string? reason)
    {
        string[] arguments = reason is null ? ["done", outcome] : ["done", outcome, "--reason", reason];
        var (code, said) = Aio(arguments);

        code.ShouldBe(2, said);
        File.Exists(RecordFile).ShouldBeFalse();

        var record = new JsonObject { ["version"] = 1, ["outcome"] = outcome, ["at"] = "2099-01-01T00:00:00.000Z" };
        if (reason is not null)
        {
            record["reason"] = reason;
        }
        Validate(record.ToJsonString()).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("finish", "approved")]
    [InlineData("done")]
    [InlineData("done", "approved", "rejected")]
    [InlineData("done", "approved", "--reason")]
    [InlineData("done", "approved", "--cost", "3")]
    public void Bad_arguments_write_nothing(params string[] arguments)
    {
        var (code, said) = Aio(arguments);

        code.ShouldBe(2, said);
        JsonNode.Parse(said)!["written"].ShouldBeNull();
        File.Exists(RecordFile).ShouldBeFalse();
    }

    string RecordFile => Path.Combine(_directory, EndRecords.RelativePath);

    void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RecordFile)!);
        File.WriteAllText(RecordFile, text);
    }

    static string Root() =>
        AiOrchestrator.BuildingBlocks.Folders.SelfCheckout.Root()
        ?? throw new DirectoryNotFoundException("These tests read this checkout's own .aio, and there is none.");

    static JsonNode SchemaDocument() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), ".aio", "schemas", "done.v1.json")))!;

    static EvaluationResults Validate(string record) =>
        JsonSchema
            .FromText(SchemaDocument().ToJsonString(), new BuildOptions { SchemaRegistry = new() })
            .Evaluate(JsonDocument.Parse(record).RootElement, new EvaluationOptions { RequireFormatValidation = true });

    /// <summary>
    /// Runs the script with <c>node</c> in the temporary directory, which is outside any git
    /// repository, so the record lands in that directory's own <c>.aio</c>. <c>AIO_DONE_FILE</c>
    /// is removed rather than inherited: this process may run inside an agent's session, and the
    /// variable is that run's own end record.
    /// </summary>
    (int Code, string Said) Aio(params string[] arguments)
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Remove("AIO_DONE_FILE");
        start.ArgumentList.Add(Path.Combine(Root(), ".aio", "aio"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var said = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, said.Trim());
    }
}
