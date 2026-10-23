namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// The files of a repository on one branch, for the PBI's Files tab (DESIGN.md#files,
/// #514). The tree is read from git on the branch, so it answers with no worktree at all; a file's
/// text is read from the checkout of that branch when there is one, so what was saved is what is
/// shown, and from git otherwise.
/// <para>
/// <b>Where the branch is checked out decides where a save lands</b>: the folder's own checkout
/// when it is on the branch, else the worktree that holds it, else nowhere. A save never checks a
/// branch out and never commits; the commit is a person's in a terminal or an agent's in its run.
/// </para>
/// <para>
/// The path is the untrusted input, and git clears it: a read or a write of a path the branch's
/// tree and the checkout's status do not list is refused, which covers <c>../</c>, an absolute
/// path and anything ignored.
/// </para>
/// </summary>
public interface IBranchFiles
{
    /// <summary>
    /// The branch's tree and where it is checked out.
    /// </summary>
    /// <param name="repository">The folder's checkout, absolute.</param>
    /// <param name="branch">The branch, such as <c>change/514</c>, or null for the checkout as it is.</param>
    Task<BranchTree> TreeAsync(string repository, string? branch, CancellationToken ct);

    /// <summary>One file's text, or null when the branch has no such file.</summary>
    Task<BranchFileText?> ReadAsync(string repository, string? branch, string path, CancellationToken ct);

    /// <summary>Writes one file of the branch's checkout to disk, and answers the tree after it.</summary>
    Task<BranchWrite> WriteAsync(string repository, string? branch, string path, string text, CancellationToken ct);
}

/// <param name="Branch">The branch read, or the checkout's own branch when none was named; null on a detached head.</param>
/// <param name="Exists">False when the repository has no such branch, locally or at <c>origin</c>.</param>
/// <param name="Checkout">Where the branch is on disk, or null when no checkout holds it and nothing can be saved.</param>
/// <param name="Files">Every file of the tree, sorted, with the checkout's untracked files in it.</param>
/// <param name="Modified">The files whose checkout differs from the branch's last commit.</param>
/// <param name="Truncated">The tree is longer than <see cref="Files"/>, which stops at a bound.</param>
public sealed record BranchTree(
    string? Branch,
    bool Exists,
    string? Checkout,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Modified,
    bool Truncated
);

/// <param name="Text">Empty when <paramref name="IsBinary"/>.</param>
/// <param name="Truncated">The text stops short of the end of the file, which is then not saved from here.</param>
/// <param name="Bytes">The whole file's size.</param>
public sealed record BranchFileText(string Text, bool IsBinary, bool Truncated, long Bytes);

/// <param name="Tree">The tree after the write, so the count of modified files is the one on disk.</param>
/// <param name="Refusal">Why nothing was written, in words a person can act on; null when it was.</param>
public sealed record BranchWrite(BranchTree Tree, string? Refusal);
