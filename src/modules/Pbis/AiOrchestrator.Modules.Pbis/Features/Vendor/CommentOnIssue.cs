using System.Text.RegularExpressions;
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
/// The Issue tab's composer (#512): a comment published at the vendor as the person, with the images
/// they dropped on it, where the next agent reads it (DESIGN.md, "Issue and Proposal show what is
/// elsewhere"). The vendor's credential is the person's, so the comment is theirs. Answers the
/// comment as read back.
/// <para>
/// The images arrive as base64 in the request, are written to a scratch directory for the vendor's
/// CLI to upload, and are removed once it has answered.
/// </para>
/// </summary>
sealed partial class CommentOnIssue : IUseCase
{
    /// <summary>More than a screenshot of a whole screen at twice its density.</summary>
    const int MaxImageBytes = 10 * 1024 * 1024;

    const int MaxImages = 10;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/issue/comment",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadIssue.IssueComment>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CommentOnIssue))
            .WithTags("Pbis");

    /// <param name="Name">The file's name as it was dropped, which becomes the image's alt text.</param>
    /// <param name="Base64">The file's bytes.</param>
    internal sealed record IssueAttachment(string Name, string Base64);

    /// <param name="Body">The comment, in the vendor's markdown. May be empty when there are images.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Body, IReadOnlyList<IssueAttachment>? Attachments)
        : ICommand<ErrorOr<ReadIssue.IssueComment>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Body)
                .NotEmpty()
                .When(command => command.Attachments is not { Count: > 0 })
                .WithMessage("Write a comment or add an image.");
            RuleFor(command => command.Attachments)
                .Must(attachments => attachments is null || attachments.Count <= MaxImages)
                .WithMessage($"A comment carries at most {MaxImages} images.");
            RuleForEach(command => command.Attachments).ChildRules(attachment =>
            {
                attachment.RuleFor(image => image.Name).NotEmpty().MaximumLength(200);
                attachment.RuleFor(image => image.Base64)
                    .NotEmpty()
                    .Must(base64 => base64.Length <= (MaxImageBytes / 3 + 1) * 4)
                    .WithMessage($"An image is at most {MaxImageBytes / (1024 * 1024)} MB.");
            });
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers)
        : IAppCommandHandler<Command, ErrorOr<ReadIssue.IssueComment>>
    {
        public async Task<ErrorOr<ReadIssue.IssueComment>> Handle(Command command, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, command.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var scratch = Directory.CreateTempSubdirectory("aio-issue-images-");
            try
            {
                var paths = new List<string>();
                foreach (var (image, index) in (command.Attachments ?? []).Select((image, index) => (image, index)))
                {
                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(image.Base64);
                    }
                    catch (FormatException)
                    {
                        return Error.Validation("Attachments", $"{image.Name} is not base64.");
                    }

                    // One directory per image, so two images dropped with the same name both arrive,
                    // each under the name it was dropped with.
                    var directory = Directory.CreateDirectory(Path.Combine(scratch.FullName, $"{index}"));
                    var path = Path.Combine(directory.FullName, SafeName(image.Name));
                    await File.WriteAllBytesAsync(path, bytes, cancellationToken);
                    paths.Add(path);
                }

                var written = await vendor.Value.Provider.CommentAsync(
                    new PbiRef(vendor.Value.Folder.Repository, command.Id),
                    command.Body,
                    paths,
                    cancellationToken
                );
                if (!written.Matches || written.Observed is not { } comment)
                {
                    return VendorOf.Refused(written.Mismatch ?? "The vendor answered with no comment.");
                }

                return new ReadIssue.IssueComment(
                    comment.Id,
                    comment.Author,
                    comment.CreatedAt,
                    comment.Body,
                    comment.Url?.ToString(),
                    ReadIssue.Harness(comment.Body)
                );
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
            finally
            {
                scratch.Delete(recursive: true);
            }
        }

        /// <summary>The dropped name without a directory or a character a shell or a URL would trip on.</summary>
        static string SafeName(string name)
        {
            var bare = Unsafe().Replace(Path.GetFileName(name.Replace('\\', '/')), "-").Trim('-', '.');
            return bare.Length == 0 ? "image" : bare;
        }
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex Unsafe();
}
