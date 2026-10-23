using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// Where a folder's agent states run, as its page shows them (#612): the states somebody chose an
/// environment for. A state left out runs in <c>defaultEnvironment</c> of <c>/api/machine</c>. Which
/// states are agent states is the loop's to say, so the page asks <c>/api/loop/validate</c> for them.
/// </summary>
sealed class ReadStateEnvironments : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runs/environments",
                async (string repository, ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(repository), cancellationToken))
            )
            .ProducesValidationProblem()
            .WithName(nameof(ReadStateEnvironments))
            .WithTags("Runs");

    /// <param name="State">The <c>loop.yaml</c> state, such as <c>in-progress</c>.</param>
    /// <param name="Environment">The environment its runs are placed in, such as <c>worktree</c>.</param>
    internal sealed record Choice(string State, string Environment);

    /// <param name="Repository">The folder's repository at the vendor, as asked.</param>
    /// <param name="States">Each state with an environment chosen, by name.</param>
    internal sealed record Response(string Repository, IReadOnlyList<Choice> States);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository) : IQuery<Response>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(query => query.Repository).NotEmpty().MaximumLength(SaveStateEnvironment.RepositoryLength);
    }

    internal sealed class Handler(RunsDbContext database) : IAppQueryHandler<Query, Response>
    {
        public Task<Response> Handle(Query query, CancellationToken cancellationToken) => Read(database, query.Repository, cancellationToken);
    }

    public static async Task<Response> Read(RunsDbContext database, string repository, CancellationToken ct)
    {
        var stored = await StateEnvironments.ListAsync(database, repository, ct);
        return new Response(
            repository,
            [.. stored.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new Choice(pair.Key, pair.Value))]
        );
    }
}
