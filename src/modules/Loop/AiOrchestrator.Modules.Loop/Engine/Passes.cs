namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// What the Loop screen's strip says about the engine now: when it last looked, what that pass did,
/// and when it looks next. A singleton, because it outlives every request that reads it.
/// <para>
/// It also keeps passes one at a time. A pass by hand and a scheduled one at the same moment would
/// read the same live runs and the same journal, and each could ask for a run on the same PBI.
/// </para>
/// </summary>
sealed class Passes
{
    readonly SemaphoreSlim _one = new(1, 1);
    readonly List<RunningScript> _scripts = [];
    LastPass? _last;
    DateTimeOffset? _next;
    CancellationTokenSource _changed = new();

    /// <summary>
    /// Cancelled when a person changes a setting the schedule follows (#562), so the wait in progress
    /// ends and the schedule reads the settings again rather than at the end of an interval they no
    /// longer name. Read it before reading the settings, so a change in between is not missed.
    /// </summary>
    public CancellationToken Changes => Volatile.Read(ref _changed).Token;

    /// <summary>
    /// Tells the schedule that its settings changed. The old source is not disposed: the schedule
    /// may still be linking to its token, and an old source holds no timer.
    /// </summary>
    public void Wake() => Interlocked.Exchange(ref _changed, new()).Cancel();

    int _soon;

    /// <summary>
    /// Asks the schedule for a pass now rather than at the end of its interval (#706): a run ended
    /// waiting for a person, and its hold should reach Needs you within the minute.
    /// </summary>
    public void Soon()
    {
        Interlocked.Exchange(ref _soon, 1);
        Wake();
    }

    /// <summary>Whether a pass was asked for since the schedule last looked, and forgets it.</summary>
    public bool TakeSoon() => Interlocked.Exchange(ref _soon, 0) == 1;

    /// <summary>The latest pass, or null when none has run since this process started.</summary>
    public LastPass? Last => Volatile.Read(ref _last);

    /// <summary>When the schedule runs its next pass, or null when nothing is scheduled.</summary>
    public DateTimeOffset? Next
    {
        get
        {
            lock (_one)
            {
                return _next;
            }
        }
        set
        {
            lock (_one)
            {
                _next = value;
            }
        }
    }

    /// <summary>
    /// The scripts a pass is running now (#597). A script runs inside the pass and leaves nothing at
    /// Runs, so this is the only place that knows one is running until it ends. Kept here and not in
    /// the journal: a process that dies mid-script leaves a started line, but the script died too.
    /// </summary>
    public IReadOnlyList<RunningScript> Scripts
    {
        get
        {
            lock (_scripts)
            {
                return [.. _scripts];
            }
        }
    }

    /// <summary>Counts <paramref name="script"/> as running until the answer is disposed.</summary>
    public IDisposable Running(RunningScript script)
    {
        lock (_scripts)
        {
            _scripts.Add(script);
        }

        return new Ended(() =>
        {
            lock (_scripts)
            {
                _scripts.Remove(script);
            }
        });
    }

    sealed class Ended(Action end) : IDisposable
    {
        public void Dispose() => end();
    }

    /// <summary>
    /// Runs <paramref name="work"/> between two passes, without counting it as one: a person's
    /// release or move from the PBI's header (#511), which must not interleave with a pass deciding
    /// about the same PBI.
    /// </summary>
    public async Task<T> Between<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await _one.WaitAsync(ct);
        try
        {
            return await work();
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>Runs <paramref name="pass"/> once no other pass is running, and keeps what it did.</summary>
    public Task<Pass> OneAtATime(Func<Task<Pass>> pass, DateTimeOffset at, CancellationToken ct) =>
        Between(
            async () =>
            {
                try
                {
                    var done = await pass();
                    Volatile.Write(ref _last, new LastPass(at, done.Folders, done.Pbis, done.Skipped, done.Written.Count, null));
                    return done;
                }
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    Volatile.Write(ref _last, new LastPass(at, 0, 0, [], 0, failure.Message));
                    throw;
                }
            },
            ct
        );
}

/// <summary>One pass as the strip shows it.</summary>
/// <param name="At">When it started.</param>
/// <param name="Folders">Folders whose loop it followed.</param>
/// <param name="Pbis">Open PBIs it looked at.</param>
/// <param name="Skipped">Each folder it could not follow, and why.</param>
/// <param name="Written">How many journal lines it wrote.</param>
/// <param name="Failure">Why it stopped before the end, or null when it finished.</param>
sealed record LastPass(DateTimeOffset At, int Folders, int Pbis, IReadOnlyList<string> Skipped, int Written, string? Failure);

/// <summary>A script of a state that a pass is running on a PBI.</summary>
/// <param name="Since">When the engine started it.</param>
sealed record RunningScript(string Repository, string Pbi, string State, string Script, DateTimeOffset Since);
