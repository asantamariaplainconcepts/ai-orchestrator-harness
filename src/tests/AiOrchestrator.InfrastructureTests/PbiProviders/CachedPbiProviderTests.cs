using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #617. <see cref="CachedPbiProvider"/> over <see cref="GitHubPbiProvider"/> and a <c>gh</c> that
/// answers from memory and counts what it is asked: a read repeated inside the few seconds asks
/// <c>gh</c> nothing, and any write to the repository makes the next read ask again.
/// </summary>
public sealed class CachedPbiProviderTests
{
    readonly FakeGh _gh = new();
    readonly Clock _clock = new();
    readonly CachedPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly PbiQuery Open = new();
    static readonly CancellationToken None = CancellationToken.None;

    public CachedPbiProviderTests()
    {
        _provider = Over(new GitHubPbiProvider(_gh.Run), _clock);
        _gh.Labels.UnionWith(["status:in-progress", "status:holding"]);
        _gh.Issues[7] = new FakeGh.Issue("Pintar en menos de un segundo");
        _gh.Issues[7].Labels.Add("status:in-progress");
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 20));
        _gh.Pulls[20] = new FakeGh.Pull();
    }

    [Fact]
    public async Task A_repeated_read_does_not_ask_gh_again()
    {
        await Reads();
        var asked = _gh.Calls.Count;
        asked.ShouldBe(3, "the PBI, its pull request and the open list are one call each");

        await Reads();

        _gh.Calls.Count.ShouldBe(asked);
    }

    [Fact]
    public async Task Moving_the_PBI_forgets_what_was_read_and_the_next_read_shows_the_move()
    {
        (await Pbi()).State.ShouldBe("in-progress");
        (await _provider.ListAsync(FakeGh.Repository, Open, None)).Single().State.ShouldBe("in-progress");

        var moved = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);
        moved.Matches.ShouldBeTrue(moved.Mismatch);
        var asked = _gh.Calls.Count;

        (await Pbi()).State.ShouldBe("verifying");
        (await _provider.ListAsync(FakeGh.Repository, Open, None)).Single().State.ShouldBe("verifying");
        _gh.Calls.Count.ShouldBe(asked + 2);
    }

    [Fact]
    public async Task A_comment_forgets_the_repository_too()
    {
        await _provider.GetPullRequestAsync(Seven, None);
        await _provider.CommentAsync(Seven, "Visto.", [], None);
        var asked = _gh.Calls.Count;

        await _provider.GetPullRequestAsync(Seven, None);

        _gh.Calls.Count.ShouldBe(asked + 1);
    }

    [Fact]
    public async Task Editing_a_comment_forgets_the_repository_too()
    {
        // #696. The journey comment is edited at every transition, and the Issue tab reads it next.
        var written = await _provider.CommentAsync(Seven, "Visto.", [], None);
        await _provider.GetPullRequestAsync(Seven, None);
        await _provider.EditCommentAsync(Seven, written.Observed.ShouldNotBeNull().Id, "Visto dos veces.", None);
        var asked = _gh.Calls.Count;

        await _provider.GetPullRequestAsync(Seven, None);

        _gh.Calls.Count.ShouldBe(asked + 1);
    }

    [Fact]
    public async Task A_read_older_than_the_ttl_is_asked_again()
    {
        await Pbi();
        _clock.Now += CachedPbiProvider.Ttl - TimeSpan.FromMilliseconds(1);
        await Pbi();
        _gh.Calls.Count.ShouldBe(1);

        _clock.Now += TimeSpan.FromMilliseconds(1);
        await Pbi();

        _gh.Calls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_read_that_failed_is_not_kept()
    {
        _gh.Intercept = call => call is ["issue", "view", "7", ..] ? FakeGh.Fail("HTTP 502: Bad Gateway") : null;
        await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None));

        _gh.Intercept = null;

        (await _provider.ReadThreadAsync(Seven, None)).ShouldNotBeNull();
        _gh.Calls.Count.ShouldBe(2);
    }

    /// <summary>#627. The refresh button's read asks <c>gh</c> again, and what it got serves the next read.</summary>
    [Fact]
    public async Task A_fresh_read_asks_gh_again_and_keeps_what_it_got()
    {
        (await Pbi()).Title.ShouldBe("Pintar en menos de un segundo");
        _gh.Issues[7] = new FakeGh.Issue("Pintar desde la caché");
        _gh.Issues[7].Labels.Add("status:in-progress");

        (await Pbi()).Title.ShouldBe("Pintar en menos de un segundo", "inside the ttl, the kept copy answers");
        _gh.Calls.Count.ShouldBe(1);

        using (FreshVendorReads.Begin())
        {
            (await Pbi()).Title.ShouldBe("Pintar desde la caché");
        }
        _gh.Calls.Count.ShouldBe(2);

        (await Pbi()).Title.ShouldBe("Pintar desde la caché");
        _gh.Calls.Count.ShouldBe(2, "the fresh read replaced the kept copy");
        FreshVendorReads.Asked.ShouldBeFalse();
    }

    /// <summary>The provider over a cache whose clock, the memory cache's included, is <paramref name="clock"/>.</summary>
    internal static CachedPbiProvider Over(IPbiProvider inner, TimeProvider clock)
    {
        var services = new ServiceCollection().AddSingleton(clock);
        services.AddMemoryCache(options => options.Clock = new MemoryClock(clock));
        services.AddHybridCache();
        return new CachedPbiProvider(inner, services.BuildServiceProvider().GetRequiredService<HybridCache>());
    }

    async Task<Pbi> Pbi() => (await _provider.ReadThreadAsync(Seven, None))!.Pbi;

    async Task Reads()
    {
        await Pbi();
        await _provider.GetPullRequestAsync(Seven, None);
        await _provider.ListAsync(FakeGh.Repository, Open, None);
    }

    sealed class MemoryClock(TimeProvider clock) : ISystemClock
    {
        public DateTimeOffset UtcNow => clock.GetUtcNow();
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
