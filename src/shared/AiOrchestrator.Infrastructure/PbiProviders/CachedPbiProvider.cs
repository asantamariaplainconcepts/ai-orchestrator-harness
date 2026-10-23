using System.ComponentModel;
using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Extensions.Caching.Hybrid;

namespace AiOrchestrator.Infrastructure.PbiProviders;

/// <summary>
/// An <see cref="IPbiProvider"/> that keeps each read of a repository for <see cref="Ttl"/>, and
/// forgets every read of that repository as soon as anything writes to it through this provider.
/// <para>
/// #617. On 10 October the PBI screen waited 2.4 s for the proposal of #597. Every write the app
/// makes goes through this port, so a screen read after an action shows its result; what other
/// processes write is seen at most <see cref="Ttl"/> late. A write forgets the whole repository, its
/// tag, because moving a sub-issue changes its epic's children and the open list.
/// </para>
/// <para>
/// #627. Inside a <see cref="FreshVendorReads"/> scope a read skips what is kept and stores what the
/// vendor answers, so the refresh button sees the vendor as it is now.
/// </para>
/// </summary>
sealed class CachedPbiProvider(IPbiProvider inner, HybridCache cache) : IPbiProvider
{
    /// <summary>A few seconds: long enough to serve a screen's burst of reads and a tab change, short enough that a change nobody announced shows soon.</summary>
    internal static readonly TimeSpan Ttl = TimeSpan.FromSeconds(10);

    static readonly HybridCacheEntryOptions Kept = new() { Expiration = Ttl, LocalCacheExpiration = Ttl };

    static readonly HybridCacheEntryOptions Fresh = new() { Expiration = Ttl, LocalCacheExpiration = Ttl, Flags = HybridCacheEntryFlags.DisableLocalCacheRead | HybridCacheEntryFlags.DisableDistributedCacheRead };

    public string Name => inner.Name;

    public Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct) => Read(repository, $"list:{query.State}:{query.Limit}:{query.Text}", t => inner.ListAsync(repository, query, t), ct);

    public Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) => Read(pbi.Repository, $"thread:{pbi.Id}", t => inner.ReadThreadAsync(pbi, t), ct);

    public Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct) => Read(pbi.Repository, $"children:{pbi.Id}", t => inner.ListChildrenAsync(pbi, t), ct);

    public Task<IReadOnlyList<byte[]?>> ReadFilesAsync(string repository, IReadOnlyList<RepositoryFile> files, CancellationToken ct) => Read(repository, $"files:{string.Join('|', files.Select(file => $"{file.Reference}:{file.Path}"))}", t => inner.ReadFilesAsync(repository, files, t), ct);

    public Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Read(pbi.Repository, $"pull:{pbi.Id}", t => inner.GetPullRequestAsync(pbi, t), ct);

    public Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) => Read(pbi.Repository, $"change:{pbi.Id}", t => inner.GetPullRequestChangeAsync(pbi, t), ct);

    public Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct) => Read(pbi.Repository, $"review:{pbi.Id}", t => inner.ReadPullRequestReviewAsync(pbi, t), ct);

    public Task<VendorHistory> ReadHistoryAsync(string repository, CancellationToken ct) => Read(repository, "history", t => inner.ReadHistoryAsync(repository, t), ct);

    // Reads passed through: not a repository's, or read once by a flow that writes right after.

    public Task<CliProbe> ProbeAsync(CancellationToken ct) => inner.ProbeAsync(ct);

    public Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct) => inner.ReadAttachmentAsync(address, ct);

    public Task<IReadOnlyList<string>> ListStatesAsync(string repository, CancellationToken ct) => inner.ListStatesAsync(repository, ct);

    public Task<PullRequestInfo?> GetBranchPullRequestAsync(string repository, string branch, CancellationToken ct) => inner.GetBranchPullRequestAsync(repository, branch, ct);

    // Writes: each forgets its repository once it is done, whatever it answered.

    public Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct) => Write(pbi.Repository, () => inner.MoveAsync(pbi, move, ct));

    public Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct) => Write(pbi.Repository, () => inner.CommentAsync(pbi, body, attachments, ct));

    public Task<Written<PbiComment>> EditCommentAsync(PbiRef pbi, string commentId, string body, CancellationToken ct) => Write(pbi.Repository, () => inner.EditCommentAsync(pbi, commentId, body, ct));

    public Task<Written<Pbi>> CreateAsync(string repository, string title, string body, string? parent, CancellationToken ct) => Write(repository, () => inner.CreateAsync(repository, title, body, parent, ct));

    public Task<Written<string>> EditBodyAsync(PbiRef pbi, string body, CancellationToken ct) => Write(pbi.Repository, () => inner.EditBodyAsync(pbi, body, ct));

    public Task<Written<IReadOnlyList<string>>> CreateStatesAsync(string repository, IReadOnlyList<string> states, CancellationToken ct) => Write(repository, () => inner.CreateStatesAsync(repository, states, ct));

    public Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct) => Write(repository, () => inner.OpenPullRequestAsync(repository, draft, ct));

    public Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct) => Write(pbi.Repository, () => inner.AddReviewCommentAsync(pbi, draft, ct));

    public Task<Written<ReviewThread>> ResolveReviewThreadAsync(PbiRef pbi, string threadId, CancellationToken ct) => Write(pbi.Repository, () => inner.ResolveReviewThreadAsync(pbi, threadId, ct));

    public Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct) => Write(pbi.Repository, () => inner.SubmitReviewAsync(pbi, verdict, body, ct));

    /// <summary>The vendor call gets the cache's token, cancelled only when every caller sharing it has stopped waiting; a failed read is not kept.</summary>
    async Task<T> Read<T>(string repository, string what, Func<CancellationToken, Task<T>> read, CancellationToken ct)
    {
        var tag = repository.ToLowerInvariant();
        var held = await cache.GetOrCreateAsync($"{tag}|{what}", async t => new Held<T>(await read(t)), FreshVendorReads.Asked ? Fresh : Kept, [tag], ct);
        return held.Value;
    }

    async Task<T> Write<T>(string repository, Func<Task<T>> write)
    {
        try
        {
            return await write();
        }
        finally
        {
            await cache.RemoveByTagAsync(repository.ToLowerInvariant(), CancellationToken.None);
        }
    }

    /// <summary>Marked immutable so the cache hands back the same instance rather than serializing every read to bytes and back.</summary>
    [ImmutableObject(true)]
    sealed record Held<T>(T Value);
}
