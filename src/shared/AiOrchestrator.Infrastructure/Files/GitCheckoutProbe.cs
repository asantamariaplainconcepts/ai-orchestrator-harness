using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// <see cref="ICheckoutProbe"/> over git: <c>symbolic-ref</c> for the branch, <c>rev-parse</c> when
/// it is detached, and <c>remote get-url origin</c>.
/// </summary>
sealed class GitCheckoutProbe(GitCli git) : ICheckoutProbe
{
    public async Task<CheckoutState?> ReadAsync(string path, CancellationToken ct)
    {
        var root = LocalPath.Expand(path);

        // `rev-parse --is-inside-work-tree` refuses a plain directory, a bare repository and the
        // inside of `.git`, which is the distinction AddFolder depends on.
        if (!Directory.Exists(root) || !(await git.Run(root, ["rev-parse", "--is-inside-work-tree"], ct)).Succeeded)
        {
            return null;
        }

        // An unborn branch is named too: the one the first commit will create.
        var head = await git.Run(root, ["symbolic-ref", "--quiet", "--short", "HEAD"], ct);
        var branch = head.Succeeded && head.StandardOutput.Trim() is { Length: > 0 } name
            ? name
            : await Detached(root, ct);

        var origin = await git.Run(root, ["remote", "get-url", "origin"], ct);
        var remote = origin.Succeeded && origin.StandardOutput.Trim() is { Length: > 0 } url ? url : null;

        return new CheckoutState(branch, remote);
    }

    /// <summary>The short sha is what a person can act on, asked of git because git lengthens it as a repository grows.</summary>
    async Task<string> Detached(string root, CancellationToken ct)
    {
        var sha = await git.Run(root, ["rev-parse", "--short", "HEAD"], ct);
        return sha.Succeeded ? $"detached at {sha.StandardOutput.Trim()}" : "detached";
    }
}
