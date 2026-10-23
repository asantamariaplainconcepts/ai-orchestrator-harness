using System.Text.RegularExpressions;

namespace AiOrchestrator.BuildingBlocks.Folders;

/// <summary>
/// Whether a task and a checkout are the same repository, compared on the remote.
/// <para>
/// <b>A second implementation of <c>src/frontend/shared/repo.ts</c>, on purpose.</b> The browser has
/// answered this question since the Tasks screen existed — <c>repoIdentity</c>/<c>sameRepo</c> there,
/// against the same two fields this reads: a task's <c>Url</c> from <c>GET /api/pbis</c> and a
/// folder's <c>RemoteUrl</c> from <c>GET /api/folders</c>. Something headless cannot call into a
/// browser, so the rule is written twice rather than shared. Both sides are a short string reduction
/// over a documented wire format with no dependency, which is the same bargain any two clients of one
/// format already make; the risk is drift, and the mitigation is that this comment names its twin.
/// </para>
/// <para>
/// <b>Not <see cref="VendorRepository"/>, which answers a different question.</b> That one addresses a
/// repository the way a vendor's API does — <c>owner/name</c>, host deliberately dropped, parsed with
/// <see cref="Uri"/>. Both properties are wrong here. Comparing without the host conflates a
/// repository on github.com with one of the same name on a private Enterprise host, and
/// <see cref="Uri"/> cannot read <c>git@github.com:owner/name.git</c> at all — which is the form git
/// reports a remote in about half the time, and therefore half the folders this has to match.
/// </para>
/// </summary>
public static partial class RepositoryIdentity
{
    /// <summary>
    /// The repository a URL names, as <c>host/owner/name</c> in lower case, or null when it names
    /// none.
    /// <para>
    /// Written to take both the forms git reports a remote in and the form a vendor links an issue
    /// in, because that is the whole job: the folder side arrives as
    /// <c>git@github.com:owner/name.git</c> or <c>https://github.com/owner/name.git</c>, and the task
    /// side arrives as <c>https://github.com/owner/name/issues/11</c>. All three reduce to
    /// <c>github.com/owner/name</c>.
    /// </para>
    /// <para>
    /// Only the first two path segments after the host are read, which is right for the vendors that
    /// address a repository as <c>owner/name</c>. A vendor that nests deeper gets a
    /// wrong-but-stable answer rather than a crash, and matches nothing — the frontend's own comment
    /// says the same of itself.
    /// </para>
    /// </summary>
    public static string? Of(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var rest = url.Trim();

        // scp-like syntax: `git@github.com:owner/name.git`. It is not a URL and never parses as one,
        // so the colon becomes a slash before anything else looks at it.
        var scp = ScpLike().Match(rest);

        rest = scp.Success
            ? $"{scp.Groups[1].Value}/{scp.Groups[2].Value}"
            : Scheme().Replace(rest, string.Empty);

        // Credentials in the authority, which carry no identity and must not be compared.
        rest = Credentials().Replace(rest, string.Empty);

        // A query or fragment on an issue link, and the `.git` a clone URL usually ends in.
        rest = QueryOrFragment().Replace(rest, string.Empty);
        rest = GitSuffix().Replace(rest, string.Empty);
        rest = TrailingSlashes().Replace(rest, string.Empty);

        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length < 3
            ? null
            : string.Join('/', segments.Take(3)).ToLowerInvariant();
    }

    /// <summary>Whether a task's URL and a folder's remote name the same repository.</summary>
    public static bool Same(string? url, string? remoteUrl)
    {
        var task = Of(url);

        return task is not null && task == Of(remoteUrl);
    }

    /// <summary>
    /// The checkout a task is about: whatever it was attributed to, or else the folder whose
    /// <c>origin</c> is the repository the task came from. Null when nothing matches.
    /// <para>
    /// <b>The second clause is what makes a mirrored task belong anywhere at all.</b> Nothing
    /// attributes a task today — a task mirrored from a vendor arrives with an empty folder path, as
    /// <c>SetPbiStatus</c>'s own comment says — so without it every issue belongs to no checkout and
    /// nothing headless could ever start a run for one.
    /// </para>
    /// <para>
    /// <b>First match wins, and two checkouts of one repository are not an error.</b> The browser's
    /// <c>folderFor</c> settled that already: a task on two folders would be one task apparently in
    /// two places, and a launcher that refused the ambiguity would launch nothing at all for anyone
    /// who keeps a second clone. This keeps the same rule rather than inventing a stricter one.
    /// </para>
    /// <para>
    /// Takes its two fields as accessors rather than an interface, because the shapes it compares are
    /// response records owned by two other modules — a shared type for them would be exactly the
    /// cross-module coupling this whole path exists to avoid.
    /// </para>
    /// </summary>
    public static TFolder? FolderFor<TFolder>(
        string? url,
        string? taskFolderPath,
        IReadOnlyList<TFolder> folders,
        Func<TFolder, string?> pathOf,
        Func<TFolder, string?> remoteOf
    )
        where TFolder : class
    {
        var attributed = string.IsNullOrEmpty(taskFolderPath)
            ? null
            : folders.FirstOrDefault(folder => pathOf(folder) == taskFolderPath);

        return attributed
            ?? folders.FirstOrDefault(folder => Same(url, remoteOf(folder)));
    }

    [GeneratedRegex(@"^[^/@]+@([^/:]+):(.+)$")]
    private static partial Regex ScpLike();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex Scheme();

    [GeneratedRegex(@"^[^/@]*@")]
    private static partial Regex Credentials();

    [GeneratedRegex(@"[?#].*$")]
    private static partial Regex QueryOrFragment();

    [GeneratedRegex(@"\.git/*$", RegexOptions.IgnoreCase)]
    private static partial Regex GitSuffix();

    [GeneratedRegex(@"/+$")]
    private static partial Regex TrailingSlashes();
}
