namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// The CLI an adapter drives, as this machine has it right now: what Settings shows under "On this
/// machine" (DESIGN.md#the-screens). Asked of the adapter, because which executable it spawns
/// and how it finds it are the adapter's.
/// </summary>
/// <param name="Cli">The executable's name, such as <c>gh</c> or <c>claude</c>.</param>
/// <param name="Path">Where it is, or null when it is not on the PATH the adapter spawns with.</param>
/// <param name="Version">What <c>--version</c> printed, or null when it printed nothing a person could read.</param>
/// <param name="Account">The account the CLI acts as at its vendor, or null when it names none or there is no vendor to act at.</param>
/// <param name="Problem">Why a CLI that is there named no account, in its own words, or null.</param>
public sealed record CliProbe(string Cli, string? Path, string? Version, string? Account = null, string? Problem = null);
