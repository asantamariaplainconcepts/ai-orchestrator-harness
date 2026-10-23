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
/// A reply in a thread of the diff, from the Review tab (#513), under the thread's first comment as
/// the vendor threads it. Answers the reply as read back.
/// </summary>
sealed class ReplyToReview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/review/reply",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewCommentRow>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReplyToReview))
            .WithTags("Pbis");

    /// <param name="CommentId">The id of the thread's first comment.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string CommentId, string Body) : ICommand<ErrorOr<ReviewCommentRow>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.CommentId).NotEmpty().Matches("^[0-9]+$");
            RuleFor(command => command.Body).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppCommandHandler<Command, ErrorOr<ReviewCommentRow>>
    {
        public async Task<ErrorOr<ReviewCommentRow>> Handle(Command command, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, command.Repository, command.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            try
            {
                var written = await resolved.Value.Provider.AddReviewCommentAsync(
                    resolved.Value.Pbi,
                    new ReviewCommentDraft(command.Body, InReplyTo: command.CommentId),
                    cancellationToken
                );
                return written is { Matches: true, Observed: { } comment } ? ReviewCommentRow.Of(comment) : Refused(written.Mismatch);
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }
        }
    }
}
