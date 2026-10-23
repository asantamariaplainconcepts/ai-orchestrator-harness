using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Definition;

/// <summary>
/// Validates the text of a loop file and says which states it declares and where each leads — or
/// every rule it breaks. Text in, answer out: reading <c>.aio/loop.yaml</c> from a folder is
/// the engine's job (#505), and the same text checked here is the text it will follow.
/// <para>
/// A route since #515, because Settings asks it whether each folder's loop holds: the
/// folder's YAML comes from Workspace, which cannot read this module's rules.
/// </para>
/// </summary>
sealed class ValidateLoop : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/loop/validate",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(request.Yaml), cancellationToken))
            )
            .WithName(nameof(ValidateLoop))
            .WithTags("Loop");

    internal sealed record Request(string Yaml);

    internal sealed record Transition(string? Outcome, string? Target, string? Hold);

    /// <param name=Agent>
    /// Whether the state runs an agent, the step the folder page lets a person place in an environment
    /// (#612). A state of scripts or a score runs on this machine.
    /// </param>
    internal sealed record State(string Name, IReadOnlyList<Transition> Transitions, bool Agent);

    /// <param name="Undeclared">Targets no state declares, where the loop does nothing.</param>
    /// <param name="Providers">
    /// Each CLI the loop names, in its agents or on a state, so the folder page can say whether that CLI
    /// trusts the folder (#606). A state with neither runs the folder's default agent, which the loop
    /// does not know.
    /// </param>
    internal sealed record Response(
        bool Valid,
        IReadOnlyList<State> States,
        IReadOnlyList<string> Undeclared,
        IReadOnlyList<string> Problems,
        IReadOnlyList<string> Providers
    );

    // Nothing to scope to: the text is checked against the format and against itself, and no
    // folder is read.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Yaml) : IQuery<Response>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(query => query.Yaml).NotNull();
    }

    internal sealed class Handler : IAppQueryHandler<Query, Response>
    {
        public Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var reading = LoopReader.Read(query.Yaml);

            return Task.FromResult(
                reading.Loop is { } loop
                    ? new Response(
                        true,
                        [
                            .. loop.States.Select(state => new State(
                                state.Name,
                                [.. state.Transitions.Select(to => new Transition(to.Outcome, to.Target, to.Hold))],
                                state.Agent is not null || state.Command is not null
                            )),
                        ],
                        loop.Undeclared,
                        [],
                        [
                            .. loop.Agents.Values.Select(agent => agent.Provider)
                                .Concat(loop.States.Select(state => state.Provider).OfType<string>())
                                .Distinct(StringComparer.Ordinal)
                                .Order(StringComparer.Ordinal),
                        ]
                    )
                    : new Response(false, [], [], reading.Problems, [])
            );
        }
    }
}
