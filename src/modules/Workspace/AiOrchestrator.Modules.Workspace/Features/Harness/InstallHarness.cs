using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
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
/// <i>Install harness</i> and <i>Reinstall</i> on the folder page (#566): the package this build
/// carries, proposed to the folder's repository as a pull request from <c>harness/install</c>, and the
/// states of its loop created at the vendor. The same request does both, because a reinstall is an
/// install over an older version and the pull request's diff is what tells them apart.
/// <para>
/// <b>Nothing reaches the default branch.</b> The files are committed in a worktree of their own
/// started from it (<see cref="IBranchPublisher"/>), the folder's checkout is not touched, and the
/// merge is the person's. A pull request already open from the branch is the answer, with nothing
/// written: the page links it until it is merged or closed.
/// </para>
/// <para>
/// A file of the package starts with <c>Managed by aio</c> and every update overwrites it; the rest of
/// <c>.aio/</c> is the repository's (#779). The repository's own loop, gate and config stay as its
/// default branch has them; the package writes the template and the stub only where they are missing,
/// so a reinstall never resets a gate a person wrote. Over a repository installed before harness 37
/// the files move: each is written to its new place and removed from where the old layout had it,
/// the repository's own files go with git's rename, the files of its configuration are gathered
/// into <c>.aio/config.json</c>, and what is left of <c>.harness/aio/</c> goes too, so the old layout
/// leaves nothing behind: a script a person had added there shows in the pull request's diff as removed.
/// </para>
/// <para>
/// A write that does not read back is a problem in the answer, not a retry: the pull request is open
/// by then, and the person decides from its page.
/// </para>
/// </summary>
sealed class InstallHarness : IUseCase
{
    /// <summary>The branch every install and reinstall is proposed from.</summary>
    public const string Branch = "harness/install";

    const string GitHub = "github.com/";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/folders/{id:guid}/harness/install",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Installation>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(InstallHarness))
            .WithTags("Workspace");

    /// <param name="Id">The vendor's id for it: the pull request number on GitHub.</param>
    /// <param name="Url">Where a person reviews and merges it.</param>
    internal sealed record InstallPullRequest(string Id, Uri Url);

    /// <param name="Version">The harness version proposed.</param>
    /// <param name="Branch">The branch it is proposed from.</param>
    /// <param name="Base">The default branch it starts from and merges into; null when the pull request was already open.</param>
    /// <param name="PullRequest">The pull request a person merges.</param>
    /// <param name="AlreadyOpen">True when the pull request was open before this request, and nothing was written.</param>
    /// <param name="States">The loop states the vendor has afterwards, ordered by name; empty when nothing was written.</param>
    /// <param name="Kept">The files left as the default branch has them: the repository's own loop and gate.</param>
    /// <param name="Problems">Each write that did not read back as meant, in one sentence.</param>
    internal sealed record Installation(
        string Version,
        string Branch,
        string? Base,
        InstallPullRequest PullRequest,
        bool AlreadyOpen,
        IReadOnlyList<string> States,
        IReadOnlyList<string> Kept,
        IReadOnlyList<string> Problems
    );

    [Requires(WorkspacePermissions.ManageWorktrees)]
    internal sealed record Command(Guid FolderId) : ICommand<ErrorOr<Installation>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, IEnumerable<IPbiProvider> vendors, IBranchPublisher publisher)
        : IAppCommandHandler<Command, ErrorOr<Installation>>
    {
        public async Task<ErrorOr<Installation>> Handle(Command command, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == command.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(command.FolderId);
            }

            var identity = RepositoryIdentity.Of(folder.RemoteUrl);
            if (!folder.IsRepository || identity?.StartsWith(GitHub, StringComparison.Ordinal) != true)
            {
                return Error.Conflict(
                    "Harness.NoVendor",
                    "Its origin is not on github.com, so there is no repository to open the pull request at."
                );
            }

            var vendor = vendors.FirstOrDefault(candidate => candidate.Name == "github");
            if (vendor is null)
            {
                return Error.Conflict("Harness.NoVendor", "No adapter for github is registered on this host.");
            }

            var repository = identity[GitHub.Length..];
            var version = HarnessPackage.Version;

            try
            {
                if (await vendor.GetBranchPullRequestAsync(repository, Branch, cancellationToken) is { } open)
                {
                    return new Installation(version, Branch, null, new(open.Id, open.Url), true, [], [], []);
                }

                var published = await publisher.PublishAsync(
                    LocalPath.Expand(folder.Path),
                    new BranchPublication(
                        Branch,
                        $"Install harness {version}",
                        [
                            .. HarnessPackage.Files.Select(file =>
                                new BranchEntry(
                                    file.Path,
                                    file.Content,
                                    file.Executable,
                                    file.Target,
                                    HarnessPackage.Owned.Contains(file.Path),
                                    HarnessPackage.Former(file.Path),
                                    file.Path == HarnessPackage.Config ? HarnessPackage.Fold : null
                                )
                            ),
                        ],
                        HarnessPackage.Removed,
                        HarnessPackage.Moves
                    ),
                    cancellationToken
                );
                if (published.Commit is null)
                {
                    return Error.Conflict("Harness.NotPushed", published.Refusal ?? $"Nothing was pushed to {Branch}.");
                }

                var problems = new List<string>();
                if (published.Refusal is { } push)
                {
                    problems.Add(push);
                }

                var opened = await vendor.OpenPullRequestAsync(
                    repository,
                    new PullRequestDraft(Branch, $"Install harness {version}", Body(version, published), Draft: false),
                    cancellationToken
                );
                if (opened.Observed is null)
                {
                    return Error.Conflict("Harness.NoPullRequest", opened.Mismatch ?? $"No pull request from {Branch} is open.");
                }
                if (opened.Mismatch is { } pull)
                {
                    problems.Add(pull);
                }

                var states = await vendor.CreateStatesAsync(repository, HarnessPackage.States, cancellationToken);
                if (states.Mismatch is { } created)
                {
                    problems.Add(created);
                }

                return new Installation(
                    version,
                    Branch,
                    published.Base,
                    new(opened.Observed.Id, opened.Observed.Url),
                    false,
                    states.Observed ?? [],
                    published.Kept,
                    problems
                );
            }
            catch (PbiProviderException failure)
            {
                return Error.Conflict("Harness.VendorRefused", failure.Message);
            }
        }

        /// <summary>What the person merging reads: what lands, what is theirs to do next, and what was kept.</summary>
        static string Body(string version, BranchPublish published)
        {
            var kept = published.Kept.Count == 0
                ? ""
                : $"\n\nKept as `{published.Base}` has them, because they are this repository's own: "
                    + string.Join(", ", published.Kept.Select(path => $"`{path}`"))
                    + ".";

            return $"""
                Installs harness {version} as AI Orchestrator ships it: the loop, its scripts, the agent commands and the hook. A file of `.aio/` that starts with `Managed by aio` is the package's and every update overwrites it; the rest is this repository's and the package never overwrites it. A repository that still has `.harness/` gets it moved to `.aio/`. Proposed from `{Branch}`; nothing was written to `{published.Base}`, and merging this pull request is what installs it.

                The loop's states already exist on this repository, so the first PBI the engine moves finds its state there.

                After the merge, `.aio/gate` is a stub that fails every PBI in `verifying` with "define your gate here". Edit it to run this repository's tests, then switch the engine on from the folder's page.{kept}
                """;
        }
    }
}
