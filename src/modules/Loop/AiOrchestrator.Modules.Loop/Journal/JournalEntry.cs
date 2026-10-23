namespace AiOrchestrator.Modules.Loop.Journal;

/// <summary>
/// One decision of the engine about one PBI: the line the Loop screen lists under "what the loop
/// decided" (DESIGN.md#the-screens). Written once and never changed.
/// <para>
/// A wait is written only when it differs from the PBI's previous line, so a PBI that sits behind a
/// full WIP for an hour is one line, not sixty.
/// </para>
/// </summary>
sealed class JournalEntry
{
    JournalEntry() { }

    public JournalEntry(DateTimeOffset at, string repository, string pbi, string from, JournalKind kind, string? to, string why, string? output = null)
    {
        At = at;
        Repository = repository;
        Pbi = pbi;
        From = from;
        Kind = kind;
        To = to;
        Why = why;
        Output = output;
    }

    /// <summary>Ascending in the order the entries were written, which is the order they are read in.</summary>
    public long Id { get; private set; }

    public DateTimeOffset At { get; private set; }

    /// <summary>The PBI's repository, such as <c>owner/name</c>.</summary>
    public string Repository { get; private set; } = string.Empty;

    /// <summary>The vendor's id for the PBI, or empty on a <see cref="JournalKind.Switched"/> line.</summary>
    public string Pbi { get; private set; } = string.Empty;

    /// <summary>The state the PBI was in when the engine decided.</summary>
    public string From { get; private set; } = string.Empty;

    public JournalKind Kind { get; private set; }

    /// <summary>
    /// Where the PBI went, for a move and a release; for a hold, the state releasing it leads to, or
    /// null when the person chooses; the state the run belongs to, for a start; null for a wait.
    /// </summary>
    public string? To { get; private set; }

    /// <summary>Why, in words a person reads: the outcome and the reason the step gave for it.</summary>
    public string Why { get; private set; } = string.Empty;

    /// <summary>
    /// For a <see cref="JournalKind.Ran"/> line, the file holding everything the script printed
    /// (#638), which the Journey links to; null on every other line. The why is the script's own
    /// sentence; the file is what a person reads when that sentence is "aspire do ci failed".
    /// </summary>
    public string? Output { get; private set; }
}

/// <summary>What the engine did.</summary>
enum JournalKind
{
    /// <summary>Moved the PBI to another state.</summary>
    Moved,

    /// <summary>Put the hold on: a person decides next.</summary>
    Held,

    /// <summary>Took the hold off because the vendor said a person had approved, and moved on.</summary>
    Released,

    /// <summary>Asked Runs for the state's agent.</summary>
    Started,

    /// <summary>Did nothing this time, and says why: the WIP is full, a run is in flight, a hold is on.</summary>
    Waited,

    /// <summary>Ran one of the state's scripts; the why is the script's name and its own why (#593).</summary>
    Ran,

    /// <summary>
    /// A person turned the engine on or off for a folder (#562). The line is the repository's, not a
    /// PBI's: its PBI is empty, and its from and to are <c>on</c> or <c>off</c>.
    /// </summary>
    Switched,
}
