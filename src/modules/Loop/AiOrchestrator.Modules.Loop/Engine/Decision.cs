using AiOrchestrator.Modules.Loop.Definition;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>What the engine does with one PBI on one pass. <see cref="Planner.Decide"/> answers one.</summary>
abstract record Decision;

/// <summary>Not the loop's: assigned to someone else, or at a state the loop does nothing in. No line in the journal.</summary>
sealed record Ignore(string Why) : Decision;

/// <summary>Nothing this pass, and why. Written when it differs from the PBI's last line.</summary>
sealed record Wait(string Why) : Decision;

/// <summary>Run these scripts of the harness in order, then decide again with what they answered.</summary>
sealed record RunScripts(IReadOnlyList<string> Scripts) : Decision;

/// <summary>Ask Runs for the state's agent.</summary>
/// <param name="Provider">The CLI, as <c>IAgentProvider.Name</c> spells it.</param>
/// <param name="Model">The model, or null for the CLI's default.</param>
/// <param name="Command">What the agent is asked, such as <c>/aio:implement 505</c>.</param>
/// <param name="Outcomes">The outcomes the state's <c>on</c> knows, or <c>next</c> alone.</param>
sealed record StartRun(string Provider, string? Model, string Command, IReadOnlyList<string> Outcomes) : Decision;

/// <summary>Move the PBI to <paramref name="To"/>, after the vendor step the transition carries.</summary>
sealed record Move(string To, string Why, VendorStep Step) : Decision;

/// <summary>Put the hold on with its reason. <paramref name="Next"/> is where releasing it leads, or null when the person chooses.</summary>
sealed record Hold(string Why, string? Next) : Decision;

/// <summary>Take off a hold a person approved at the vendor, and move to where it led.</summary>
sealed record Release(string To, string Why) : Decision;

/// <summary>
/// Write a <c>ran</c> line with how the run ended and change nothing else: a run a person stopped
/// and then released (#707) is not held again.
/// </summary>
sealed record Note(string Why) : Decision;
