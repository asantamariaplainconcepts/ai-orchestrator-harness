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

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// A comment anchored to a line of the PBI's OpenSpec change (#512): a review comment on the pull
/// request, on that file and that line, which is where the coder reads it in its next run
/// (<c>/aio:propose</c>, "Read"). A reply to a thread is one more comment on the same line.
/// Answers the comment as read back.
/// </summary>
sealed class CommentOnProposal : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/proposal/comment",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadProposal.ProposalComment>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CommentOnProposal))
            .WithTags("Pbis");

    /// <param name="Path">The change's file, relative to the repository root.</param>
    /// <param name="Line">The line of that file at the pull request's head, from 1.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Path, int Line, string Body)
        : ICommand<ErrorOr<ReadProposal.ProposalComment>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Path).NotEmpty();
            RuleFor(command => command.Line).GreaterThan(0);
            RuleFor(command => command.Body).NotEmpty().WithMessage("Write the comment first.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers)
        : IAppCommandHandler<Command, ErrorOr<ReadProposal.ProposalComment>>
    {
        public async Task<ErrorOr<ReadProposal.ProposalComment>> Handle(Command command, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, command.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            var pbi = new PbiRef(folder.Repository, command.Id);
            try
            {
                var files = await provider.GetPullRequestChangeAsync(pbi, cancellationToken);
                if (files is null)
                {
                    return Error.Conflict("Pbi.NoPullRequest", $"#{command.Id} has no pull request to comment on yet.");
                }

                if (ProposalChange.Of(files) is not { } change || change.Files.All(file => file.Path != command.Path))
                {
                    return Error.NotFound("Pbi.NoSuchChangeFile", $"The change of #{command.Id} has no file {command.Path}.");
                }

                var written = await provider.AddReviewCommentAsync(pbi, new ReviewCommentDraft(command.Body, command.Path, command.Line), cancellationToken);
                if (!written.Matches || written.Observed is not { } comment)
                {
                    return VendorOf.Refused(written.Mismatch ?? "The vendor answered with no comment.");
                }

                return new ReadProposal.ProposalComment(comment.Id, comment.Path, comment.Line, comment.Author, comment.CreatedAt, comment.Body);
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
