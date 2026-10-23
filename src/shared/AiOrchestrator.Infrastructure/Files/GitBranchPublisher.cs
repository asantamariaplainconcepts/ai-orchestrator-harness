using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// <see cref="IBranchPublisher"/> over git: <c>ls-remote --symref</c> names origin's default branch,
/// a detached worktree in the temporary directory starts from it, the entries are written, or moved
/// from where an older layout had them, and committed there, and <c>push --force</c> sends the commit to the branch. The push is read back with
/// <c>ls-remote</c>, and the worktree is removed whatever happened.
/// </summary>
sealed class GitBranchPublisher(GitCli git) : IBranchPublisher
{
    /// <summary>A push to a network remote, and the fetch before it, take longer than a status.</summary>
    static readonly GitCli Network = new(TimeSpan.FromMinutes(2));

    public async Task<BranchPublish> PublishAsync(string repository, BranchPublication publication, CancellationToken ct)
    {
        var root = LocalPath.Expand(repository);
        if (!Directory.Exists(root))
        {
            return Refused(null, $"{root} does not exist on this machine.");
        }

        var head = await Network.Run(root, ["ls-remote", "--symref", "origin", "HEAD"], ct);
        var trunk = head.Succeeded ? DefaultBranch(head.StandardOutput) : null;
        if (trunk is null)
        {
            return Refused(
                null,
                head.Succeeded
                    ? "origin has no default branch yet. Push a first commit to it, then install."
                    : $"origin could not be read: {First(head)}"
            );
        }

        if (string.Equals(trunk, publication.Branch, StringComparison.Ordinal))
        {
            return Refused(trunk, $"{publication.Branch} is origin's default branch, and nothing is written there.");
        }

        var fetch = await Network.Run(
            root,
            ["fetch", "--no-tags", "origin", $"+refs/heads/{trunk}:refs/remotes/origin/{trunk}"],
            ct
        );
        if (!fetch.Succeeded)
        {
            return Refused(trunk, $"origin/{trunk} could not be fetched: {First(fetch)}");
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"aio-publish-{Guid.NewGuid():N}");
        var added = await git.Run(root, ["worktree", "add", "--detach", scratch, $"origin/{trunk}"], ct);
        if (!added.Succeeded)
        {
            return Refused(trunk, $"No worktree could be made from origin/{trunk}: {First(added)}");
        }

        try
        {
            return await Commit(scratch, trunk, publication, ct);
        }
        finally
        {
            // CancellationToken.None: a request cancelled half way still leaves no worktree behind.
            await git.Run(root, ["worktree", "remove", "--force", scratch], CancellationToken.None);
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
                await git.Run(root, ["worktree", "prune"], CancellationToken.None);
            }
        }
    }

    async Task<BranchPublish> Commit(string scratch, string trunk, BranchPublication publication, CancellationToken ct)
    {
        var kept = new List<string>();
        // What left the tree, staged with the entries so its removal is in the commit.
        var moved = new List<string>();
        foreach (var (from, to) in (publication.Moves ?? []).Select(move => (move.From, move.To)))
        {
            var source = Path.Combine(scratch, from);
            var target = Path.Combine(scratch, to);
            if (File.Exists(source) && !Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target);
                moved.AddRange([from, to]);
            }
        }

        foreach (var entry in publication.Entries)
        {
            var path = Path.Combine(scratch, entry.Path);
            var content = entry.Content;
            if (entry.Compose is not null && entry.KeepExisting && !Exists(path))
            {
                content = entry.Compose(read => File.Exists(Path.Combine(scratch, read)) ? File.ReadAllText(Path.Combine(scratch, read)) : null) ?? content;
            }

            var was = (entry.Was ?? []).Where(old => Exists(Path.Combine(scratch, old))).ToList();
            if (was.Count > 0)
            {
                moved.AddRange(was);
                if (entry.KeepExisting && !Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Move(Path.Combine(scratch, was[0]), path);
                    foreach (var other in was.Skip(1))
                    {
                        File.Delete(Path.Combine(scratch, other));
                    }
                    kept.Add(entry.Path);
                    continue;
                }
                foreach (var old in was)
                {
                    File.Delete(Path.Combine(scratch, old));
                }
            }

            if (Exists(path))
            {
                if (entry.KeepExisting)
                {
                    kept.Add(entry.Path);
                    continue;
                }
                if (Directory.Exists(path) && new FileInfo(path).LinkTarget is null)
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (entry.Target is not null)
            {
                File.CreateSymbolicLink(path, entry.Target);
                continue;
            }

            await File.WriteAllBytesAsync(path, content, ct);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, entry.Executable ? Executable : Plain);
            }
        }

        foreach (var removed in publication.Removed ?? [])
        {
            var path = Path.Combine(scratch, removed);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                moved.Add(removed);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
                moved.Add(removed);
            }
        }

        var stage = await git.Run(scratch, ["add", "--all", "--", .. publication.Entries.Select(entry => entry.Path), .. moved], ct);
        if (!stage.Succeeded)
        {
            return Refused(trunk, $"The files could not be staged: {First(stage)}", kept);
        }

        if ((await git.Run(scratch, ["diff", "--cached", "--quiet"], ct)).ExitCode == 0)
        {
            return Refused(trunk, $"origin/{trunk} already has every file as this version writes it, so there is nothing to propose.", kept);
        }

        var commit = await git.Run(scratch, ["commit", "--quiet", "-m", publication.Message], ct);
        if (!commit.Succeeded)
        {
            return Refused(trunk, $"git refused the commit: {First(commit)}", kept);
        }

        var sha = (await git.Run(scratch, ["rev-parse", "HEAD"], ct)).StandardOutput.Trim();
        var push = await Network.Run(scratch, ["push", "--force", "--quiet", "origin", $"HEAD:refs/heads/{publication.Branch}"], ct);
        if (!push.Succeeded)
        {
            return Refused(trunk, $"git refused the push to {publication.Branch}: {First(push)}", kept);
        }

        var read = await Network.Run(scratch, ["ls-remote", "origin", $"refs/heads/{publication.Branch}"], ct);
        var observed = read.Succeeded ? read.StandardOutput.Split('\t', 2)[0].Trim() : "";
        return observed == sha
            ? new BranchPublish(trunk, sha, kept, null)
            : new BranchPublish(
                trunk,
                observed.Length == 0 ? null : observed,
                kept,
                $"{publication.Branch} should be at {sha} after the push, and origin reads {(observed.Length == 0 ? "no such branch" : observed)}."
            );
    }

    const UnixFileMode Plain =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    const UnixFileMode Executable =
        Plain | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>A link counts even when it points at nothing.</summary>
    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    /// <summary>The branch of <c>ref: refs/heads/main	HEAD</c>, the first line <c>ls-remote --symref</c> prints.</summary>
    static string? DefaultBranch(string printed) =>
        printed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("ref: refs/heads/", StringComparison.Ordinal) && line.EndsWith("HEAD", StringComparison.Ordinal))
            .Select(line => line["ref: refs/heads/".Length..].Split('\t', 2)[0].Trim())
            .FirstOrDefault(name => name.Length > 0);

    static string First(GitResult run) =>
        (run.StandardError + "\n" + run.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
        ?? $"git exited with {run.ExitCode}";

    static BranchPublish Refused(string? trunk, string why, IReadOnlyList<string>? kept = null) => new(trunk, null, kept ?? [], why);
}
