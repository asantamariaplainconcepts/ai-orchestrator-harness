namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>Which run's usage to read.</summary>
/// <param name="AgentId">Which vendor to ask. One reader per vendor; unknown ids read nothing.</param>
/// <param name="WorkingDirectory">
/// The checkout or worktree the agent ran in — the fallback attribution key, used when the vendor
/// would not be told which session id to use. Orca attributes this way throughout, matching the
/// transcript's <c>cwd</c> against known worktrees longest-path-first so a nested worktree wins
/// over its parent.
/// <para>
/// <b>It is a weak key, and measurably so.</b> Any other session in the same checkout — a person's
/// own editor, a second run — falls inside the same directory and the same window. A run that did
/// nothing but count files reported <b>$9.33</b> this way, having swallowed a neighbouring
/// session's 17.8M cache reads. Prefer <paramref name="SessionId"/> wherever the vendor allows it.
/// </para>
/// </param>
/// <param name="From">Start of the window — the run's start, less a small margin.</param>
/// <param name="To">End of the window. The run's end.</param>
/// <param name="SessionId">
/// The run's id, which is also the agent's session id for vendors that let it be pinned. When the
/// vendor honoured it the transcript is named after it and attribution is exact; when it did not,
/// the reader falls back to the directory and the window.
/// </param>
sealed record UsageQuery(
    string AgentId,
    string WorkingDirectory,
    DateTimeOffset From,
    DateTimeOffset To,
    Guid SessionId = default
);

/// <summary>
/// What a run consumed, read back from Claude Code's transcript after it finished. Tokens are the
/// measurement; dollars are an interpretation of it.
/// <para>
/// <b>Both are stored, and the tokens are the durable half.</b> A token count is a fact that stays
/// true; a dollar figure is that fact multiplied by a price that changes without telling us. Keeping
/// only the dollars would leave a number nobody can re-derive or correct, so the counts are kept and
/// <see cref="CostUsd"/> is allowed to be null when the model is not one we hold a price for.
/// </para>
/// </summary>
/// <param name="Model">
/// As the vendor reported it. Recorded even when it is not priced, because "we saw a model we have
/// no price for" is a fixable state and "cost unknown" alone is not.
/// </param>
/// <param name="CacheReadTokens">
/// Priced far below fresh input and usually the largest count of the four. Dropping it — which any
/// attempt to read a cost off the screen necessarily does — understates a long run badly.
/// </param>
/// <param name="CacheWriteTokens">Priced <i>above</i> fresh input, so it cannot be folded in either.</param>
sealed record MeasuredUsage(
    string? Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal? CostUsd
)
{
    /// <summary>What the context actually held, which is every token the model read — cache hits included.</summary>
    public long TotalTokens => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;
}
