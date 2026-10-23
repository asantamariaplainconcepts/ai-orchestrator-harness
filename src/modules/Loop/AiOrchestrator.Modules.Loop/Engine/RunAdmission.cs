using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// Whether a run can start here now: the environment Runs would place it in can take it (#614),
/// and outside a sandbox the CLI trusts the folder (#606). It also counts the live runs the WIP
/// limit is held to: read once a pass, and one more for each run it starts.
/// </summary>
sealed class RunAdmission(IRunRequests runs, IEnumerable<IAgentProvider>? agents = null)
{
    /// <summary>Runs not ended yet, the loop's and manual ones alike; <see cref="Planner"/> holds them to the WIP.</summary>
    public int Live { get; private set; }

    public async Task CountLiveAsync(CancellationToken ct) => Live = (await runs.ListLiveAsync(ct)).Count;

    /// <summary>Why the run waits instead of starting, in the words the PBI's line carries, or null when it can start.</summary>
    public async Task<string?> WaitsAsync(RunRequest request, CancellationToken ct)
    {
        // #614. An environment that cannot take the run now, such as a sandbox with no sign-in,
        // makes the PBI wait with what the person does instead of failing it.
        var placement = await runs.PlacementAsync(request, ct);
        if (placement?.Waits is { } unplaceable)
        {
            return unplaceable;
        }

        // #606. A CLI that would open on a trust question nobody answers is not started: the PBI
        // waits with what the person does, as file-overlap waits for a branch. In a sandbox the CLI
        // keeps its own configuration, so this machine's trust is not asked.
        var trust = placement is { Sandboxed: true } ? null : agents?.FirstOrDefault(agent => agent.Name == request.Provider)?.Trust(request.FolderPath);
        return trust is { Trusted: false } ? trust.Why : null;
    }

    /// <summary>Asks Runs for the run and counts it as live.</summary>
    public async Task<Guid> StartAsync(RunRequest request, CancellationToken ct)
    {
        var id = await runs.StartAsync(request, ct);
        Live++;
        return id;
    }
}
