using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;

namespace AiOrchestrator.Modules.Loop;

/// <summary>
/// What answers <see cref="IRunRequests"/> while no module implements it: a refusal that names the
/// issue that will, so a pass fails saying why instead of the host refusing to start. Registered
/// with <c>TryAdd</c>, so <c>Modules.Runs</c> registering its own (#506) replaces it whichever module
/// is added first.
/// </summary>
sealed class RunsNotWired : IRunRequests
{
    const string Why = "No module implements IRunRequests yet: Modules.Runs does, from #506.";

    public Task<Guid> StartAsync(RunRequest request, CancellationToken ct) => throw new InvalidOperationException(Why);

    public Task<RunStatus?> GetAsync(Guid runId, CancellationToken ct) => throw new InvalidOperationException(Why);

    public Task<IReadOnlyList<RunStatus>> ListLiveAsync(CancellationToken ct) => throw new InvalidOperationException(Why);

    public IScriptPlace OpenScriptPlace(PbiRef pbi, string folderPath, string branch) => throw new InvalidOperationException(Why);

    public Task<TimeSpan> StaleAfterAsync(CancellationToken ct) => throw new InvalidOperationException(Why);
}
