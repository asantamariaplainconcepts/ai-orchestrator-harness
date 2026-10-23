using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AiOrchestrator.BuildingBlocks.Domain;

public static class DomainGeneratedIds
{
    /// <summary>
    /// Declares that every <c>Id</c> is produced by the domain, not by the store.
    /// <para>
    /// <b>Call this at the end of every module's <c>OnModelCreating</c>.</b> Without it, EF's
    /// default for a Guid key is <c>ValueGeneratedOnAdd</c>, and it then decides Added-versus-Modified
    /// by whether the key still holds its default value. <see cref="BaseEntity"/> assigns a v7 GUID
    /// in its constructor, so a brand-new entity arrives with a non-default key and EF concludes it
    /// must already exist.
    /// </para>
    /// <para>
    /// For a new <i>aggregate</i> that is harmless: <c>Add</c> states the intent explicitly. It bites
    /// on a child appended to an <b>owned collection of an already-tracked parent</b> — adding a
    /// worktree to a folder that was loaded, say. EF emits an UPDATE, it affects zero rows, and the
    /// failure surfaces as <c>DbUpdateConcurrencyException</c>: "expected to affect 1 row(s), but
    /// actually affected 0". Nothing in that message mentions insertion, key generation, or owned
    /// types, which is why this is a one-line fix that takes an afternoon to find.
    /// </para>
    /// </summary>
    public static ModelBuilder IdsAreDomainGenerated(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindProperty(nameof(BaseEntity.Id)) is { ClrType: var type } id && type == typeof(Guid))
            {
                id.ValueGenerated = ValueGenerated.Never;
            }
        }

        return modelBuilder;
    }
}
