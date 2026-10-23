using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// The lines one pass writes: each added to the journal as it is decided and kept in order, so the
/// pass answers what it wrote. Saving is the caller's: a <c>started</c> line is saved before its
/// script runs, so the journal says so while it runs, and the rest with the PBI's step.
/// </summary>
sealed class PassJournal(LoopDbContext database, TimeProvider clock)
{
    readonly List<JournalEntry> _written = [];

    public IReadOnlyList<JournalEntry> Written => _written;

    public void Clear() => _written.Clear();

    public void Write(Pbi pbi, string from, JournalKind kind, string? to, string why, string? output = null) =>
        Write(pbi.Ref, from, kind, to, why, output);

    public void Write(PbiRef pbi, string from, JournalKind kind, string? to, string why, string? output = null)
    {
        var entry = new JournalEntry(clock.GetUtcNow(), pbi.Repository, pbi.Id, from, kind, to, why, output);
        database.Journal.Add(entry);
        _written.Add(entry);
    }

    public Task SaveAsync(CancellationToken ct) => database.SaveChangesAsync(ct);
}
