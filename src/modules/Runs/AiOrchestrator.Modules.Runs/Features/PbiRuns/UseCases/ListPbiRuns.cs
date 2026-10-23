using System.Text.Json;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// Every run of one PBI, the loop's and the manual ones, oldest first: what the Journey lists under
/// each state (DESIGN.md#the-screens). Each carries its end record as <c>aio done</c> wrote it,
/// its cost and where its recording is, so a row unfolds without a second request.
/// </summary>
sealed class ListPbiRuns : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runs",
                async (string repository, string pbi, ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(repository, pbi), cancellationToken))
            )
            .WithName(nameof(ListPbiRuns))
            .WithTags("Runs");

    /// <param name="Manual">A person opened it with Terminal or Explore with agent; it moved nothing.</param>
    /// <param name="Provider">The agent CLI, or null for a terminal.</param>
    /// <param name="Command">What the agent was asked, or empty.</param>
    /// <param name="LastOutputAt">When it last wrote, while it is live.</param>
    /// <param name="Stale">Live and silent for longer than <c>Runs:StaleAfter</c>.</param>
    /// <param name="Outcome">The outcome its end record names, once it ended with one.</param>
    /// <param name="Reason">Why, in the agent's words, or the failure's explanation.</param>
    /// <param name="Failure">Why it failed, such as <c>noEndRecord</c>, or null.</param>
    /// <param name="Problems">What the agent found and did not fix.</param>
    /// <param name="EndRecord">The end record exactly as <c>aio done</c> wrote it, or null.</param>
    /// <param name="CostUsd">What it cost, or null when the CLI does not say.</param>
    /// <param name="RecordingPath">Its asciicast v2 recording on the control plane's disk.</param>
    /// <param name="Recorded">Whether that file exists, so <c>GET /api/runs/{runId}/recording</c> has something to serve.</param>
    internal sealed record RunRow(
        Guid RunId,
        string State,
        bool Manual,
        string? Provider,
        string? Model,
        string Command,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        DateTimeOffset? LastOutputAt,
        bool Stale,
        string? Outcome,
        string? Reason,
        string? Failure,
        IReadOnlyList<string> Problems,
        string? EndRecord,
        long? InputTokens,
        long? OutputTokens,
        decimal? CostUsd,
        string RecordingPath,
        bool Recorded
    );

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Pbi) : IQuery<IReadOnlyList<RunRow>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Pbi).NotEmpty();
        }
    }

    internal sealed class Handler(RunBook book) : IAppQueryHandler<Query, IReadOnlyList<RunRow>>
    {
        public async Task<IReadOnlyList<RunRow>> Handle(Query query, CancellationToken cancellationToken)
        {
            var runs = await book.ListAsync(new PbiRef(query.Repository, query.Pbi), cancellationToken);

            return
            [
                .. runs.Select(run => new RunRow(
                    run.RunId,
                    run.State,
                    run.Manual,
                    run.Provider,
                    run.Model,
                    run.Command,
                    run.StartedAt,
                    run.EndedAt,
                    run.LastOutputAt,
                    run.Stale,
                    run.End?.Outcome,
                    run.End?.Reason,
                    run.End?.Failure is { } failure ? JsonNamingPolicy.CamelCase.ConvertName(failure.ToString()) : null,
                    run.End?.Problems ?? [],
                    run.EndRecord,
                    run.InputTokens,
                    run.OutputTokens,
                    run.CostUsd,
                    run.RecordingPath,
                    File.Exists(run.RecordingPath)
                )),
            ];
        }
    }
}
