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
/// The bytes of an image in a PBI's body or comments (#513, #512): an evidence capture or an image of
/// the issue, which a private
/// repository serves to nobody without its credential, so the browser asks here and this asks the
/// vendor with the machine's.
/// <para>
/// <b>Only an address the PBI's body or comments carry, re-read now.</b> The address comes from the browser,
/// and the credential must not read whatever it names: one the comments do not carry is refused
/// before the vendor is asked, as the first tree's byte route refused (#294).
/// </para>
/// </summary>
sealed class ReadAttachment : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/attachment",
                async (string repository, string id, string url, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id, url), cancellationToken);
                    return result.Match(image => Results.File(image.Bytes, image.ContentType), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadAttachment))
            .WithTags("Pbis");

    internal sealed record Image(byte[] Bytes, string ContentType);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id, string Url) : IQuery<ErrorOr<Image>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
            RuleFor(query => query.Url).NotEmpty().MaximumLength(2048);
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<Image>>
    {
        public async Task<ErrorOr<Image>> Handle(Query query, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, query.Repository, query.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            var (pbi, provider) = resolved.Value;
            try
            {
                // The body's images too (#512): the Issue tab draws them from here.
                var thread = await provider.ReadThreadAsync(pbi, cancellationToken);
                var assets = thread is null
                    ? []
                    : EvidenceComments.Assets([new PbiComment("body", thread.Author, thread.CreatedAt, thread.Body), .. thread.Comments]);
                if (!assets.Contains(query.Url) || !Uri.TryCreate(query.Url, UriKind.Absolute, out var address))
                {
                    return Error.NotFound("Attachment.NotCarried", $"No comment of {pbi.Repository}#{pbi.Id} carries that image.");
                }

                return await provider.ReadAttachmentAsync(address, cancellationToken) is { } bytes
                    ? new Image(bytes, ContentType(bytes))
                    : Error.NotFound("Attachment.Unread", "The vendor did not answer with the image.");
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }
        }

        /// <summary>By the bytes, not by the address: an asset address names no type.</summary>
        static string ContentType(byte[] bytes) =>
            bytes switch
            {
                [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
                [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
                [0x47, 0x49, 0x46, ..] => "image/gif",
                [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
                _ => "application/octet-stream",
            };
    }
}
