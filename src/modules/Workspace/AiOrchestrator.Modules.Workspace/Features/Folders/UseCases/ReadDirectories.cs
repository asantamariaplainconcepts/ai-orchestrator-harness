using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// The subdirectories of one directory on this machine, for browsing to a checkout from the add
/// folder dialog (#692). A browser cannot do it: <c>showDirectoryPicker</c> hands back a handle,
/// never the absolute path <see cref="AddFolder"/> needs.
/// <para>
/// Directories only, never files or their contents, and nothing that starts with a dot. With no
/// path it starts at the home directory.
/// </para>
/// <para>
/// A directory the system will not let this process read is a 403 that says where the permission
/// is given, not an empty list: an empty directory and a denied one look the same as a list, and on
/// macOS <c>~/Documents</c>, <c>~/Desktop</c> and <c>~/Downloads</c> are denied until the person
/// allows them for the app <c>aio</c> was started from. Nothing here asks for that permission.
/// </para>
/// </summary>
sealed class ReadDirectories : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/directories",
                async (string? path, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(path), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<DirectoryListing>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadDirectories))
            .WithTags("Workspace");

    /// <param name="Path">The directory listed, absolute, with <c>~</c> expanded.</param>
    /// <param name="Parent">The directory above it, or null at the root.</param>
    /// <param name="Directories">Its subdirectories by name, those starting with a dot left out.</param>
    internal sealed record DirectoryListing(string Path, string? Parent, IReadOnlyList<DirectoryEntry> Directories);

    /// <param name="IsGitRepository">It has a <c>.git</c>, a directory or the file a linked worktree has.</param>
    internal sealed record DirectoryEntry(string Name, string Path, bool IsGitRepository);

    // Like AddFolder, there is no folder yet to hold a permission on.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string? Path) : IQuery<ErrorOr<DirectoryListing>>;

    internal sealed class Handler : IAppQueryHandler<Query, ErrorOr<DirectoryListing>>
    {
        static readonly EnumerationOptions OneLevel = new()
        {
            // The default is to skip what cannot be read, which would answer a denied directory
            // with an empty list.
            IgnoreInaccessible = false,
            AttributesToSkip = 0,
            RecurseSubdirectories = false,
        };

        public Task<ErrorOr<DirectoryListing>> Handle(Query query, CancellationToken cancellationToken) =>
            Task.FromResult(List(query.Path));

        static ErrorOr<DirectoryListing> List(string? requested)
        {
            var expanded = LocalPath.Expand(string.IsNullOrWhiteSpace(requested) ? "~" : requested);

            if (!Path.IsPathRooted(expanded))
            {
                return Error.Validation(
                    "Directory.NotAbsolute",
                    $"{expanded} is not an absolute path. Start it with / or ~."
                );
            }

            // GetFullPath folds `..` away, and the root keeps its one slash.
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded));

            if (!Directory.Exists(path))
            {
                return NotFound(path);
            }

            List<DirectoryEntry> directories;

            try
            {
                directories =
                [
                    .. new DirectoryInfo(path)
                        .EnumerateDirectories("*", OneLevel)
                        .Where(directory => !directory.Name.StartsWith('.'))
                        .Select(directory => new DirectoryEntry(directory.Name, directory.FullName, IsGitRepository(directory.FullName)))
                        .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(directory => directory.Name, StringComparer.Ordinal),
                ];
            }
            catch (UnauthorizedAccessException)
            {
                return Denied(path);
            }
            catch (DirectoryNotFoundException)
            {
                // Removed between the check and the read.
                return NotFound(path);
            }

            return new DirectoryListing(path, Directory.GetParent(path)?.FullName, directories);
        }

        static bool IsGitRepository(string path)
        {
            var git = Path.Combine(path, ".git");
            return Directory.Exists(git) || File.Exists(git);
        }

        static Error NotFound(string path) =>
            Error.NotFound("Directory.NotFound", $"There is no directory at {path} on this machine.");

        static Error Denied(string path) =>
            Error.Forbidden(
                "Directory.Denied",
                OperatingSystem.IsMacOS()
                    ? $"This machine does not let the console read {path}. Allow it in System Settings → "
                        + "Privacy & Security → Files and Folders, for the app aio was started from, then try again."
                    : $"This machine does not let the console read {path}. Give the user the console runs "
                        + "as permission to read it, then try again."
            );
    }
}
