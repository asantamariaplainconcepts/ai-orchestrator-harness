namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// Proposes files to a checkout's <c>origin</c> on a branch of their own, for installing the harness
/// (#566). The branch starts from origin's default branch, in a worktree of its own that is removed
/// afterwards, so the folder's checkout, its branch and its uncommitted work are not touched.
/// <para>
/// <b>It never writes to the default branch.</b> A branch named like it is refused before anything is
/// written; what lands there is the person's merge of the pull request.
/// </para>
/// <para>
/// The branch is pushed with force: it is the publisher's own, and a branch left behind by a merged
/// pull request has to start again from the default branch, not from its old head.
/// </para>
/// </summary>
public interface IBranchPublisher
{
    /// <param name="repository">The folder's checkout, absolute.</param>
    Task<BranchPublish> PublishAsync(string repository, BranchPublication publication, CancellationToken ct);
}

/// <param name="Branch">The branch to push, such as <c>harness/install</c>.</param>
/// <param name="Message">The commit message.</param>
/// <param name="Entries">What the commit writes; anything else on the default branch stays as it is.</param>
/// <param name="Removed">
/// What the commit removes once the entries are written, a folder with everything in it: the old
/// layout's script folder, after its gate has moved (#729).
/// </param>
/// <param name="Moves">
/// Files of the repository's that the package does not carry and that change place: each is moved,
/// as git sees a rename, when the default branch has it at <see cref="BranchMove.From"/> and nothing
/// at <see cref="BranchMove.To"/> (#779).
/// </param>
public sealed record BranchPublication(
    string Branch,
    string Message,
    IReadOnlyList<BranchEntry> Entries,
    IReadOnlyList<string>? Removed = null,
    IReadOnlyList<BranchMove>? Moves = null
);

/// <param name="From">Where the default branch may have the file, from the repository's top.</param>
/// <param name="To">Where it goes.</param>
public sealed record BranchMove(string From, string To);

/// <param name="Path">From the repository's top, with forward slashes.</param>
/// <param name="Content">The bytes of a file; empty for a link.</param>
/// <param name="Executable">Written with the execute bit.</param>
/// <param name="Target">Where a symbolic link points, from its directory; null for a file.</param>
/// <param name="KeepExisting">Written only when the default branch does not have it: a file the repository makes its own.</param>
/// <param name="Was">
/// Where the default branch may have the same file from an older layout (#729), the nearest layout
/// first. The first it has is the file's source, and each it has is removed, so the file moves
/// instead of being copied; with <paramref name="KeepExisting"/> and nothing at <paramref name="Path"/>,
/// the file there is the one the default branch had at the source.
/// </param>
/// <param name="Compose">
/// What to write instead of <paramref name="Content"/> when <paramref name="KeepExisting"/> finds
/// nothing at <paramref name="Path"/>: it reads a file of the default branch by its path and answers the
/// bytes, or null to write <paramref name="Content"/>. A file that gathers several old ones (#779).
/// </param>
public sealed record BranchEntry(
    string Path,
    byte[] Content,
    bool Executable,
    string? Target,
    bool KeepExisting = false,
    IReadOnlyList<string>? Was = null,
    Func<Func<string, string?>, byte[]?>? Compose = null
);

/// <param name="Base">The default branch the commit starts from, or null when it could not be read.</param>
/// <param name="Commit">The commit origin's branch points at, read back after the push; null when nothing was pushed.</param>
/// <param name="Kept">The entries left as the default branch has them, because of <see cref="BranchEntry.KeepExisting"/>.</param>
/// <param name="Refusal">Why nothing was pushed, or why the push did not read back, in words a person can act on; null when it did.</param>
public sealed record BranchPublish(string? Base, string? Commit, IReadOnlyList<string> Kept, string? Refusal);
