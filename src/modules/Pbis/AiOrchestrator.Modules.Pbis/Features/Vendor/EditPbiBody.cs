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
/// #634. <c>aio issue edit</c>: the PBI's body replaced at the folder's vendor, where a refine closes
/// the gaps the issue and the code already answer. Answers the body as read back.
/// </summary>
sealed class EditPbiBody : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/edit",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<EditedBody>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(EditPbiBody))
            .WithTags("Pbis");

    /// <param name="Body">The body as the vendor reads it back.</param>
    internal sealed record EditedBody(string Repository, string Id, string Body);

    /// <param name="Body">The whole new body, in the vendor's markdown.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Body) : ICommand<ErrorOr<EditedBody>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Body).NotEmpty().WithMessage("A body cannot be emptied from here.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppCommandHandler<Command, ErrorOr<EditedBody>>
    {
        public async Task<ErrorOr<EditedBody>> Handle(Command command, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, command.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var pbi = new PbiRef(vendor.Value.Folder.Repository, command.Id);
            try
            {
                var written = await vendor.Value.Provider.EditBodyAsync(pbi, command.Body, cancellationToken);
                if (!written.Matches || written.Observed is not { } body)
                {
                    return VendorOf.Refused(written.Mismatch ?? "The vendor answered with no body.");
                }

                return new EditedBody(pbi.Repository, pbi.Id, body);
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
