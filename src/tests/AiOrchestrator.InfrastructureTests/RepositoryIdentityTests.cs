using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The backend's copy of <c>src/frontend/shared/repo.ts</c>, which is what lets something headless
/// decide a mirrored task belongs to a checkout on this machine.
/// <para>
/// These cases are the frontend's own comments turned into assertions, deliberately: the two
/// implementations are free to drift, and the only thing that would catch it is a test here naming
/// the same inputs that file names. The pairs below are the ones it calls out — the two forms git
/// reports a remote in, the <c>.git</c> suffix, credentials in the authority, and the host that must
/// keep two same-named repositories apart.
/// </para>
/// </summary>
public sealed class RepositoryIdentityTests
{
    /// <summary>
    /// The whole point: three spellings of one repository, arriving from three different places,
    /// reduce to one string.
    /// </summary>
    [Theory]
    [InlineData("git@github.com:owner/name.git")]
    [InlineData("https://github.com/owner/name.git")]
    [InlineData("https://github.com/owner/name/issues/11")]
    public void Every_shape_of_one_repository_reduces_to_the_same_identity(string url)
    {
        RepositoryIdentity.Of(url).ShouldBe("github.com/owner/name");
    }

    /// <summary>
    /// <b>The host is kept, and this is why.</b> The same owner and name on github.com and on a
    /// private Enterprise host are two repositories. Dropping the host would silently conflate them,
    /// and a launcher would start a run in the wrong checkout.
    /// </summary>
    [Fact]
    public void Two_hosts_serving_the_same_owner_and_name_are_not_the_same_repository()
    {
        RepositoryIdentity
            .Same("https://github.com/owner/name/issues/11", "git@ghe.internal:owner/name.git")
            .ShouldBeFalse();
    }

    /// <summary>Credentials in the authority carry no identity and must not reach the comparison.</summary>
    [Fact]
    public void Credentials_in_the_url_are_not_part_of_the_identity()
    {
        RepositoryIdentity
            .Of("https://user:token@github.com/owner/name.git")
            .ShouldBe("github.com/owner/name");
    }

    /// <summary>
    /// GitHub treats an owner and a name case-insensitively and redirects between spellings, so a
    /// remote and an issue link that differ only in case are one repository.
    /// </summary>
    [Fact]
    public void Case_does_not_separate_a_task_from_its_folder()
    {
        RepositoryIdentity
            .Same("https://github.com/Owner/Name/issues/11", "git@github.com:owner/name.git")
            .ShouldBeTrue();
    }

    /// <summary>A query or fragment on an issue link is not part of the repository it names.</summary>
    [Theory]
    [InlineData("https://github.com/owner/name/issues/11?utm=1")]
    [InlineData("https://github.com/owner/name/issues/11#issuecomment-1")]
    [InlineData("https://github.com/owner/name/")]
    public void Trailing_noise_does_not_change_the_identity(string url)
    {
        RepositoryIdentity.Of(url).ShouldBe("github.com/owner/name");
    }

    /// <summary>
    /// Null rather than a partial answer. A URL naming no repository must not match one — a
    /// half-parsed value that compared equal to another half-parsed value would launch a run in
    /// whatever folder happened to be listed first.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://github.com")]
    [InlineData("https://github.com/owner")]
    [InlineData("not a url at all")]
    public void A_url_that_names_no_repository_is_null(string? url)
    {
        RepositoryIdentity.Of(url).ShouldBeNull();
    }

    /// <summary>Two nulls are not a match, which is the trap a null-propagating comparison falls into.</summary>
    [Fact]
    public void Nothing_matches_nothing()
    {
        RepositoryIdentity.Same(null, null).ShouldBeFalse();
        RepositoryIdentity.Same("https://github.com/owner", "https://github.com/owner").ShouldBeFalse();
    }

    sealed record Folder(string? Path, string? RemoteUrl);

    static Folder? Resolve(string? taskUrl, string? folderPath, params Folder[] folders) =>
        RepositoryIdentity.FolderFor(
            taskUrl,
            folderPath,
            folders,
            folder => folder.Path,
            folder => folder.RemoteUrl
        );

    [Fact]
    public void A_task_resolves_to_the_folder_whose_remote_is_its_repository()
    {
        var match = new Folder("/src/name", "git@github.com:owner/name.git");

        Resolve(
                "https://github.com/owner/name/issues/11",
                folderPath: "",
                new Folder("/src/other", "git@github.com:owner/other.git"),
                match
            )
            .ShouldBe(match);
    }

    /// <summary>
    /// A task whose repository is not cloned here resolves to nothing — and the caller retries it on
    /// a later tick, because a folder can be added after the task was first seen.
    /// </summary>
    [Fact]
    public void A_task_with_no_matching_folder_resolves_to_nothing()
    {
        Resolve(
                "https://github.com/owner/name/issues/11",
                folderPath: "",
                new Folder("/src/other", "git@github.com:owner/other.git")
            )
            .ShouldBeNull();
    }

    /// <summary>
    /// <b>First match wins</b>, the browser's rule kept verbatim: a second clone of one repository
    /// puts the task on one checkout rather than on both.
    /// </summary>
    [Fact]
    public void Two_checkouts_of_one_repository_resolve_to_the_first()
    {
        var first = new Folder("/src/name", "git@github.com:owner/name.git");
        var second = new Folder("/src/name-2", "https://github.com/owner/name.git");

        Resolve("https://github.com/owner/name/issues/11", folderPath: "", first, second)
            .ShouldBe(first);
    }

    /// <summary>
    /// An attributed task goes where it was attributed, ahead of the remote comparison. Nothing
    /// writes that field today, which is exactly why the second clause exists — but the order is the
    /// browser's and is kept, so the day something does write it, both sides still agree.
    /// </summary>
    [Fact]
    public void An_attributed_task_goes_to_its_own_folder_first()
    {
        var attributed = new Folder("/src/name-2", "https://github.com/owner/name.git");

        Resolve(
                "https://github.com/owner/name/issues/11",
                folderPath: "/src/name-2",
                new Folder("/src/name", "git@github.com:owner/name.git"),
                attributed
            )
            .ShouldBe(attributed);
    }
}
