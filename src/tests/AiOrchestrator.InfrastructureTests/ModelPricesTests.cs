using AiOrchestrator.Infrastructure.AgentProviders;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// Pricing, and specifically the two ways of getting it wrong that are easy to ship.
/// <para>
/// The first is folding the cache counts into the input rate, or dropping them. They are priced
/// differently from input and from each other — a read at a tenth, a write at a quarter more — and
/// on a real run they dwarf everything else. The second is guessing a rate for a model the table
/// does not know, which produces a confident number nobody can check.
/// </para>
/// </summary>
public sealed class ModelPricesTests
{
    /// <summary>
    /// The measured shape of a real session on this repository, kept as a fixed case because the
    /// arithmetic is the whole argument for reading transcripts at all: priced on input and output
    /// alone these tokens come to $10.53, and the cache counts take it to $118.98.
    /// </summary>
    [Fact]
    public void Cache_tokens_dominate_a_real_run_and_are_priced()
    {
        var cost = ModelPrices.Cost(
            "claude-opus-5",
            input: 1_812,
            output: 420_913,
            cacheRead: 202_594_580,
            cacheWrite: 1_144_135
        );

        cost.ShouldNotBeNull();
        cost.Value.ShouldBe(118.9800m, tolerance: 0.0001m);

        var withoutCache = ModelPrices.Cost("claude-opus-5", 1_812, 420_913, 0, 0);
        withoutCache.ShouldNotBeNull();
        withoutCache.Value.ShouldBe(10.5319m, tolerance: 0.0001m);
    }

    [Fact]
    public void A_cache_read_costs_a_tenth_of_fresh_input()
    {
        var input = ModelPrices.Cost("claude-opus-5", 1_000_000, 0, 0, 0);
        var cached = ModelPrices.Cost("claude-opus-5", 0, 0, 1_000_000, 0);

        input.ShouldBe(5.00m);
        cached.ShouldBe(0.50m);
    }

    /// <summary>Above input, not below — so the two cache counts do not cancel out.</summary>
    [Fact]
    public void A_cache_write_costs_a_quarter_more_than_fresh_input()
    {
        ModelPrices.Cost("claude-opus-5", 0, 0, 0, 1_000_000).ShouldBe(6.25m);
    }

    /// <summary>A transcript may name a dated snapshot where the table holds the family.</summary>
    [Theory]
    [InlineData("claude-haiku-4-5", 1.00)]
    [InlineData("claude-haiku-4-5-20251001", 1.00)]
    [InlineData("claude-sonnet-5", 3.00)]
    [InlineData("claude-opus-4-8", 5.00)]
    [InlineData("claude-fable-5", 10.00)]
    public void A_model_id_resolves_by_longest_prefix(string model, decimal perMillionInput)
    {
        ModelPrices.Cost(model, 1_000_000, 0, 0, 0).ShouldBe(perMillionInput);
    }

    /// <summary>
    /// Null rather than a nearest neighbour or a zero. A missing cost sends somebody to the price
    /// table; a confidently wrong one does not.
    /// </summary>
    [Theory]
    [InlineData("some-future-model")]
    [InlineData("gpt-4")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unpriced_model_reports_nothing(string? model)
    {
        ModelPrices.Cost(model, 1_000_000, 1_000_000, 1_000_000, 1_000_000).ShouldBeNull();
    }

    /// <summary>
    /// Four decimal places, matching the column. A short run genuinely costs less than a cent, and
    /// rounding it to two would report free work.
    /// </summary>
    [Fact]
    public void A_run_costing_less_than_a_cent_is_not_rounded_to_nothing()
    {
        var cost = ModelPrices.Cost("claude-haiku-4-5", input: 1_000, output: 0, 0, 0);

        cost.ShouldNotBeNull();
        cost.Value.ShouldBeGreaterThan(0m);
        cost.Value.ShouldBe(0.0010m);
    }

    [Fact]
    public void Nothing_used_costs_nothing()
    {
        ModelPrices.Cost("claude-opus-5", 0, 0, 0, 0).ShouldBe(0m);
    }
}
