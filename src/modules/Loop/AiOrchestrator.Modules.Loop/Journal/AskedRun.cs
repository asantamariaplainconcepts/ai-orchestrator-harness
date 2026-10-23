namespace AiOrchestrator.Modules.Loop.Journal;

/// <summary>
/// A run the engine asked <c>IRunRequests</c> for, kept so the next pass knows which run to ask
/// about. The engine asks rather than waits: a run that ended while this process was down is read
/// on the first pass after it comes back.
/// </summary>
sealed class AskedRun
{
    AskedRun() { }

    public AskedRun(Guid runId, string repository, string pbi, string state, DateTimeOffset askedAt)
    {
        RunId = runId;
        Repository = repository;
        Pbi = pbi;
        State = state;
        AskedAt = askedAt;
    }

    public Guid RunId { get; private set; }

    public string Repository { get; private set; } = string.Empty;

    public string Pbi { get; private set; } = string.Empty;

    /// <summary>The state the run belongs to. A PBI a person moved elsewhere leaves its run behind.</summary>
    public string State { get; private set; } = string.Empty;

    public DateTimeOffset AskedAt { get; private set; }

    /// <summary>Whether the engine has acted on how the run ended. A run is acted on once.</summary>
    public bool Consumed { get; private set; }

    public void Consume() => Consumed = true;
}
