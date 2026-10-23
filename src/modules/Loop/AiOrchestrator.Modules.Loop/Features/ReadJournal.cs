using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// The engine's journal, newest first: one line per decision, each with its reason
/// (DESIGN.md, "What the engine did and why"). The Loop screen lists it whole;
/// a PBI's screen narrows it to that PBI with <c>repository</c> and <c>pbi</c>.
/// </summary>
sealed class ReadJournal : IUseCase
{
    const int Most = 500;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/journal",
                async (string? repository, string? pbi, int? limit, ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(repository, pbi, limit ?? 100), cancellationToken))
            )
            .WithName(nameof(ReadJournal))
            .WithTags("Loop");

    /// <summary>One decision.</summary>
    /// <param name="Id">The line's number, ascending as written; what <see cref="ReadScriptOutput"/> takes.</param>
    /// <param name="Kind">moved, held, released, started, waited or ran; switched when a person turned a
    /// folder's engine on or off, with an empty PBI and <c>on</c> or <c>off</c> as from and to.</param>
    /// <param name="To">Where the PBI went; for a hold, where releasing it leads, or null when a person chooses.</param>
    /// <param name="Why">The reason, in the words of the step that gave it.</param>
    /// <param name="Output">For a ran line, the file holding everything the script printed (#638); null otherwise.</param>
    internal sealed record Line(long Id, DateTimeOffset At, string Repository, string Pbi, string From, string Kind, string? To, string Why, string? Output)
    {
        public static Line Of(JournalEntry entry) =>
            new(entry.Id, entry.At, entry.Repository, entry.Pbi, entry.From, entry.Kind.ToString().ToLowerInvariant(), entry.To, entry.Why, entry.Output);
    }

    // Nothing to scope to: the journal spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string? Repository, string? Pbi, int Limit) : IQuery<IReadOnlyList<Line>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Limit).InclusiveBetween(1, Most);
            RuleFor(query => query.Repository).NotEmpty().When(query => query.Pbi is not null).WithMessage("A PBI is named with its repository.");
        }
    }

    internal sealed class Handler(LoopDbContext database) : IAppQueryHandler<Query, IReadOnlyList<Line>>
    {
        public async Task<IReadOnlyList<Line>> Handle(Query query, CancellationToken cancellationToken)
        {
            var lines = database.Journal.AsNoTracking();
            if (query.Repository is { } repository)
            {
                lines = lines.Where(entry => entry.Repository == repository);
            }
            if (query.Pbi is { } pbi)
            {
                lines = lines.Where(entry => entry.Pbi == pbi);
            }

            var newest = await lines.OrderByDescending(entry => entry.Id).Take(query.Limit).ToListAsync(cancellationToken);
            return [.. newest.Select(Line.Of)];
        }
    }
}
