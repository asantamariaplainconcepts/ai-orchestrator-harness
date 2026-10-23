using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// What this control plane can run a step with: the agent CLIs it has a provider for and the
/// environments it can place a run in, by the names <c>loop.yaml</c> and the folders use. Whether
/// each CLI answers on this machine right now is Settings' to ask (#515). An environment that cannot
/// take a run now, such as sbx signed out, is left out of the list and says why in its note (#614).
/// </summary>
sealed class ReadMachine : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/machine",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadMachine))
            .WithTags("Runs");

    /// <param name="Agents">Each provider's name, such as <c>claude-code</c>.</param>
    /// <param name="Environments">Each environment offered now, such as <c>worktree</c>.</param>
    /// <param name="DefaultEnvironment">The one every run is placed in until a folder chooses its own.</param>
    /// <param name="Notes">What a person should read about an environment: why it is not offered, or a caution.</param>
    internal sealed record Machine(IReadOnlyList<string> Agents, IReadOnlyList<string> Environments, string DefaultEnvironment, IReadOnlyList<EnvironmentNote> Notes);

    /// <param name="Environment">The environment, such as <c>sbx</c>.</param>
    /// <param name="Offered">Whether it is in <see cref="Machine.Environments"/>.</param>
    /// <param name="Note">The sentence to show beside its selector.</param>
    /// <param name="Remedy">The command that fixes or improves it, or null.</param>
    internal sealed record EnvironmentNote(string Environment, bool Offered, string Note, string? Remedy);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Machine>;

    internal sealed class Handler(IEnumerable<IAgentProvider> agents, IEnumerable<IEnvironment> environments, IOptions<RunsOptions> options)
        : IAppQueryHandler<Query, Machine>
    {
        public async Task<Machine> Handle(Query query, CancellationToken cancellationToken)
        {
            var offered = new List<string>();
            var notes = new List<EnvironmentNote>();

            foreach (var environment in environments.DistinctBy(environment => environment.Name).OrderBy(environment => environment.Name, StringComparer.Ordinal))
            {
                var readiness = await environment.ReadinessAsync(cancellationToken);

                // The default is where scripts and the gate run, so it stays offered whatever it says.
                if (readiness.Offered || environment.Name == options.Value.Environment)
                {
                    offered.Add(environment.Name);
                }

                if (readiness.Note is { } note)
                {
                    notes.Add(new EnvironmentNote(environment.Name, readiness.Offered, note, readiness.Remedy));
                }
            }

            return new Machine(
                [.. agents.Select(agent => agent.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                offered,
                options.Value.Environment,
                notes
            );
        }
    }
}
