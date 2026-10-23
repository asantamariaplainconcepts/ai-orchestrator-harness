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
/// #634. <c>aio issue create</c>: a new PBI in the folder's vendor, with no state, filed under a parent
/// when one is named. Answers the PBI as read back. A PBI created but not filed under its parent is a
/// conflict whose sentence names the PBI, so the agent knows what exists.
/// </summary>
sealed class CreatePbi : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/create",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<VendorPbi>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CreatePbi))
            .WithTags("Pbis");

    /// <param name="Body">The PBI's body, in the vendor's markdown. May be empty.</param>
    /// <param name="Parent">The vendor's id of the PBI to file it under, in the same repository, or null.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Title, string? Body, string? Parent) : ICommand<ErrorOr<VendorPbi>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Title).NotEmpty().MaximumLength(256);
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppCommandHandler<Command, ErrorOr<VendorPbi>>
    {
        public async Task<ErrorOr<VendorPbi>> Handle(Command command, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, command.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            try
            {
                var written = await provider.CreateAsync(
                    folder.Repository,
                    command.Title,
                    command.Body ?? string.Empty,
                    string.IsNullOrWhiteSpace(command.Parent) ? null : command.Parent,
                    cancellationToken
                );
                if (!written.Matches || written.Observed is not { } pbi)
                {
                    return VendorOf.Refused(written.Mismatch ?? "The vendor answered with no PBI.");
                }

                return VendorPbi.Of(folder, pbi);
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
