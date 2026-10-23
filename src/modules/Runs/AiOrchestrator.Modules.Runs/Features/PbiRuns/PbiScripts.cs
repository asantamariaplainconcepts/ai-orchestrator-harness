using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// The scripts of a state, run where that PBI's runs run: the environment prepares the PBI's branch,
/// the state's scripts run there one after the other, and the place is retired after the last. #507
/// found the gap end to end: run in the folder's checkout, <c>archive</c> committed on whatever branch
/// a person had left there, and the gate tested that branch and not the PBI's.
/// <para>
/// One place for the state, not one per script (#751): a place retired after the gate took its
/// ignored reports with it, and <c>quality</c>, prepared afresh, found no coverage to measure.
/// </para>
/// <para>
/// Always in <see cref="RunsOptions.Environment"/>, whatever a folder chose for its agent states
/// (#612): the gate needs this machine's SDK and a real worktree.
/// </para>
/// <para>
/// Nothing retries, as for a run: a place that cannot be prepared is a script that could not start,
/// with the environment's sentence, and the loop holds on it.
/// </para>
/// </summary>
sealed partial class PbiScripts(
    IEnumerable<IEnvironment> environments,
    IScriptRunner scripts,
    LiveRuns live,
    IOptions<RunsOptions> options,
    ILogger<PbiScripts> logger
)
{
    /// <summary>The same base a run's place starts from: the remote's default branch.</summary>
    const string BaseRef = "HEAD";

    readonly RunsOptions _options = options.Value;
    readonly IScriptRunner _scripts = scripts;

    public IScriptPlace Open(string folderPath, string branch) => new Place(this, folderPath, branch);

    /// <summary>
    /// Prepared when its first script runs, so a state that runs none, such as a merge with no pull
    /// request, prepares nothing; a refusal is kept and answers every script after it.
    /// </summary>
    sealed class Place(PbiScripts owner, string folderPath, string branch) : IScriptPlace
    {
        (IEnvironment Environment, RunPlace Place)? _prepared;
        string? _refused;

        public async Task<ScriptResult> RunAsync(string script, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            if (_prepared is null && _refused is null)
            {
                (_prepared, _refused) = await owner.Prepare(folderPath, branch, ct);
            }

            return _prepared is { } prepared
                ? await owner._scripts.RunAsync(new ScriptRequest(script, prepared.Place.WorkingDirectory, arguments), ct)
                : new(-1, null, $"{branch} could not be prepared for {script}: {_refused}");
        }

        public async ValueTask DisposeAsync()
        {
            if (_prepared is { } prepared)
            {
                await owner.Retire(prepared.Environment, prepared.Place, branch);
            }
        }
    }

    async Task<((IEnvironment, RunPlace)?, string?)> Prepare(string folderPath, string branch, CancellationToken ct)
    {
        if (environments.FirstOrDefault(environment => environment.Name == _options.Environment) is not { } environment)
        {
            return (null, $"no environment is called '{_options.Environment}'");
        }

        try
        {
            return ((environment, await environment.PrepareAsync(new PlaceRequest(folderPath, branch, BaseRef), ct)), null);
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            return (null, refused.Message);
        }
    }

    /// <summary>
    /// Gives the place back unless a live run works in it. A refusal leaves it on disk: the next
    /// prepare finds it, or names what is in the way.
    /// </summary>
    async Task Retire(IEnvironment environment, RunPlace place, string branch)
    {
        if (live.Uses(place.WorkingDirectory, except: Guid.Empty))
        {
            return;
        }

        try
        {
            await environment.RetireAsync(place, CancellationToken.None);
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            LeftInPlace(logger, branch, place.WorkingDirectory, refused.Message);
        }
    }

    [LoggerMessage(EventId = 5063, Level = LogLevel.Warning, Message = "The scripts of {Branch} left {Place} in place: {Reason}")]
    static partial void LeftInPlace(ILogger logger, string branch, string place, string reason);
}
