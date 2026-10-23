namespace AiOrchestrator.Modules.Loop;

/// <summary>The engine's settings, from the <c>Loop</c> section of the host's configuration.</summary>
sealed class LoopSettings
{
    public const string Section = "Loop";

    /// <summary>
    /// Whether the engine runs on its own, one pass every <see cref="IntervalSeconds"/>. Off unless
    /// configured: a pass claims PBIs, writes labels at the vendor and starts agents that spend
    /// money, so a checkout started to look at a screen must not begin working its repository's
    /// issues. The old label watcher starts disabled for the same reason. A pass by hand
    /// (<c>POST /api/loop/run-once</c>) works either way.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Seconds between two passes of the engine. A pass reads every folder's PBIs at the vendor, so
    /// a minute keeps well inside its rate limits. Zero or less schedules nothing.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Where each loop script's output is kept (#638), one file per run of a script under
    /// <c>&lt;repository&gt;/&lt;pbi&gt;/</c>. Unset, it is a <c>scripts</c> directory beside the SQLite
    /// file, next to Runs' <c>runs</c>, so a worktree's AppHost keeps them apart as it keeps its
    /// database apart (ADR-0003).
    /// </summary>
    public string? ScriptsDirectory { get; set; }
}
