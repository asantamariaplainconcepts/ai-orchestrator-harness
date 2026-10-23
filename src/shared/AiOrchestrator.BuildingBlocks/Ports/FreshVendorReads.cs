namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// #627. While a scope from <see cref="Begin"/> is open on the current async flow, an
/// <see cref="IPbiProvider"/> that keeps its reads asks the vendor again instead of answering from
/// memory, and keeps what it got. The host opens one for a request sent with
/// <c>Cache-Control: no-cache</c>, which is what the PBI screen's refresh button sends.
/// </summary>
public static class FreshVendorReads
{
    static readonly AsyncLocal<bool> Fresh = new();

    /// <summary>Whether the current flow asked for fresh reads.</summary>
    public static bool Asked => Fresh.Value;

    /// <summary>Opens a scope of fresh reads; disposing it restores what was there before.</summary>
    public static IDisposable Begin()
    {
        var before = Fresh.Value;
        Fresh.Value = true;
        return new Scope(before);
    }

    sealed class Scope(bool before) : IDisposable
    {
        public void Dispose() => Fresh.Value = before;
    }
}
