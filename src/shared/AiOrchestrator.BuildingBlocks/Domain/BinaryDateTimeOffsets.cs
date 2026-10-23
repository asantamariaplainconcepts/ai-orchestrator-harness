using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiOrchestrator.BuildingBlocks.Domain;

public static class BinaryDateTimeOffsets
{
    /// <summary>
    /// Stores every <see cref="DateTimeOffset"/> as a bit-packed <c>long</c> instead of SQLite's
    /// default <c>TEXT</c>.
    /// <para>
    /// <b>Call this from every module's <c>ConfigureConventions</c>, guarded by
    /// <c>Database.IsSqlite()</c>.</b> SQLite cannot order or compare a <c>DateTimeOffset</c>
    /// stored as text: the first <c>OrderByDescending(task => task.MirroredAt)</c> against it
    /// answered 500 and failed 32 of 73 Tasks functional tests in the #210 spike. This is EF's
    /// documented remedy, not a workaround invented here.
    /// </para>
    /// <para>
    /// The guard matters because the same model also reads Postgres — the one-off import in
    /// <c>tools/</c> opens each module's context on both providers — and there the native
    /// <c>timestamp with time zone</c> is the right mapping. EF's model cache keys on the
    /// provider, so the two shapes coexist in one process.
    /// </para>
    /// <para>
    /// The trade (ADR-0003): ordering across <i>mixed</i> offsets is approximate under this
    /// encoding. Every write in this codebase is UTC, so the caveat is theoretical — if a vendor
    /// timestamp ever has to keep a non-UTC offset <i>and</i> sort, that is one of the ADR's
    /// reversal conditions, not a bug to patch here.
    /// </para>
    /// </summary>
    public static ModelConfigurationBuilder StoreDateTimeOffsetsAsBinary(
        this ModelConfigurationBuilder configurationBuilder
    )
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();

        return configurationBuilder;
    }
}
