using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Machine;

/// <summary>
/// "On this machine" in Settings (#515, DESIGN.md#the-screens): the CLI each vendor
/// adapter reaches its vendor through, with the account it acts as, and each agent CLI, with where
/// it is and which version, or that it is not on the PATH. Asked of the adapters on every request,
/// because a CLI installed or signed into a minute ago is the answer a person checking expects.
/// <para>
/// One row per registered adapter. A CLI with no adapter yet is not listed: the loop could not run
/// it whether it is installed or not.
/// </para>
/// <para>
/// The same probes are the requirements screen's (#680), with what no adapter drives beside them:
/// <c>git</c>, <c>node</c>, the sandbox's CLI and a container runtime, each with how to
/// install it here.
/// </para>
/// </summary>
sealed class ReadTools : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/machine/tools",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadTools))
            .WithTags("Workspace");

    /// <param name="Requirements">What the machine needs for the loop, the required rows first.</param>
    /// <param name="Ready">Every required row is present, one agent CLI being enough.</param>
    internal sealed record Tools(IReadOnlyList<Tool> Vendors, IReadOnlyList<Tool> Agents, IReadOnlyList<Requirement> Requirements, bool Ready);

    /// <param name="Adapter">The adapter's name: a vendor such as <c>github</c>, or a provider such as <c>claude-code</c>.</param>
    /// <param name="Cli">The executable it drives, such as <c>gh</c>.</param>
    /// <param name="Path">Where it is, or null when it is not on the PATH.</param>
    /// <param name="Version">Its version, or null when it did not say.</param>
    /// <param name="Account">The account a vendor CLI acts as, or null.</param>
    /// <param name="Problem">Why a CLI that is there named no account or is signed out, in its own words.</param>
    /// <param name="Models">The models an agent CLI can be opened with (#516), most capable first; empty for a vendor.</param>
    internal sealed record Tool(string Adapter, string Cli, string? Path, string? Version, string? Account, string? Problem, IReadOnlyList<string> Models);

    /// <param name="Name">The CLI, such as <c>node</c> or <c>claude</c>.</param>
    /// <param name="State"><c>present</c>, <c>missing</c> or <c>signed-out</c>.</param>
    /// <param name="Required">Whether the loop needs it; an optional one widens what it can do.</param>
    /// <param name="Agent">An agent CLI: one of them present is what is required.</param>
    /// <param name="Version">Its version, or null when it is missing or did not say.</param>
    /// <param name="Account">The account it acts as, or null.</param>
    /// <param name="Problem">Why it is signed out or does not answer, in its own words, or null.</param>
    /// <param name="Command">What to run to put it right on this machine, or null when there is no command to give.</param>
    /// <param name="Link">Where its install instructions are, when it is missing.</param>
    internal sealed record Requirement(
        string Name,
        string State,
        bool Required,
        bool Agent,
        string? Version,
        string? Account,
        string? Problem,
        string? Command,
        string? Link
    );

    // Nothing to scope to: the machine is not a folder's, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Tools>;

    internal sealed class Handler(
        IEnumerable<IPbiProvider> vendors,
        IEnumerable<IAgentProvider> agents,
        IEnumerable<IEnvironment> environments,
        IMachineProbe machine
    ) : IAppQueryHandler<Query, Tools>
    {
        public async Task<Tools> Handle(Query query, CancellationToken cancellationToken)
        {
            // Every probe at once: each is a process or two, and the slowest sets the answer's time.
            var vendorRows = Task.WhenAll(
                vendors
                    .OrderBy(vendor => vendor.Name, StringComparer.Ordinal)
                    .Select(async vendor => (vendor.Name, Probe: await vendor.ProbeAsync(cancellationToken)))
            );
            var agentRows = Task.WhenAll(
                agents
                    .DistinctBy(agent => agent.Name, StringComparer.Ordinal)
                    .OrderBy(agent => agent.Name, StringComparer.Ordinal)
                    .Select(async agent => (agent.Name, Probe: await agent.ProbeAsync(cancellationToken), agent.Models))
            );
            var git = machine.ProbeAsync(Requirements.Git, cancellationToken);
            var node = machine.ProbeAsync(Requirements.Node, cancellationToken);
            var containers = machine.ContainerRuntimeAsync(cancellationToken);
            var sandbox = Sandbox(cancellationToken);

            var probedVendors = await vendorRows;
            var probedAgents = await agentRows;
            var requirements = Requirements.Rows(
                await git,
                [.. probedVendors.Select(vendor => vendor.Probe)],
                await node,
                [.. probedAgents.Select(agent => agent.Probe)],
                await sandbox,
                await containers,
                machine
            );

            return new Tools(
                [.. probedVendors.Select(vendor => Row(vendor.Name, vendor.Probe))],
                [.. probedAgents.Select(agent => Row(agent.Name, agent.Probe) with { Models = agent.Models })],
                requirements,
                Requirements.Met(requirements)
            );
        }

        /// <summary>The sandbox's CLI and how its environment answers, or null when no adapter is registered for one.</summary>
        async Task<(CliProbe, EnvironmentReadiness)?> Sandbox(CancellationToken cancellationToken)
        {
            if (environments.FirstOrDefault(environment => environment.Name == Requirements.Sandbox) is not { } sandbox)
            {
                return null;
            }

            var cli = machine.ProbeAsync(Requirements.Sandbox, cancellationToken);
            var readiness = await sandbox.ReadinessAsync(cancellationToken);
            return (await cli, readiness);
        }

        static Tool Row(string adapter, CliProbe probe) =>
            new(adapter, probe.Cli, probe.Path, probe.Version, probe.Account, probe.Problem, []);
    }
}
