using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// A repository's loop as the engine reads it (#511): the states of <c>.aio/loop.yaml</c> top to
/// bottom, which the PBI's Journey draws, and the states no one declares, which <i>Move to…</i>
/// offers. Read by the same reader the engine follows, so the Journey never shows a loop the engine
/// would not run. A loop that breaks its rules answers its problem and no states.
/// </summary>
sealed class ReadLoopDefinition : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/definition",
                async (string repository, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<LoopShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadLoopDefinition))
            .WithTags("Loop");

    /// <param name="Name">The loop's name, or null when it cannot be read.</param>
    /// <param name="States">In the order the file declares them.</param>
    /// <param name="Undeclared">Targets no state declares, such as <c>done</c>: the loop does nothing there.</param>
    /// <param name="Problem">Why the loop cannot be followed, or null.</param>
    internal sealed record LoopShape(
        string? Name,
        IReadOnlyList<LoopAgentRow> Agents,
        IReadOnlyList<LoopStateRow> States,
        IReadOnlyList<string> Undeclared,
        string? Problem
    );

    /// <param name="Role">The key under <c>agents</c>, such as <c>coder</c>.</param>
    internal sealed record LoopAgentRow(string Role, string Provider, string? Model);

    /// <param name="Scripts">What runs first, in order.</param>
    /// <param name="Agent">A role from <c>agents</c>, or null.</param>
    /// <param name="Score">The score script, for a state that runs one.</param>
    /// <param name="Back">Where a person's Request changes sends the PBI, or null: the button does not appear.</param>
    /// <param name="Tab">The PBI tab marked while the PBI is here, or null.</param>
    internal sealed record LoopStateRow(
        string Name,
        IReadOnlyList<string> Scripts,
        string? Agent,
        string? Command,
        string? Score,
        string? Provider,
        string? Model,
        int? MaxLoops,
        IReadOnlyList<LoopTransitionRow> Transitions,
        string? Back,
        string? Tab
    );

    /// <param name="Outcome">The key under <c>on</c>, or null for <c>next</c>.</param>
    /// <param name="Target">Where it leads, or null for a hold where the person chooses.</param>
    /// <param name="Hold">The hold's reason, or null.</param>
    internal sealed record LoopTransitionRow(string? Outcome, string? Target, string? Hold);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository) : IQuery<ErrorOr<LoopShape>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(query => query.Repository).NotEmpty();
    }

    internal sealed class Handler(IFolderDirectory folders) : IAppQueryHandler<Query, ErrorOr<LoopShape>>
    {
        public async Task<ErrorOr<LoopShape>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = (await folders.ListAsync(cancellationToken)).FirstOrDefault(candidate =>
                string.Equals(candidate.Repository, query.Repository, StringComparison.OrdinalIgnoreCase)
            );
            if (folder is null)
            {
                return Error.NotFound("Loop.NoFolder", $"No folder is a checkout of {query.Repository}. Add one from the rail.");
            }

            var (loop, problem) = await LoopReader.ReadAsync(folder, cancellationToken);
            if (loop is null)
            {
                return new LoopShape(null, [], [], [], problem);
            }

            return new LoopShape(
                loop.Name,
                [.. loop.Agents.Select(agent => new LoopAgentRow(agent.Key, agent.Value.Provider, agent.Value.Model))],
                [
                    .. loop.States.Select(state => new LoopStateRow(
                        state.Name,
                        state.Scripts,
                        state.Agent,
                        state.Command,
                        state.Score,
                        state.Provider,
                        state.Model,
                        state.MaxLoops,
                        [.. state.Transitions.Select(to => new LoopTransitionRow(to.Outcome, to.Target, to.Hold))],
                        state.Back,
                        state.Tab
                    )),
                ],
                loop.Undeclared,
                null
            );
        }
    }
}
