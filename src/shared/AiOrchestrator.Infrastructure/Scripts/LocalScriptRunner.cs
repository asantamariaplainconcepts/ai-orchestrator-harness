using AiOrchestrator.BuildingBlocks.Ports;
using CliWrap;

namespace AiOrchestrator.Infrastructure.Scripts;

/// <summary>
/// Runs the script <see cref="HarnessScripts.Find"/> names in the checkout it is pointed at and reads
/// the JSON line the script ends on.
/// <para>
/// The file is executed, not handed to <c>bash</c>: the scripts carry their own interpreter on the
/// shebang line, and <c>evidence</c> is Node; <c>HarnessScript</c>, which runs the environment's
/// own two, runs them under bash. No deadline either: the gate runs for minutes, and the caller's token is what stops
/// a run.
/// </para>
/// <para>
/// Standard output is kept whole, because the line that matters is the last one and a bound would
/// cut exactly that. The login shell's PATH is given to the script, or <c>gh</c>, <c>git</c> and
/// <c>aspire</c> go missing when the server was started from anywhere but a terminal.
/// </para>
/// </summary>
public sealed class LocalScriptRunner : IScriptRunner
{
    public async Task<ScriptResult> RunAsync(ScriptRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A name, not a path: `../x` would run a file outside the harness.
        if (request.Script.Length == 0 || request.Script.IndexOfAny(['/', '\\']) >= 0 || request.Script.StartsWith('.'))
        {
            return new ScriptResult(-1, null, $"'{request.Script}' is not the name of a script in .aio/.");
        }

        if (HarnessScripts.Find(request.WorkingDirectory, request.Script) is not { } script)
        {
            return new ScriptResult(-1, null, $"{request.WorkingDirectory} has no {request.Script} in .aio/ or .aio/steps/, nor in the .harness/ folders of an install before harness 37.");
        }

        // No pager and no prompt: either would wait for somebody who is not there.
        var environment = LoginShellEnvironment.PathOverride();
        environment["GH_PAGER"] = "cat";
        environment["GH_PROMPT_DISABLED"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";

        // #741. Whatever the script commits, the claim's commit-tree included, is committed by `aio script <name>`.
        foreach (var (name, value) in CommitMark.Script(request.Script))
        {
            environment[name] = value;
        }

        var run = await ProcessRun.Capture(
            Cli.Wrap(script)
                .WithArguments(request.Arguments)
                .WithWorkingDirectory(request.WorkingDirectory)
                .WithEnvironmentVariables(environment),
            Timeout.InfiniteTimeSpan,
            ct
        );

        if (run.Missing)
        {
            return new ScriptResult(-1, null, $"{Path.GetRelativePath(request.WorkingDirectory, script)} could not be started: {run.NotStarted}");
        }

        var output = run.StandardError.Length == 0 ? run.StandardOutput : $"{run.StandardOutput}{run.StandardError}";

        return new ScriptResult(run.ExitCode, ScriptLine.Last(run.StandardOutput), output);
    }
}
