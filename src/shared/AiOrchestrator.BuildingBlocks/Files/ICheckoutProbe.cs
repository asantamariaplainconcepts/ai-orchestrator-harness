namespace AiOrchestrator.BuildingBlocks.Files;

/// <summary>
/// What git says about a checkout when it is added as a folder (#515): the branch it is on and the
/// remote it came from, which is what names the vendor's repository.
/// <para>
/// Null fuses "not a repository" with "git could not be asked" on purpose; <c>AddFolder</c>, where a
/// person is present, is what tells the two apart.
/// </para>
/// </summary>
public interface ICheckoutProbe
{
    Task<CheckoutState?> ReadAsync(string path, CancellationToken ct);
}

/// <param name="Branch">The branch, or <c>detached at &lt;sha&gt;</c>.</param>
/// <param name="RemoteUrl">Where <c>origin</c> points, or null for a checkout with no remote.</param>
public sealed record CheckoutState(string Branch, string? RemoteUrl);
