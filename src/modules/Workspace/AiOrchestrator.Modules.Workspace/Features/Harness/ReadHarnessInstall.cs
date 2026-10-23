using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Features.Folders;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Harness;

/// <summary>
/// The install pull request still open for a folder's repository (#566), which the folder page links
/// in place of the install button, and in place of the engine's switch while the engine is off. Asked
/// of the vendor on every request: a merge or a close at the vendor shows on the next one.
/// <para>
/// A vendor that cannot answer is a <see cref="InstallState.Problem"/>, as on the states route, so
/// the page still draws.
/// </para>
/// </summary>
sealed class ReadHarnessInstall : IUseCase
{
    const string GitHub = "github.com/";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/folders/{id:guid}/harness/install",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<InstallState>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadHarnessInstall))
            .WithTags("Workspace");

    /// <param name="Branch">The branch an install is proposed from.</param>
    /// <param name="PullRequest">The open pull request from it, or null when there is none or the vendor was not asked.</param>
    /// <param name="Problem">Why the vendor was not asked or did not answer.</param>
    internal sealed record InstallState(string Branch, InstallHarness.InstallPullRequest? PullRequest, string? Problem);

    [Requires(WorkspacePermissions.Read)]
    internal sealed record Query(Guid FolderId) : IQuery<ErrorOr<InstallState>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, IEnumerable<IPbiProvider> vendors)
        : IAppQueryHandler<Query, ErrorOr<InstallState>>
    {
        public async Task<ErrorOr<InstallState>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == query.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(query.FolderId);
            }

            var repository = RepositoryIdentity.Of(folder.RemoteUrl);
            if (repository?.StartsWith(GitHub, StringComparison.Ordinal) != true)
            {
                return new InstallState(InstallHarness.Branch, null, "Its origin is not on github.com, so there is no vendor to ask.");
            }

            var vendor = vendors.FirstOrDefault(candidate => candidate.Name == "github");
            if (vendor is null)
            {
                return new InstallState(InstallHarness.Branch, null, "No adapter for github is registered on this host.");
            }

            try
            {
                var open = await vendor.GetBranchPullRequestAsync(repository[GitHub.Length..], InstallHarness.Branch, cancellationToken);
                return new InstallState(InstallHarness.Branch, open is null ? null : new(open.Id, open.Url), null);
            }
            catch (PbiProviderException failure)
            {
                return new InstallState(InstallHarness.Branch, null, failure.Message);
            }
        }
    }
}
