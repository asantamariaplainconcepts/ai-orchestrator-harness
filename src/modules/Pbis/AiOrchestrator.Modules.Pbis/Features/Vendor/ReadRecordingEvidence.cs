using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
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
/// #697. A run's recording as the repository's <c>aio/evidence</c> branch keeps it, for the Journey of
/// an instance whose database does not have the run: the journey comment names the path, and this
/// reads it from the vendor and serves it with the media type <c>GET /api/runs/{runId}/recording</c>
/// uses, so the same <c>asciinema-player</c> replays it. Only a path laid out as a recording is read,
/// so the caller cannot reach anything else of the repository.
/// </summary>
sealed class ReadRecordingEvidence : IUseCase
{
    /// <summary>The media type asciinema registers for the format, as the local recording is served.</summary>
    public const string MediaType = "application/x-asciicast";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/recording",
                async (string repository, string path, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, path), cancellationToken);
                    return result.Match(bytes => Results.File(bytes, MediaType), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status200OK, contentType: MediaType)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadRecordingEvidence))
            .WithTags("Pbis");

    /// <param name="Path">On the branch, as <see cref="EvidenceBranch.RecordingOf"/> names it.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Path) : IQuery<ErrorOr<byte[]>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Path)
                .Must(EvidenceBranch.IsRecording)
                .WithMessage("The path is not a recording of aio/evidence, which reads <pbi>/<run id>/session.cast.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<byte[]>>
    {
        public async Task<ErrorOr<byte[]>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            try
            {
                var read = await provider.ReadFilesAsync(folder.Repository, [new RepositoryFile(EvidenceBranch.Name, query.Path)], cancellationToken);
                return read[0] is { } bytes
                    ? bytes
                    : Error.NotFound(
                        "Pbi.NoRecording",
                        $"{folder.Repository} has no {query.Path} on {EvidenceBranch.Name}. The instance that ran it pushes it when its next run ends."
                    );
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
