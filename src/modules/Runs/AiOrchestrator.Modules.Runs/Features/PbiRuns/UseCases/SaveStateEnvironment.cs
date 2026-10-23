using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// Chooses where one agent state of a folder runs (#612), from the folder's page. The next run of that
/// state is placed there; a live run stays where it started. Only an environment this control plane
/// has an adapter for is taken, so the page cannot store a name no run could be placed in.
/// </summary>
sealed class SaveStateEnvironment : IUseCase
{
    internal const int RepositoryLength = 200;

    internal const int StateLength = 100;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/runs/environments",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.Repository, request.State, request.Environment), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadStateEnvironments.Response>()
            .ProducesValidationProblem()
            .WithName(nameof(SaveStateEnvironment))
            .WithTags("Runs");

    /// <param name="Repository">The folder's repository at the vendor, such as <c>owner/name</c>.</param>
    /// <param name="State">The agent state of its loop, such as <c>in-progress</c>.</param>
    /// <param name="Environment">An environment of <c>/api/machine</c>, such as <c>worktree</c>.</param>
    internal sealed record Request(string Repository, string State, string Environment);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string State, string Environment) : ICommand<ErrorOr<ReadStateEnvironments.Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty().MaximumLength(RepositoryLength);
            RuleFor(command => command.State).NotEmpty().MaximumLength(StateLength);
            RuleFor(command => command.Environment).NotEmpty();
        }
    }

    internal sealed class Handler(IEnumerable<IEnvironment> environments, RunsDbContext database)
        : IAppCommandHandler<Command, ErrorOr<ReadStateEnvironments.Response>>
    {
        public async Task<ErrorOr<ReadStateEnvironments.Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!environments.Any(environment => environment.Name == command.Environment))
            {
                var registered = string.Join(", ", environments.Select(environment => environment.Name).Order(StringComparer.Ordinal));
                return Error.Validation(
                    "Runs.NoEnvironment",
                    $"No environment is called '{command.Environment}'. This control plane has {registered}."
                );
            }

            await StateEnvironments.SetAsync(database, command.Repository, command.State, command.Environment, cancellationToken);
            return await ReadStateEnvironments.Read(database, command.Repository, cancellationToken);
        }
    }
}
