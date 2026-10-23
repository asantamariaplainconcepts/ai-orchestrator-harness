using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// #734. The Metrics screen of a folder: how the loop works its repository, in four blocks (the agents
/// by step, what people did at each hold, the flow, and the notes that change how a number reads), over
/// the last 7 or 30 days or all of it, and for one epic's sub-issues when one is named. One read of the
/// journal, the repository's runs and the vendor's history; <see cref="MetricsReading"/> does the rest.
/// Every number carries the PBIs or runs it counts, so the screen can list them.
/// </summary>
sealed class ReadMetrics : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/metrics",
                async (string repository, int? days, string? epic, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, days, epic), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<MetricsReport>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadMetrics))
            .WithTags("Loop");

    /// <param name="From">The start of the period, or null for all of it.</param>
    /// <param name="To">When it was read.</param>
    /// <param name="Epic">The epic counted alone, or null.</param>
    /// <param name="Epics">The epics of the repository, which the filter offers.</param>
    /// <param name="Pbis">Every PBI the numbers name, with its title, to list them by name.</param>
    internal sealed record MetricsReport(
        string Repository,
        DateTimeOffset? From,
        DateTimeOffset To,
        string? Epic,
        IReadOnlyList<MetricsPbi> Epics,
        IReadOnlyList<MetricsPbi> Pbis,
        MetricsAgents Agents,
        MetricsPeople People,
        MetricsFlow Flow,
        IReadOnlyList<MetricsNote> Notes
    );

    /// <param name="Title">As the vendor has it, or empty when it could not be read.</param>
    internal sealed record MetricsPbi(string Id, string Title);

    /// <param name="Steps">One per state whose agent ran, in the loop's order.</param>
    /// <param name="Runs">The loop's runs of the period, newest first.</param>
    internal sealed record MetricsAgents(IReadOnlyList<MetricsStep> Steps, IReadOnlyList<MetricsRun> Runs);

    /// <param name="Command">What the state's agent was asked, such as <c>/aio:implement</c>.</param>
    /// <param name="Runs">Every run of the step in the period.</param>
    /// <param name="Succeeded">Those that ended with an outcome.</param>
    /// <param name="MedianSeconds">Of the ended runs, or null when none ended.</param>
    /// <param name="CostUsd">Of the runs that said what they cost.</param>
    /// <param name="Tokens">Read and written, of the runs that said.</param>
    /// <param name="CostPerRun">The cost over the runs that said it, or null.</param>
    /// <param name="TokensPerRun">The tokens over the runs that said them, or null.</param>
    internal sealed record MetricsStep(
        string State,
        string? Command,
        IReadOnlyList<Guid> Runs,
        IReadOnlyList<Guid> Succeeded,
        double? MedianSeconds,
        double? P90Seconds,
        decimal CostUsd,
        long Tokens,
        decimal? CostPerRun,
        long? TokensPerRun
    );

    /// <param name="Seconds">How long it ran, or null while it is live.</param>
    /// <param name="Result">The outcome, the failure such as <c>notLoggedIn</c>, or <c>live</c>.</param>
    /// <param name="Succeeded">It ended with an outcome.</param>
    /// <param name="CostUsd">Null when the CLI did not say.</param>
    internal sealed record MetricsRun(
        Guid RunId,
        string State,
        string Pbi,
        DateTimeOffset StartedAt,
        double? Seconds,
        string Result,
        bool Succeeded,
        decimal? CostUsd,
        long? Tokens
    );

    /// <param name="Holds">By reason, the most frequent first.</param>
    /// <param name="Decisions">The states where a person approves or asks for changes.</param>
    /// <param name="MergedWithPerson">PBIs whose merged pull request has a commit without the loop's mark.</param>
    /// <param name="MergedAgentOnly">PBIs whose merged pull request is all the loop's.</param>
    internal sealed record MetricsPeople(
        IReadOnlyList<MetricsHoldReason> Holds,
        IReadOnlyList<MetricsDecisionPoint> Decisions,
        IReadOnlyList<string> MergedWithPerson,
        IReadOnlyList<string> MergedAgentOnly
    );

    /// <summary>The holds of one reason, a PBI per hold, by how each ended.</summary>
    /// <param name="Unchanged">Released with no commit of a person on the branch while it was held.</param>
    /// <param name="AfterCommit">Released after a person committed on the branch.</param>
    /// <param name="Returned">Sent back: a request for changes, or a state above.</param>
    /// <param name="Closed">The PBI closed while held, without reaching done.</param>
    /// <param name="Open">Still held.</param>
    /// <param name="MedianSeconds">Time in hold of the ended ones.</param>
    /// <param name="AlwaysUnchanged">Every ended hold of the reason was released unchanged: it caught nothing.</param>
    internal sealed record MetricsHoldReason(
        string Reason,
        IReadOnlyList<string> Unchanged,
        IReadOnlyList<string> AfterCommit,
        IReadOnlyList<string> Returned,
        IReadOnlyList<string> Closed,
        IReadOnlyList<string> Open,
        double? MedianSeconds,
        double? MaxSeconds,
        bool AlwaysUnchanged
    );

    /// <param name="Tab">The tab the state names, <c>proposal</c> for the plan, <c>review</c> for the merge.</param>
    /// <param name="AsIs">Held, and approved as it was.</param>
    /// <param name="AfterChanges">Approved after a person asked for changes.</param>
    /// <param name="WithoutPerson">Left forward without a hold: the score let it through.</param>
    internal sealed record MetricsDecisionPoint(
        string State,
        string? Tab,
        IReadOnlyList<string> AsIs,
        IReadOnlyList<string> AfterChanges,
        IReadOnlyList<string> WithoutPerson
    );

    /// <param name="Done">The PBIs that reached done in the period, the order they did.</param>
    /// <param name="P50Seconds">From a PBI's first journal line to done.</param>
    /// <param name="Epics">Each epic from its first sub-issue's first line to its close.</param>
    /// <param name="Rework">Each PBI that went back, with how many times.</param>
    /// <param name="Weeks">PBIs to done per week, Monday first.</param>
    internal sealed record MetricsFlow(
        IReadOnlyList<string> Done,
        double? P50Seconds,
        double? P75Seconds,
        double? P90Seconds,
        IReadOnlyList<MetricsEpicFlow> Epics,
        IReadOnlyList<MetricsRework> Rework,
        IReadOnlyList<MetricsWeek> Weeks
    );

    /// <param name="Pbis">Its sub-issues the journal knows.</param>
    /// <param name="Closed">Null while it is open.</param>
    /// <param name="Seconds">Null while it is open.</param>
    internal sealed record MetricsEpicFlow(string Id, string Title, IReadOnlyList<string> Pbis, DateTimeOffset Started, DateTimeOffset? Closed, double? Seconds);

    internal sealed record MetricsRework(string Pbi, int Rounds);

    internal sealed record MetricsWeek(DateTimeOffset Week, IReadOnlyList<string> Pbis);

    /// <summary>A warning that changes how a number reads; the screen words it from its kind.</summary>
    /// <param name="Kind">
    /// <c>unpricedRuns</c>, <c>liveRuns</c>, <c>openHolds</c>, <c>manualRuns</c>, <c>firstRun</c>,
    /// <c>noRuns</c>, <c>olderPullRequests</c>, <c>unmarkedCommits</c> or <c>vendorUnread</c>.
    /// </param>
    /// <param name="At">
    /// For <c>firstRun</c>, when the repository's first run started; for <c>unmarkedCommits</c>, the
    /// earliest commit with the loop's mark, before which none is classified.
    /// </param>
    /// <param name="Detail">For <c>vendorUnread</c>, what the vendor said.</param>
    internal sealed record MetricsNote(string Kind, int Count, IReadOnlyList<string> Pbis, IReadOnlyList<Guid> Runs, DateTimeOffset? At, string? Detail);

    /// <param name="Repository">The folder's repository, such as <c>owner/name</c>.</param>
    /// <param name="Days">7 or 30, or null for all of it.</param>
    /// <param name="Epic">An epic's id, to count its sub-issues alone.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, int? Days, string? Epic) : IQuery<ErrorOr<MetricsReport>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Days).Must(days => days is null or 7 or 30).WithMessage("The period is 7 or 30 days, or all of it.");
        }
    }

    internal sealed class Handler(
        IFolderDirectory folders,
        IEnumerable<IPbiProvider> providers,
        IRunRequests runs,
        LoopDbContext database,
        TimeProvider clock
    ) : IAppQueryHandler<Query, ErrorOr<MetricsReport>>
    {
        public async Task<ErrorOr<MetricsReport>> Handle(Query query, CancellationToken cancellationToken)
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
                return Error.Conflict("Loop.Unreadable", $"{folder.Path}: {problem}");
            }

            // A wait says nothing a number here counts, and it is most of the journal.
            var journal = await database
                .Journal.AsNoTracking()
                .Where(entry => entry.Repository == folder.Repository && entry.Kind != JournalKind.Waited)
                .OrderBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            VendorHistory? history = null;
            string? unread = null;
            try
            {
                history = providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is { } provider
                    ? await provider.ReadHistoryAsync(folder.Repository, cancellationToken)
                    : throw new PbiProviderException($"No adapter reads {folder.Vendor}.");
            }
            catch (PbiProviderException failure)
            {
                unread = failure.Message;
            }

            var now = clock.GetUtcNow();
            return MetricsReading.Read(
                new MetricsReading.Input(
                    folder.Repository,
                    loop,
                    journal,
                    await runs.ListRepositoryAsync(folder.Repository, cancellationToken),
                    history,
                    unread,
                    now,
                    query.Days is { } days ? now.AddDays(-days) : null,
                    string.IsNullOrWhiteSpace(query.Epic) ? null : query.Epic
                )
            );
        }
    }
}
