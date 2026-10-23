using System.Text;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// <see cref="IBranchFiles"/> over git: <c>ls-tree</c> on the branch for the tree,
/// <c>worktree list</c> for where it is checked out, <c>status</c> there for what is modified, and
/// <c>show</c> for a file no checkout holds. Every listing is asked for with <c>-z</c>, because a
/// path may contain a newline.
/// </summary>
sealed class GitBranchFiles(GitCli git) : IBranchFiles
{
    /// <summary>
    /// The longest tree answered. Fifty thousand paths are about three megabytes of JSON, and the
    /// tree on the screen is virtualised, so a repository up to there is listed whole.
    /// </summary>
    const int MaxFiles = 50_000;

    static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<BranchTree> TreeAsync(string repository, string? branch, CancellationToken ct)
    {
        var root = LocalPath.Expand(repository);
        var place = await Locate(root, branch, ct);

        if (place.Ref is null)
        {
            return new BranchTree(place.Branch, Exists: false, null, [], [], Truncated: false);
        }

        var listed = await git.Run(root, ["ls-tree", "-r", "-z", "--name-only", place.Ref], ct);
        var files = new HashSet<string>(listed.Succeeded ? listed.NulSeparated() : [], StringComparer.Ordinal);
        var modified = new List<string>();

        if (place.Checkout is not null)
        {
            foreach (var (path, deleted) in await Status(place.Checkout, ct))
            {
                modified.Add(path);
                if (!deleted)
                {
                    files.Add(path);
                }
            }
        }

        var sorted = files.Order(StringComparer.Ordinal).ToList();
        modified.Sort(StringComparer.Ordinal);

        return new BranchTree(
            place.Branch,
            Exists: true,
            place.Checkout,
            sorted.Count > MaxFiles ? sorted[..MaxFiles] : sorted,
            modified,
            Truncated: sorted.Count > MaxFiles
        );
    }

    public async Task<BranchFileText?> ReadAsync(string repository, string? branch, string path, CancellationToken ct)
    {
        var root = LocalPath.Expand(repository);
        var tree = await TreeAsync(root, branch, ct);

        if (!Lists(tree, path))
        {
            return null;
        }

        if (tree.Checkout is not null && OnDisk(tree.Checkout, path) is { } file)
        {
            return FileText.Read(file);
        }

        // No checkout holds the branch, or the path is a link: the branch's own blob.
        var place = await Locate(root, branch, ct);
        var blob = $"{place.Ref}:{path}";
        var size = await git.Run(root, ["cat-file", "-s", blob], ct);

        if (!size.Succeeded || !long.TryParse(size.StandardOutput.Trim(), out var bytes))
        {
            return null;
        }

        if (bytes > FileText.MaxBytes)
        {
            return new BranchFileText(string.Empty, IsBinary: false, Truncated: true, bytes);
        }

        var shown = await git.Run(root, ["cat-file", "blob", blob], ct);

        if (!shown.Succeeded)
        {
            return null;
        }

        // Git's own test for binary is a NUL early on; a replacement character is a byte that did
        // not decode, which cannot be shown as text either.
        var binary = shown.StandardOutput.Contains('\0') || shown.StandardOutput.Contains('�');
        return new BranchFileText(binary ? string.Empty : shown.StandardOutput, binary, Truncated: false, bytes);
    }

    public async Task<BranchWrite> WriteAsync(
        string repository,
        string? branch,
        string path,
        string text,
        CancellationToken ct
    )
    {
        var root = LocalPath.Expand(repository);
        var tree = await TreeAsync(root, branch, ct);

        if (!tree.Exists)
        {
            return new BranchWrite(tree, $"The repository has no branch {branch}, so there is nothing to save to.");
        }

        if (tree.Checkout is null)
        {
            return new BranchWrite(
                tree,
                $"No checkout on this machine is on {tree.Branch}, so there is nowhere to save. Show the checkout as it is, or open a terminal on the branch."
            );
        }

        if (!Lists(tree, path))
        {
            return new BranchWrite(tree, $"{path} is not a file of {tree.Branch}.");
        }

        if (OnDisk(tree.Checkout, path) is not { } file)
        {
            return new BranchWrite(tree, $"{path} is a link or outside the checkout, so it is not saved from here.");
        }

        await File.WriteAllTextAsync(file, text, Utf8, ct);

        return new BranchWrite(await TreeAsync(root, branch, ct), null);
    }

    static bool Lists(BranchTree tree, string path) =>
        tree.Exists && tree.Files.Contains(path, StringComparer.Ordinal);

    /// <summary>
    /// The file's absolute path in the checkout, or null when it resolves outside it or is a link.
    /// Git listed the path already; this is the second fence, for a tracked link that points away.
    /// </summary>
    static string? OnDisk(string checkout, string path)
    {
        var top = Path.GetFullPath(checkout).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(top, path));

        if (!full.StartsWith(top, StringComparison.Ordinal) || Directory.Exists(full))
        {
            return null;
        }

        var info = new FileInfo(full);
        return info.Exists && info.LinkTarget is not null ? null : full;
    }

    /// <summary>The ref to read, the branch it names, and the checkout that holds it.</summary>
    sealed record Place(string? Branch, string? Ref, string? Checkout);

    async Task<Place> Locate(string root, string? branch, CancellationToken ct)
    {
        var head = await git.Run(root, ["symbolic-ref", "--quiet", "--short", "HEAD"], ct);
        var current = head.Succeeded ? head.StandardOutput.Trim() : null;

        if (branch is null)
        {
            // The checkout as it is, branch or detached head.
            var valid = await git.Run(root, ["rev-parse", "--verify", "--quiet", "HEAD"], ct);
            return valid.Succeeded ? new Place(current, "HEAD", root) : new Place(current, null, null);
        }

        if (await Resolves(root, $"refs/heads/{branch}", ct))
        {
            var checkout = current == branch ? root : await Worktree(root, branch, ct);
            return new Place(branch, $"refs/heads/{branch}", checkout);
        }

        // Pushed and not fetched into a local branch: readable, and no checkout can hold it.
        return await Resolves(root, $"refs/remotes/origin/{branch}", ct)
            ? new Place(branch, $"refs/remotes/origin/{branch}", null)
            : new Place(branch, null, null);
    }

    async Task<bool> Resolves(string root, string reference, CancellationToken ct) =>
        (await git.Run(root, ["rev-parse", "--verify", "--quiet", reference + "^{commit}"], ct)).Succeeded;

    /// <summary>The worktree that has <paramref name="branch"/> checked out, if one is still on disk.</summary>
    async Task<string?> Worktree(string root, string branch, CancellationToken ct)
    {
        var listed = await git.Run(root, ["worktree", "list", "--porcelain", "-z"], ct);

        if (!listed.Succeeded)
        {
            return null;
        }

        string? path = null;
        foreach (var field in listed.NulSeparated())
        {
            if (field.StartsWith("worktree ", StringComparison.Ordinal))
            {
                path = field["worktree ".Length..];
            }
            else if (field == $"branch refs/heads/{branch}" && path is not null && Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Every path <c>status</c> names in the checkout, untracked files one by one, and whether it is
    /// gone from disk. A rename's entry is followed by the path it came from, which is skipped.
    /// </summary>
    async Task<IReadOnlyList<(string Path, bool Deleted)>> Status(string checkout, CancellationToken ct)
    {
        var status = await git.Run(checkout, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], ct);

        if (!status.Succeeded)
        {
            return [];
        }

        var entries = new List<(string, bool)>();
        var fields = status.NulSeparated();

        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Length < 4)
            {
                continue;
            }

            var (staged, worktree) = (field[0], field[1]);
            entries.Add((field[3..], staged == 'D' || worktree == 'D'));

            if (staged is 'R' or 'C')
            {
                index++;
            }
        }

        return entries;
    }
}
