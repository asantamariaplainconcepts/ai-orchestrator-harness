using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Journal;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// Everything the engine knows about one PBI when it decides: the photograph a pass takes before it
/// changes anything. <see cref="Planner.Decide"/> reads only this.
/// </summary>
/// <param name="Loop">The folder's <c>.aio/loop.yaml</c>.</param>
/// <param name="Pbi">The PBI as the vendor has it now.</param>
/// <param name="Identity">The account this control plane acts as at the vendor.</param>
/// <param name="LiveRuns">Runs not ended yet, which is what the WIP limit counts.</param>
/// <param name="Run">The run the engine asked for in the PBI's current state, or null when it asked for none.</param>
/// <param name="Scripts">What the state's scripts and score answered this pass, in order; null until they ran.</param>
/// <param name="Returns">How many times each state has sent this PBI back to a state above it.</param>
/// <param name="Last">The engine's latest move, hold or release of this PBI, or null.</param>
/// <param name="Approved">Whether the PBI's pull request is approved at the vendor. Read only while
/// a hold that leads somewhere is on, because an approval is what releases it.</param>
/// <param name="FolderAgent">The folder's default agent and model, which a state that names no agent
/// runs with, or null when the folder has none.</param>
sealed record Situation(
    LoopDefinition Loop,
    Pbi Pbi,
    string Identity,
    int LiveRuns,
    AskedRunStatus? Run,
    IReadOnlyList<ScriptVerdict>? Scripts,
    IReadOnlyDictionary<string, int> Returns,
    LastTransition? Last,
    bool Approved,
    LoopAgent? FolderAgent = null
);

/// <summary>A run the engine asked for, as Runs answers for it now.</summary>
/// <param name="Found">False when Runs has no run with that id.</param>
/// <param name="End">How it ended, or null while it runs.</param>
/// <param name="AskedAt">When the engine asked for it, or null when not known.</param>
sealed record AskedRunStatus(Guid RunId, bool Found, RunEnd? End, DateTimeOffset? AskedAt = null);

/// <summary>A move, hold or release the journal holds for the PBI.</summary>
/// <param name="At">When the journal wrote it, or null when not known.</param>
sealed record LastTransition(JournalKind Kind, string From, string? To, string Why, DateTimeOffset? At = null);
