using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.SharedFunctionalTests;

/// <summary>
/// A vendor that answers nothing: every member a test does not override throws. Nine fakes wrote the
/// same twenty refusals each before #668; a fake now overrides the questions its screen asks. A fake
/// that answers one of the port's defaulted members lists <see cref="IPbiProvider"/> again, so its
/// own public member is the one the port calls.
/// </summary>
public abstract class VendorStub : IPbiProvider
{
    public virtual string Name => "github";

    public virtual Task<CliProbe> ProbeAsync(CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<IReadOnlyList<byte[]?>> ReadFilesAsync(string repository, IReadOnlyList<RepositoryFile> files, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct) => throw new NotSupportedException();

    public virtual Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<Written<ReviewThread>> ResolveReviewThreadAsync(PbiRef pbi, string threadId, CancellationToken ct) =>
        throw new NotSupportedException();

    public virtual Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct) =>
        throw new NotSupportedException();
}
