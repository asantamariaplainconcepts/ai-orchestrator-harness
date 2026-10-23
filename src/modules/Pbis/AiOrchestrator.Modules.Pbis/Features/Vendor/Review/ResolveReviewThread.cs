using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static AiOrchestrator.Modules.Pbis.Features.Vendor.Review.ReviewVendor;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

/// <summary>
/// <i>Resolve</i> on a thread of the diff, from the Review tab (#513). Answers the thread as read
/// back; a thread of another pull request is refused before anything is written.
/// </summary>
sealed class ResolveReviewThread : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/review/resolve",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewThreadRow>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ResolveReviewThread))
            .WithTags("Pbis");

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string ThreadId) : ICommand<ErrorOr<ReviewThreadRow>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.ThreadId).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppCommandHandler<Command, ErrorOr<ReviewThreadRow>>
    {
        public async Task<ErrorOr<ReviewThreadRow>> Handle(Command command, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, command.Repository, command.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            try
            {
                var written = await resolved.Value.Provider.ResolveReviewThreadAsync(resolved.Value.Pbi, command.ThreadId, cancellationToken);
                return written is { Matches: true, Observed: { } thread } ? ReviewThreadRow.Of(thread) : Refused(written.Mismatch);
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }
        }
    }
}
