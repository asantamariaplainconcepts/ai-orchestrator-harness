using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// <see cref="IEvidenceBranch"/> over git (#697). A detached worktree with no files checked out holds
/// the index the commits are built in, so nothing is written to disk but git's objects: each file is
/// hashed from where it is, added to the tree of <c>origin/aio/evidence</c> (or to an empty one the
/// first time, which makes the branch an orphan), and committed on the one before. The push is not
/// forced. Refused because another instance pushed first, it fetches the branch and builds the
/// commits again on what that one left, up to <see cref="Tries"/> times.
/// </summary>
sealed class GitEvidenceBranch(GitCli git) : IEvidenceBranch
{
    /// <summary>A push to a network remote, and the fetch before it, take longer than a status.</summary>
    static readonly GitCli Network = new(TimeSpan.FromMinutes(2));

    /// <summary>Each refusal is a push that landed elsewhere in between; three in a row is a remote that refuses.</summary>
    const int Tries = 3;

    const string Tracking = "refs/remotes/origin/" + EvidenceBranch.Name;

    public async Task<EvidencePush> PushAsync(string checkout, string scratch, IReadOnlyList<EvidenceFile> files, CancellationToken ct)
    {
        var root = LocalPath.Expand(checkout);
        if (!Directory.Exists(root))
        {
            return new([], $"{root} does not exist on this machine.");
        }

        var wanted = files.Where(file => File.Exists(file.Source)).DistinctBy(file => file.Path, StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return new([], null);
        }

        Directory.CreateDirectory(scratch);
        var place = Path.Combine(scratch, Guid.NewGuid().ToString("N"));
        var added = await git.Run(root, ["worktree", "add", "--quiet", "--detach", "--no-checkout", place], ct);
        if (!added.Succeeded)
        {
            return new([], $"No worktree could be made for {EvidenceBranch.Name}: {First(added)}");
        }

        try
        {
            // What this checkout last knew of the branch is tried first; it is fetched when it knows
            // nothing, and again after every refusal.
            if (!(await git.Run(root, ["rev-parse", "-q", "--verify", Tracking], ct)).Succeeded && await Fetch(root, ct) is { } unread)
            {
                return new([], unread);
            }

            var refused = "";
            for (var attempt = 0; attempt < Tries; attempt++)
            {
                var (pushed, head, failed) = await Commit(place, wanted, ct);
                if (failed is not null)
                {
                    return new([], failed);
                }

                if (head is null)
                {
                    return new([], null);
                }

                var push = await Network.Run(place, ["push", "--quiet", "origin", $"{head}:refs/heads/{EvidenceBranch.Name}"], ct);
                if (push.Succeeded)
                {
                    return new(pushed, null);
                }

                refused = First(push);
                if (await Fetch(root, ct) is { } again)
                {
                    return new([], again);
                }
            }

            return new([], $"origin refused {Tries} pushes to {EvidenceBranch.Name}: {refused}");
        }
        finally
        {
            // CancellationToken.None: a push cancelled half way still leaves no worktree behind.
            await git.Run(root, ["worktree", "remove", "--force", place], CancellationToken.None);
            if (Directory.Exists(place))
            {
                Directory.Delete(place, recursive: true);
                await git.Run(root, ["worktree", "prune"], CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// One commit per file the branch lacks, on the branch as this checkout knows it. The head is null
    /// when it lacks none.
    /// </summary>
    async Task<(IReadOnlyList<string> Pushed, string? Head, string? Refusal)> Commit(string place, IReadOnlyList<EvidenceFile> files, CancellationToken ct)
    {
        var known = await git.Run(place, ["rev-parse", "-q", "--verify", $"{Tracking}^{{commit}}"], ct);
        var parent = known.Succeeded ? known.StandardOutput.Trim() : null;

        var there = new HashSet<string>(StringComparer.Ordinal);
        if (parent is not null)
        {
            var tree = await git.Run(place, ["ls-tree", "-r", "-z", "--name-only", parent], ct);
            if (!tree.Succeeded)
            {
                return ([], null, $"{EvidenceBranch.Name} could not be read: {First(tree)}");
            }

            there.UnionWith(tree.NulSeparated());
        }

        var read = await git.Run(place, parent is null ? ["read-tree", "--empty"] : ["read-tree", parent], ct);
        if (!read.Succeeded)
        {
            return ([], null, $"{EvidenceBranch.Name} could not be read: {First(read)}");
        }

        var pushed = new List<string>();
        var head = parent;
        foreach (var file in files.Where(file => !there.Contains(file.Path)))
        {
            var blob = await git.Run(place, ["hash-object", "-w", "--no-filters", "--", file.Source], ct);
            var staged = blob.Succeeded
                ? await git.Run(place, ["update-index", "--add", "--cacheinfo", $"100644,{blob.StandardOutput.Trim()},{file.Path}"], ct)
                : blob;
            var tree = staged.Succeeded ? await git.Run(place, ["write-tree"], ct) : staged;
            if (!tree.Succeeded)
            {
                return ([], null, $"{file.Path} could not be added: {First(tree)}");
            }

            List<string> arguments = ["commit-tree", tree.StandardOutput.Trim(), "-m", file.Message];
            if (head is not null)
            {
                arguments.AddRange(["-p", head]);
            }

            var commit = await git.Run(place, arguments, ct);
            if (!commit.Succeeded)
            {
                return ([], null, $"git refused the commit of {file.Path}: {First(commit)}");
            }

            head = commit.StandardOutput.Trim();
            pushed.Add(file.Path);
        }

        return (pushed, pushed.Count == 0 ? null : head, null);
    }

    /// <summary>Origin's branch into the checkout's tracking ref, or why not. A branch origin does not have yet is no branch, not a failure.</summary>
    static async Task<string?> Fetch(string root, CancellationToken ct)
    {
        var fetched = await Network.Run(root, ["fetch", "--quiet", "--no-tags", "origin", $"+refs/heads/{EvidenceBranch.Name}:{Tracking}"], ct);
        if (fetched.Succeeded)
        {
            return null;
        }

        if (fetched.StandardError.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase))
        {
            await Network.Run(root, ["update-ref", "-d", Tracking], ct);
            return null;
        }

        return $"origin/{EvidenceBranch.Name} could not be fetched: {First(fetched)}";
    }

    static string First(GitResult run) =>
        (run.StandardError + "\n" + run.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
        ?? $"git exited with {run.ExitCode}";
}
