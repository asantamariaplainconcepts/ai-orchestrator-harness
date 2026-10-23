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
/// One file of the PBI's OpenSpec change as bytes, at its pull request's head (#512): the images a
/// proposal or a design shows inside its text. Only a file the pull request carries inside the
/// change's folder is read, so the path a caller names cannot reach anything else of the repository.
/// </summary>
sealed class ReadProposalFile : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/proposal/file",
                async (string repository, string id, string path, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id, path), cancellationToken);
                    return result.Match(file => Served(http, file), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadProposalFile))
            .WithTags("Pbis");

    internal sealed record ChangeFile(byte[] Bytes, string ContentType);

    /// <summary>
    /// The bytes, served so that an SVG opened on its own runs nothing: it comes from this origin, and
    /// a script inside it would otherwise run with this origin's rights.
    /// </summary>
    static IResult Served(HttpContext http, ChangeFile file)
    {
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        return Results.File(file.Bytes, file.ContentType);
    }

    /// <summary>What a file of the change is, by its name and then its first bytes.</summary>
    internal static string MediaType(byte[] bytes, string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".md" or ".txt" or ".yaml" or ".yml" => "text/plain; charset=utf-8",
            _ => bytes is [0x89, 0x50, 0x4E, 0x47, ..] ? "image/png" : "application/octet-stream",
        };

    /// <param name="Path">Relative to the repository root.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id, string Path) : IQuery<ErrorOr<ChangeFile>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
            RuleFor(query => query.Path).NotEmpty().MaximumLength(1000);
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<ChangeFile>>
    {
        public async Task<ErrorOr<ChangeFile>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            var missing = Error.NotFound("Pbi.NoSuchChangeFile", $"The change of #{query.Id} has no file {query.Path}.");
            try
            {
                var files = await provider.GetPullRequestChangeAsync(new PbiRef(folder.Repository, query.Id), cancellationToken);
                if (files is null || ProposalChange.Of(files) is not { } change || change.Files.All(file => file.Path != query.Path))
                {
                    return missing;
                }

                return (await provider.ReadFilesAsync(folder.Repository, [new RepositoryFile(files.HeadSha, query.Path)], cancellationToken))[0] is { } bytes
                    ? new ChangeFile(bytes, MediaType(bytes, query.Path))
                    : missing;
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
