using Microsoft.Data.Sqlite;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>The <c>Runs</c> section of the configuration.</summary>
sealed class RunsOptions
{
    public const string Section = "Runs";

    /// <summary>
    /// How long a live run may write nothing before it reads as stale (#485). Ten minutes: an agent
    /// thinking hard still repaints its spinner, so a silent terminal for that long is one that
    /// stopped, or one waiting at a prompt nobody will answer, so a loop run silent that long without
    /// its end record is ended (#764). The default only: a threshold set in Settings wins
    /// (<see cref="StaleThreshold"/>, #562).
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long a run's agent has, after its end record appears, to exit by itself before its
    /// session is killed. Interactive <c>claude</c> stays open after <c>aio done</c>, so without this
    /// a finished run would never end.
    /// </summary>
    public TimeSpan EndGrace { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a loop run's session may stay silent on a screen its CLI's adapter reads as waiting for
    /// a person, a permission or a login, before the run ends as a failure that holds its PBI (#706).
    /// Thirty seconds: long enough that a prompt the agent passes through is not taken for one it
    /// waits at, short enough that the PBI reaches Needs you within the minute.
    /// </summary>
    public TimeSpan PersonWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The environment a run is placed in when its folder chose none for its state
    /// (<see cref="StateEnvironments"/>, #612), and every script of the loop: the gate needs this
    /// machine's SDK.
    /// </summary>
    public string Environment { get; set; } = "worktree";

    /// <summary>
    /// Where each run keeps its end record and recording, one directory per run. Unset, it is a
    /// <c>runs</c> directory beside the SQLite file, so a worktree's AppHost keeps its runs apart
    /// the way it keeps its database apart (ADR-0003).
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>The directory runs are written to, given the module's connection string.</summary>
    public string RunDirectory(string? connectionString)
    {
        if (Directory is { Length: > 0 } configured)
        {
            return configured;
        }

        var database = connectionString is null ? null : new SqliteConnectionStringBuilder(connectionString).DataSource;

        return database is { Length: > 0 } && Path.GetDirectoryName(Path.GetFullPath(database)) is { } beside
            ? Path.Combine(beside, "runs")
            : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "AiOrchestrator", "runs");
    }
}
