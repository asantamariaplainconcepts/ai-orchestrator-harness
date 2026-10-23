using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>
/// <see cref="IEnvironment"/> on this machine: a git worktree of the folder's checkout, on the PBI's
/// branch. It is prepared by <c>.aio/steps/prepare</c> and retired by
/// <c>.aio/steps/cleanup</c>, the same files a person runs by hand, so a run starts from exactly
/// what they would. A repository that does not carry them gets the copies this build does
/// (<see cref="HarnessScript"/>), so a folder needs no harness for a session to open on a PBI.
/// <para>
/// Every refusal is the script's own sentence: uncommitted files, a conflict with the base, a branch
/// that diverged from what was pushed. Both scripts write to the checkout's <c>.git</c>, so they run
/// one at a time per repository (<see cref="RepositoryLock"/>), and the one retry is for a config
/// lock file that another process held.
/// </para>
/// </summary>
sealed class WorktreeEnvironment : IEnvironment
{
    public const string WorktreeName = "worktree";

    public string Name => WorktreeName;

    /// <summary>The instance's setting: this machine is the person's, so its mode is theirs to choose.</summary>
    public AgentPermissions? Permissions => null;

    public async Task<RunPlace> PrepareAsync(PlaceRequest request, CancellationToken ct)
    {
        var root = LocalPath.Expand(request.RepositoryPath);

        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException($"{request.RepositoryPath} is no longer on disk.");
        }

        var run = await Locked(root, "prepare", [request.Branch, request.BaseRef.Trim()], ct);
        var path = Answered(run, "prepare", "path", "no worktree was prepared");

        return new RunPlace(WorktreeName, path);
    }

    /// <summary>The launch as the provider gave it: the session starts in the worktree, on this machine.</summary>
    public SessionOptions Enter(RunPlace place, SessionOptions launch) => launch;

    /// <summary>
    /// Removes the worktree and keeps the branch. Without <c>--force</c>, so uncommitted work refuses
    /// rather than goes; what was committed stays on the branch.
    /// </summary>
    public async Task RetireAsync(RunPlace place, CancellationToken ct)
    {
        var target = place.WorkingDirectory;

        // From the worktree itself, the one path sure to belong to its repository, or from the folder
        // that held it when it is already gone.
        var from = Directory.Exists(target) ? target : Path.GetDirectoryName(target) ?? target;
        var run = await Locked(from, "cleanup", [target, "--worktree-only"], ct);

        if (run is null)
        {
            throw new InvalidOperationException(
                "This build carries no copy of .aio/steps/cleanup, so the worktree was not removed."
            );
        }

        if (HarnessScript.Answer(run.StandardOutput) is not { } answer || !HarnessScript.Flag(answer, "removed"))
        {
            throw new InvalidOperationException(Why(run, "cleanup"));
        }
    }

    /// <summary>
    /// The script with its repository to itself, run once more after a short pause when git found the
    /// config locked by a writer outside this process.
    /// </summary>
    static async Task<ProcessOutcome?> Locked(string directory, string script, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        using var held = await RepositoryLock.Acquire(directory, ct);
        var run = await HarnessScript.Run(directory, script, arguments, ct);

        if (run is not null && RepositoryLock.Contended(run))
        {
            await Task.Delay(RepositoryLock.RetryPause, ct);
            run = await HarnessScript.Run(directory, script, arguments, ct);
        }

        return run;
    }

    static string Answered(ProcessOutcome? run, string script, string property, string consequence)
    {
        if (run is null)
        {
            throw new InvalidOperationException(
                $"This build carries no copy of .aio/steps/{script}, so {consequence}."
            );
        }

        if (run.TimedOut)
        {
            throw new InvalidOperationException($".aio/steps/{script} did not finish in time, so {consequence}.");
        }

        return HarnessScript.Answer(run.StandardOutput) is { } answer
            && HarnessScript.Text(answer, property) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(Why(run, script));
    }

    static string Why(ProcessOutcome run, string script) =>
        HarnessScript.Answer(run.StandardOutput) is { } answer && HarnessScript.Text(answer, "why") is { Length: > 0 } why
            ? why
            : HarnessScript.Said(run, script);
}
