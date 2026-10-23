using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Domain;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// Points the app at a directory on this machine — usually a git checkout, and not necessarily one.
/// <para>
/// <b>The gate here is "what is this", not "is this a repository".</b> A directory git has never
/// heard of is still somewhere a person can open a terminal; what it does not get is the probed
/// branch and remote, and <see cref="Folder.IsRepository"/> is what says so.
/// </para>
/// <para>
/// The one operation with no folder to hold a permission on — there is no folder yet — so it
/// declares <see cref="Access.AnyCaller"/> rather than a permission. On a machine one person owns
/// that is the whole of it; the day this is served to a team, adding a folder is the moment a
/// scope comes into existence and this is where granting it would go.
/// </para>
/// </summary>
sealed class AddFolder : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/folders",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.Path, request.DefaultAgentId), cancellationToken);
                    return result.Match(response => Results.Created($"/api/folders/{response.Id}", response), ApiResults.Problem);
                }
            )
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AddFolder))
            .WithTags("Workspace");

    internal sealed record Request(string Path, string? DefaultAgentId);

    internal sealed record Response(Guid Id, string Name, string Path, string Branch);

    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Path, string? DefaultAgentId)
        : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() =>
            RuleFor(command => command.Path)
                .NotEmpty()
                .MaximumLength(1000)
                .WithMessage("Enter the path to a directory on this machine.");
    }

    internal sealed class Handler(
        WorkspaceDbContext database,
        ICheckoutProbe probe
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var path = Canonical(command.Path);

            // Compared canonical on **both** sides, and in memory rather than in SQL. `Path == path`
            // was a string match, so `~/code/portal` typed by hand and the
            // `/Users/me/code/portal` a chooser returns were two folders over one checkout — and now
            // that a chooser exists, producing that pair takes two clicks. Postgres cannot expand a
            // tilde or resolve a symlink, so the comparison has to happen here; the list is the
            // checkouts on one person's machine, so reading it is a handful of rows.
            //
            // It also means **rows written before this change still match**. A folder stored as the
            // literal `~/code/portal` canonicalises to the same string as the chooser's answer, which
            // is what makes rewriting the existing rows unnecessary rather than merely deferred.
            var existing = await database
                .Folders.AsNoTracking()
                .Select(folder => folder.Path)
                .ToListAsync(cancellationToken);

            if (existing.Any(stored => Canonical(stored) == path))
            {
                return FolderErrors.AlreadyAdded(path);
            }

            // Probed before it is stored, not after. A path the person got wrong is a mistake they
            // can fix while they are still looking at the dialog; discovering it on the next screen
            // would leave a broken row behind that they have to go delete.
            var state = await probe.ReadAsync(path, cancellationToken);

            if (state is null && Refusal(path) is { } refusal)
            {
                return refusal;
            }

            var folder = Folder.Create(
                NameFrom(path),
                path,
                string.IsNullOrWhiteSpace(command.DefaultAgentId)
                    ? "claude-code"
                    : command.DefaultAgentId,
                isRepository: state is not null
            );

            // Only a checkout has either. A plain directory keeps no branch and no remote, and
            // `IsRepository` is what tells every reader those are not readings.
            if (state is not null)
            {
                folder.RecordProbe(state.Branch, state.RemoteUrl);
            }

            database.Folders.Add(folder);
            await database.SaveChangesAsync(cancellationToken);

            return new Response(folder.Id, folder.Name, folder.Path, folder.Branch);
        }

        /// <summary>
        /// Why a path the probe could not describe is not addable, or null when it is — a plain
        /// directory, which is a folder this product is happy to hold.
        /// <para>
        /// <b><see cref="ICheckoutProbe"/> is not asked to tell these apart.</b> Its null fuses "not a
        /// repository" with "git could not be asked", so the distinction is drawn here, once, where
        /// somebody is present to be told.
        /// </para>
        /// <para>
        /// <b>The <c>.git</c> test is the whole of it, and it is deliberately not a git call.</b> A
        /// <c>.git</c> that exists next to a probe that answered null means git should have been
        /// able to describe this and could not: a corrupt repository, a git that is not on
        /// <c>PATH</c>, a linked worktree whose parent checkout has gone. Storing that as a plain
        /// directory would quietly demote a real checkout to one with no branch and no remote, and
        /// the loop would never find its PBIs. Absence of <c>.git</c> is the only evidence of a plain directory there is.
        /// </para>
        /// <para>
        /// It is a file as often as a directory: that is how a linked worktree spells it, and this
        /// is a product whose own repository has three of them open at once.
        /// </para>
        /// </summary>
        static Error? Refusal(string path)
        {
            if (!Directory.Exists(path))
            {
                return FolderErrors.NoSuchDirectory(path);
            }

            var git = Path.Combine(path, ".git");

            return Directory.Exists(git) || File.Exists(git)
                ? FolderErrors.NotDescribable(path)
                : null;
        }

        /// <summary>
        /// The one stored form: tilde expanded, symlinks resolved, no trailing slash.
        /// <para>
        /// The <c>realpath</c> step is orca's lesson rather than tidiness. Its <c>canonicalize</c>
        /// calls <c>realpathSync.native</c> behind an <c>existsSync</c> guard because macOS spells
        /// one directory both <c>/tmp/x</c> and <c>/private/tmp/x</c>, and here that same mismatch
        /// produced a different digest, a different volume and a silently empty database that all
        /// four gates shipped (AGENTS.md). A chooser makes it reachable from the UI: Finder resolves
        /// before it answers, so it hands back the real path of a checkout a person would have typed
        /// by its symlink.
        /// </para>
        /// <para>
        /// <b>The guard is half the lesson.</b> Resolving throws on a path that is not there, which
        /// is exactly when this is comparing — a folder being added by hand, or one removed from disk
        /// since it was stored. A path that does not resolve falls through as the expanded string;
        /// the probe below is about to refuse it by name anyway, and a refusal that says what was
        /// typed is worth more than one that says what it was guessed to mean.
        /// </para>
        /// </summary>
        static string Canonical(string path)
        {
            var expanded = LocalPath.Expand(path).TrimEnd('/');

            if (expanded.Length == 0 || !Path.IsPathRooted(expanded) || !Directory.Exists(expanded))
            {
                return expanded;
            }

            try
            {
                // Segment by segment, and **not** one ResolveLinkTarget on the whole path: that
                // resolves the last component only, which is the one case macOS does not need it
                // for. `/tmp/x` is a real directory whose *parent* is the link — resolving `x` finds
                // nothing and returns `/tmp/x`, still not equal to the `/private/tmp/x` that a
                // chooser and git both report.
                var resolved = Path.GetPathRoot(expanded)!;

                foreach (
                    var segment in expanded.Split(
                        Path.DirectorySeparatorChar,
                        StringSplitOptions.RemoveEmptyEntries
                    )
                )
                {
                    resolved = Path.Combine(resolved, segment);

                    if (
                        Directory.Exists(resolved)
                        && Directory.ResolveLinkTarget(resolved, returnFinalTarget: true) is { } target
                    )
                    {
                        resolved = target.FullName;
                    }
                }

                return resolved.TrimEnd(Path.DirectorySeparatorChar) is { Length: > 0 } trimmed
                    ? trimmed
                    : resolved;
            }
            catch (Exception failure)
                when (failure is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A link that has just been deleted, a cycle, or a directory this process may not
                // traverse. The typed path is a worse key than the real one and a much better one
                // than a 500.
                return expanded;
            }
        }

        /// <summary>The last segment, which is what a person calls the repository.</summary>
        static string NameFrom(string path) =>
            path.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } segments
                ? segments[^1]
                : path;
    }
}
