using ErrorOr;

namespace AiOrchestrator.Modules.Workspace.Features.Folders;

/// <summary>
/// The refusals this module can return. Kept beside the slices rather than in a shared errors
/// file per module-with-one-aggregate: there is exactly one thing that can be missing.
/// </summary>
static class FolderErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Folder.NotFound", $"No folder with id {id} is known to this machine.");

    public static Error AlreadyAdded(string path) =>
        Error.Conflict("Folder.AlreadyAdded", $"{path} is already on the list.");

    /// <summary>
    /// There is nothing at that path. Separated from the refusal below because the two have nothing
    /// in common but the null they came from: one is a typo, the other is a repository in trouble.
    /// </summary>
    public static Error NoSuchDirectory(string path) =>
        Error.Validation(
            "Folder.NoSuchDirectory",
            $"There is no directory at {path} on this machine."
        );

    /// <summary>
    /// A <c>.git</c> is there and git could not describe the checkout anyway.
    /// <para>
    /// Refused rather than stored as a plain directory, which is the other thing an undescribable
    /// path could be turned into. A folder added that way would look like it worked and then have no
    /// branch and no remote for as long as it existed.
    /// </para>
    /// <para>
    /// It names the three causes because they have three different fixes and this product cannot
    /// tell which one it is: the probe answers null to all of them on purpose.
    /// </para>
    /// </summary>
    public static Error NotDescribable(string path) =>
        Error.Validation(
            "Folder.NotDescribable",
            $"{path} has a .git, but git could not describe it. The repository may be corrupt, "
                + "git may not be on this server's PATH, or this may be a worktree whose checkout "
                + "has been removed."
        );

    /// <summary>
    /// One sentence for "git does not list that path here", which covers a file deleted since the tree
    /// was drawn, one that is ignored, and one that is not in this working directory at all. They are
    /// deliberately not distinguished: a caller probing paths learns nothing from the difference.
    /// </summary>
    public static Error NoSuchFile(string file) =>
        Error.NotFound("Folder.NoSuchFile", $"git does not list '{file}' in this working directory.");

    /// <summary>
    /// A conflict, not a failure: a stale path or a missing editor is the caller's world, not a
    /// broken server, and the sentence already says what to do about it.
    /// </summary>
    public static Error DesktopUnavailable(string detail) =>
        Error.Conflict("Folder.DesktopUnavailable", detail);
}
