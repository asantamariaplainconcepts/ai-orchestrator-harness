
namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// Turns token counts into dollars, and declines to when it cannot.
/// <para>
/// <b>This table is a snapshot and it will go stale.</b> Prices change, introductory rates expire,
/// and models ship faster than anybody edits a constant. That is why an unrecognised model prices to
/// <c>null</c> rather than to the nearest neighbour or to zero: a missing cost sends somebody to
/// this file, and a confidently wrong one does not. Orca goes further and reports no dollar figure
/// at all for Claude — only tokens — and reads a cost only where the vendor stores one itself
/// (OpenCode's SQLite <c>cost</c> column). We need the column the history table already has, so we
/// price here; keeping the raw counts next to the result is what makes that recoverable.
/// </para>
/// <para>
/// Rates are per million tokens, from the Anthropic pricing table as of
/// <see cref="PricedOn"/>. Update both together.
/// </para>
/// </summary>
static class ModelPrices
{
    /// <summary>
    /// When the rates below were last checked. Surfaced in nothing yet; it exists so the next
    /// person can tell at a glance whether these numbers predate the model they are looking at.
    /// </summary>
    public static readonly DateOnly PricedOn = new(2026, 8, 14);

    /// <summary>
    /// A cache read costs about a tenth of fresh input, and a cache <i>write</i> costs a quarter
    /// more than it — so the two cache counts pull the total in opposite directions and neither can
    /// be folded into the input rate. Both multipliers are relative to input, which is how Anthropic
    /// documents them, so a price change touches one number per model rather than four.
    /// </summary>
    const decimal CacheReadMultiplier = 0.1m;

    /// <summary>The five-minute TTL, which is the default and what an agent CLI uses.</summary>
    const decimal CacheWriteMultiplier = 1.25m;

    /// <summary>
    /// Keyed by model id prefix, longest match wins — a transcript may name a dated snapshot
    /// (<c>claude-haiku-4-5-20251001</c>) where the table holds the family.
    /// <para>
    /// Claude Sonnet 5 is listed at its standard $3/$15. Its introductory $2/$10 runs to
    /// 2026-08-31 and is deliberately <b>not</b> modelled: a rate that expires in weeks would make
    /// this table silently wrong from September, and overstating a cost is the safer error.
    /// </para>
    /// </summary>
    static readonly (string Prefix, decimal Input, decimal Output)[] PerMillion =
    [
        ("claude-fable-5", 10m, 50m),
        ("claude-mythos-5", 10m, 50m),
        ("claude-opus-5", 5m, 25m),
        ("claude-opus-4-8", 5m, 25m),
        ("claude-opus-4-7", 5m, 25m),
        ("claude-opus-4-6", 5m, 25m),
        ("claude-opus-4-5", 5m, 25m),
        ("claude-sonnet-5", 3m, 15m),
        ("claude-sonnet-4-6", 3m, 15m),
        ("claude-sonnet-4-5", 3m, 15m),
        ("claude-haiku-4-5", 1m, 5m),
    ];

    /// <summary>
    /// Null when the model is unknown or unnamed. Callers keep the token counts regardless, so a
    /// null here costs a display value and not the measurement.
    /// </summary>
    public static decimal? Cost(string? model, long input, long output, long cacheRead, long cacheWrite)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var match = PerMillion
            .Where(entry => model.StartsWith(entry.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Prefix.Length)
            .Select(entry => (Rates: entry, Found: true))
            .FirstOrDefault();

        if (!match.Found)
        {
            return null;
        }

        var (_, inputRate, outputRate) = match.Rates;

        var dollars =
            (input * inputRate
                + output * outputRate
                + cacheRead * inputRate * CacheReadMultiplier
                + cacheWrite * inputRate * CacheWriteMultiplier)
            / 1_000_000m;

        // Four places, matching the column. A short run genuinely costs less than a cent, and
        // rounding it to two would report free work.
        return Math.Round(dollars, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Prices a usage record in place, leaving its counts untouched.</summary>
    public static MeasuredUsage Priced(MeasuredUsage usage) =>
        usage with
        {
            CostUsd = Cost(
                usage.Model,
                usage.InputTokens,
                usage.OutputTokens,
                usage.CacheReadTokens,
                usage.CacheWriteTokens
            ),
        };
}
