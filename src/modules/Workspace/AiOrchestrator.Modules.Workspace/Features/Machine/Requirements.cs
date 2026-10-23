using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Workspace.Features.Machine;

/// <summary>
/// What this machine needs for the loop, row by row, from probes already taken (#680): <c>git</c>,
/// each vendor's CLI signed in, <c>node</c> for the harness's scripts and one agent CLI signed in are
/// required; the sandbox environment and a container runtime for Aspire are optional. A row that is
/// not there carries how to put it right on this machine, from <see cref="IMachineProbe"/>.
/// </summary>
static class Requirements
{
    public const string Present = "present";
    public const string Missing = "missing";
    public const string SignedOut = "signed-out";

    public const string Git = "git";

    /// <summary>What the harness's scripts run on.</summary>
    public const string Node = "node";

    /// <summary>The CLI behind the sandbox environment, which is also that environment's name.</summary>
    public const string Sandbox = "sbx";

    /// <param name="sandbox">How the sandbox environment answers, or null when no adapter is registered for one.</param>
    public static IReadOnlyList<ReadTools.Requirement> Rows(
        CliProbe git,
        IReadOnlyList<CliProbe> vendors,
        CliProbe node,
        IReadOnlyList<CliProbe> agents,
        (CliProbe Cli, EnvironmentReadiness Readiness)? sandbox,
        CliProbe containers,
        IMachineProbe machine
    )
    {
        List<ReadTools.Requirement> rows =
        [
            Row(git, required: true, agent: false, machine),
            .. vendors.Select(vendor => Row(vendor, required: true, agent: false, machine)),
            Row(node, required: true, agent: false, machine),
            .. agents.Select(agent => Row(agent, required: true, agent: true, machine)),
        ];

        if (sandbox is var (cli, readiness))
        {
            // sbx's own answer says what is missing beyond the CLI: Docker's sign-in, or the Claude
            // credential a /login inside a sandbox leaves.
            rows.Add(
                cli.Path is null || readiness.Offered
                    ? Row(cli, required: false, agent: false, machine)
                    : Of(cli, SignedOut, required: false, agent: false) with { Problem = readiness.Note, Command = readiness.Remedy }
            );
        }

        // A runtime that is installed and does not answer is as good as none for Aspire.
        rows.Add(
            containers is { Path: not null, Problem: not null }
                ? Of(containers, Missing, required: false, agent: false) with
                {
                    Command = machine.MakeReady(containers.Cli),
                    Link = machine.Install(containers.Cli).Link,
                }
                : Row(containers, required: false, agent: false, machine)
        );

        return rows;
    }

    /// <summary>Every required row is present, and so is one agent CLI when any is listed.</summary>
    public static bool Met(IReadOnlyList<ReadTools.Requirement> rows) =>
        rows.Where(row => row is { Required: true, Agent: false }).All(row => row.State == Present)
        && (rows.All(row => !row.Agent) || rows.Any(row => row is { Agent: true, State: Present }));

    static ReadTools.Requirement Row(CliProbe probe, bool required, bool agent, IMachineProbe machine)
    {
        if (probe.Path is null)
        {
            var install = machine.Install(probe.Cli);
            return Of(probe, Missing, required, agent) with { Command = install.Command, Link = install.Link };
        }

        return probe.Problem is null
            ? Of(probe, Present, required, agent)
            : Of(probe, SignedOut, required, agent) with { Command = machine.MakeReady(probe.Cli) };
    }

    static ReadTools.Requirement Of(CliProbe probe, string state, bool required, bool agent) =>
        new(probe.Cli, state, required, agent, probe.Version, probe.Account, probe.Problem, null, null);
}
