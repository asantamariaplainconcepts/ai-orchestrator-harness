using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// One execution of a step on a PBI (README.md#the-concepts): who ran it, when, what it
/// cost, what it ended with, and where its recording is. The loop's runs and the ones a person opens
/// by hand on a PBI are the same row; <see cref="Manual"/> tells them apart, and a manual run never
/// reaches the loop.
/// <para>
/// It carries only what the Journey shows, and no transcript or diff: the transcript is the
/// recording and the diff is the pull request's.
/// </para>
/// </summary>
sealed class PbiRun : Aggregate
{
    PbiRun() { }

    PbiRun(
        PbiRef pbi,
        string state,
        string folderPath,
        string branch,
        string? provider,
        string? model,
        string command,
        bool manual,
        IReadOnlyList<string> outcomes,
        string directory,
        DateTimeOffset startedAt
    )
    {
        Repository = pbi.Repository;
        PbiId = pbi.Id;
        State = state;
        FolderPath = folderPath;
        Branch = branch;
        Provider = provider;
        Model = model;
        Command = command;
        Manual = manual;
        OutcomesJson = JsonSerializer.Serialize(outcomes);
        EndRecordPath = Path.Combine(directory, Id.ToString("N"), "done.json");
        RecordingPath = Path.Combine(directory, Id.ToString("N"), "session.cast");
        StartedAt = startedAt;
    }

    /// <summary>The PBI's repository or project, as the vendor names it.</summary>
    public string Repository { get; private set; } = string.Empty;

    /// <summary>The vendor's id for the PBI.</summary>
    public string PbiId { get; private set; } = string.Empty;

    /// <summary>The <c>loop.yaml</c> state the run belongs to; for a manual run, the one the PBI was in.</summary>
    public string State { get; private set; } = string.Empty;

    public string FolderPath { get; private set; } = string.Empty;

    public string Branch { get; private set; } = string.Empty;

    /// <summary>The agent CLI, or null for a terminal a person opened, which runs their shell.</summary>
    public string? Provider { get; private set; }

    public string? Model { get; private set; }

    /// <summary>What the agent was asked; empty for a terminal, or an agent opened with no command.</summary>
    public string Command { get; private set; } = string.Empty;

    /// <summary>A person opened it (Terminal, Explore with agent). It never produces a transition.</summary>
    public bool Manual { get; private set; }

    /// <summary>The outcomes the state's <c>on</c> knew when the run started, as JSON.</summary>
    public string OutcomesJson { get; private set; } = "[]";

    /// <summary>Where <c>aio done</c> writes, outside the place, so retiring it keeps the record.</summary>
    public string EndRecordPath { get; private set; } = string.Empty;

    /// <summary>The asciicast v2 recording of the run's terminal.</summary>
    public string RecordingPath { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>The environment that prepared the place, such as <c>local</c>, once it has.</summary>
    public string? Environment { get; private set; }

    /// <summary>Where the run's session started, once the environment prepared it.</summary>
    public string? WorkingDirectory { get; private set; }

    /// <summary>When the run ended, or null while it is live.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>The outcome its end record names. Null while live, on a failure and on a manual run.</summary>
    public string? Outcome { get; private set; }

    public string? Reason { get; private set; }

    public RunFailure? Failure { get; private set; }

    public string ProblemsJson { get; private set; } = "[]";

    /// <summary>The end record exactly as <c>aio done</c> wrote it, or null when there was none.</summary>
    public string? EndRecord { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public decimal? CostUsd { get; private set; }

    public bool Ended => EndedAt is not null;

    public PbiRef Pbi => new(Repository, PbiId);

    public IReadOnlyList<string> Outcomes => JsonSerializer.Deserialize<string[]>(OutcomesJson) ?? [];

    public IReadOnlyList<string> Problems => JsonSerializer.Deserialize<string[]>(ProblemsJson) ?? [];

    /// <summary>
    /// A run the loop asked for. Its record and recording go under
    /// <paramref name="directory"/>/<c>&lt;run id&gt;</c>.
    /// </summary>
    public static PbiRun Loop(RunRequest request, string directory, DateTimeOffset startedAt) =>
        new(
            request.Pbi,
            request.State,
            request.FolderPath,
            request.Branch,
            request.Provider,
            request.Model,
            request.Command,
            manual: false,
            request.Outcomes,
            directory,
            startedAt
        );

    /// <summary>A run a person opened on a PBI: a terminal when <paramref name="provider"/> is null.</summary>
    public static PbiRun Opened(
        PbiRef pbi,
        string state,
        string folderPath,
        string branch,
        string? provider,
        string? model,
        string? command,
        string directory,
        DateTimeOffset startedAt
    ) => new(pbi, state, folderPath, branch, provider, model, command ?? string.Empty, manual: true, [], directory, startedAt);

    public void Placed(RunPlace place)
    {
        Environment = place.Environment;
        WorkingDirectory = place.WorkingDirectory;
    }

    /// <summary>
    /// Writes down how the run ended. The first ending stands: a run that ended is not ended again.
    /// </summary>
    /// <param name="end">The loop's reading of the end record; null for a manual run, which has none.</param>
    /// <param name="record">The end record as written, or null.</param>
    /// <param name="usage">What it cost, or null when the CLI does not say.</param>
    /// <param name="at">When it ended.</param>
    public void End(RunEnd? end, string? record, AgentUsage? usage, DateTimeOffset at)
    {
        if (Ended)
        {
            return;
        }

        EndedAt = at;
        Outcome = end?.Outcome;
        Reason = end?.Reason;
        Failure = end?.Failure;
        ProblemsJson = JsonSerializer.Serialize(end?.Problems ?? []);
        EndRecord = record;
        InputTokens = usage?.InputTokens;
        OutputTokens = usage?.OutputTokens;
        CostUsd = usage?.CostUsd;
    }
}
