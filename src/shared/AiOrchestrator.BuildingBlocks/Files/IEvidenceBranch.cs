using System.Text.RegularExpressions;

namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// #697. The repository's branch of evidence, <c>aio/evidence</c>: an orphan branch at origin that
/// keeps a copy of every run's recording, so an instance that did not run it can replay it from the
/// vendor. Files are only added, each at a path no other run writes, so two instances pushing at once
/// settle with a fetch and another try, never with a conflict.
/// <para>
/// The work happens in a worktree of its own under <c>scratch</c>, removed afterwards: the folder's
/// checkout, its branch, its index and every PBI's worktree are not touched. Nothing here is deleted
/// from the instance: the branch is a copy.
/// </para>
/// </summary>
public interface IEvidenceBranch
{
    /// <summary>
    /// Adds to <see cref="EvidenceBranch.Name"/> at origin every file it does not have yet, one commit
    /// each, and pushes them. A file the branch already has is left as it is.
    /// </summary>
    /// <param name="checkout">The folder's checkout, absolute.</param>
    /// <param name="scratch">A directory of this instance's own where the temporary worktree goes.</param>
    Task<EvidencePush> PushAsync(string checkout, string scratch, IReadOnlyList<EvidenceFile> files, CancellationToken ct);
}

/// <param name="Path">Where it goes on the branch, as <see cref="EvidenceBranch.RecordingOf"/> names a recording.</param>
/// <param name="Source">The file on this machine, absolute.</param>
/// <param name="Message">The commit that adds it, such as <c>12 · in-progress · 3f2a…</c>.</param>
public sealed record EvidenceFile(string Path, string Source, string Message);

/// <param name="Pushed">The paths this push added, in order; empty when the branch had them all.</param>
/// <param name="Refusal">Why they did not reach origin, in words a person can act on; null when they did.</param>
public sealed record EvidencePush(IReadOnlyList<string> Pushed, string? Refusal);

/// <summary>The branch's name and how a recording is laid out on it.</summary>
public static partial class EvidenceBranch
{
    /// <summary>
    /// Outside <c>change/*</c>, which is all <c>claim</c>, <c>merge</c> and <c>cleanup</c> look at, so
    /// none of them takes it for a claim or deletes it.
    /// </summary>
    public const string Name = "aio/evidence";

    /// <summary><c>&lt;pbi&gt;/&lt;run id&gt;/session.cast</c>: unique per run, so two pushes never write one path.</summary>
    public static string RecordingOf(string pbi, Guid runId) => $"{pbi}/{runId:N}/session.cast";

    /// <summary>Whether a path is one <see cref="RecordingOf"/> names, the only thing read back from the branch.</summary>
    public static bool IsRecording(string path) => Recording().IsMatch(path);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*/[0-9a-f]{32}/session\\.cast$")]
    private static partial Regex Recording();
}
