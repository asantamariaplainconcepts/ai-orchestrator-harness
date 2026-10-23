using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// What the requirements screen (#680) asks of this machine beyond the CLIs the adapters drive: the
/// CLIs no adapter owns, such as <c>git</c> and <c>node</c>, a container runtime, and how each one is
/// installed or made ready here. The commands are the adapter's because they name vendors and
/// package managers, which a module does not.
/// </summary>
public interface IMachineProbe
{
    /// <summary>A CLI on the login shell's PATH, asked <c>--version</c>; its path is null when it is not there.</summary>
    Task<CliProbe> ProbeAsync(string cli, CancellationToken ct);

    /// <summary>
    /// Podman, else Docker, whichever answers <c>info</c>. One that is installed and does not answer
    /// carries why in <see cref="CliProbe.Problem"/>; with neither installed the path is null.
    /// </summary>
    Task<CliProbe> ContainerRuntimeAsync(CancellationToken ct);

    /// <summary>How <paramref name="cli"/> is installed on this machine: a command for its package manager, else a page.</summary>
    MachineRemedy Install(string cli);

    /// <summary>The command that makes an installed <paramref name="cli"/> ready, signing it in or starting it, or null when it has none.</summary>
    string? MakeReady(string cli);
}

/// <param name="Command">What to paste into a terminal, or null when this machine's package manager cannot be told.</param>
/// <param name="Link">Where the CLI's own install instructions are, or null for a CLI the probe does not know.</param>
public sealed record MachineRemedy(string? Command, string? Link);
