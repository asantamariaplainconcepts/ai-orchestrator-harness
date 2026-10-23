using System.ComponentModel;
using System.Text;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// Runs one command line for one pipeline step, streams what it says into the step's log, and
/// throws when it exits non-zero. Everything a CI step here actually does goes through this.
/// <para>
/// Throwing is the contract, not a style choice: a step that swallows an exit code is a gate that
/// reports success on a broken build, which is the whole failure this folder exists to make
/// impossible. <c>aspire do</c> turns a thrown step into a non-zero exit for the process.
/// </para>
/// <para>
/// <b>The inherited <c>PATH</c> is used as-is, and that is a decision.</b> Orca does not: it spawns
/// <c>$SHELL -ilc</c> at start-up to recover the <c>PATH</c> a login shell would have built,
/// wrapped in sentinel delimiters, with ANSI stripped and a five-second budget, and it seeds
/// <c>PNPM_HOME</c> and half a dozen package-manager bins by hand — because a GUI-launched Electron
/// app inherits the launchd environment, in which pnpm simply is not on <c>PATH</c>. That is
/// correct for orca and wrong here. <c>aspire do ci</c> is typed into a terminal, so it inherits
/// the <c>PATH</c> of the person or agent who typed it, which is by definition the one where their
/// <c>dotnet</c> and <c>pnpm</c> live. Probing for a better one would mean this gate could run a
/// different pnpm than the developer standing next to it, and the failure it is meant to catch is
/// precisely the gate and the developer disagreeing. What is left is a worse error message, which
/// is why the start failure below names the executable and says "is it on PATH".
/// </para>
/// </summary>
static partial class StepCommand
{
    /// <summary>
    /// Runs <paramref name="fileName"/> in <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="arguments">
    /// One element per argument, never a joined string, so CliWrap escapes each one: a string
    /// joined by hand is re-split by rules that differ per platform, so the first path containing a
    /// space is a bug that only appears on somebody else's machine. Here the paths come from a
    /// checkout directory, which is exactly the kind of path that has a space in it.
    /// </param>
    /// <param name="throwOnFailure">
    /// False only where a non-zero exit is an answer rather than a failure — Stryker exiting non-zero
    /// is a project not measured — and the caller reads the returned code instead.
    /// </param>
    public static async Task<int> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? captureLine = null,
        bool throwOnFailure = true,
        IReadOnlyDictionary<string, string>? environment = null
    )
    {
        var commandLine = $"{fileName} {string.Join(' ', arguments)}";

        Starting(logger, commandLine, workingDirectory);

        // Both streams are logged as they arrive, line by line, rather than read to the end and
        // logged afterwards: a `dotnet test` that takes four minutes would otherwise print nothing
        // for four minutes, and nothing watching it could tell that from a hang. Their relative
        // order is not preserved — short of a pseudo-terminal there is none to have.
        //
        // Lines are unbounded, deliberately. Orca caps a plugin worker's unterminated output at
        // 8 KB because a plugin is somebody else's code and may never write a newline; `dotnet`
        // and `pnpm` are build tools that emit lines, and a cap here would truncate a stack trace,
        // which is the one thing in this output anybody reads twice.
        var command = Cli.Wrap(fileName)
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory)
            .WithEnvironmentVariables(
                (environment ?? new Dictionary<string, string>()).ToDictionary(
                    pair => pair.Key,
                    string? (pair) => pair.Value
                )
            )
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(
                PipeTarget.ToDelegate(
                    line =>
                    {
                        Line(logger, line);
                        // #336. The test step's capture: whatever reads stdout line by line already
                        // may hand the line to a second consumer without a second reader — the pipe
                        // is drained once, and the summary the gate consumes is assembled from what
                        // the process actually said.
                        captureLine?.Invoke(line);
                    },
                    Encoding.UTF8
                )
            )
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => ErrorLine(logger, line), Encoding.UTF8));

        CommandResult result;

        try
        {
            // Cancelling kills the tree, not the child. `pnpm` is a launcher for node and
            // `dotnet test` is a launcher for testhost, so signalling only what we started leaves
            // the process that is doing the work running with its parent gone. Orca pays for this
            // the hard way: it walks `ps` for descendants *before* signalling the root, because
            // "once the root dies, surviving descendants reparent to pid 1 and can no longer be
            // found by a ppid walk". CliWrap kills the whole tree on cancellation.
            result = await command.ExecuteAsync(cancellationToken);
        }
        catch (Win32Exception failure)
        {
            throw new InvalidOperationException(
                $"could not start `{commandLine}` — is {fileName} on PATH?",
                failure
            );
        }

        if (result.ExitCode is not 0 && throwOnFailure)
        {
            throw new InvalidOperationException(
                $"`{commandLine}` exited {result.ExitCode} (in {workingDirectory})"
            );
        }

        return result.ExitCode;
    }

    // Source-generated because CA1848 is a warning here and warnings are errors. The message is the
    // child's own line, forwarded verbatim: a step's log should read exactly like running the
    // command by hand, or the next person runs it by hand to find out what it really said.

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "$ {CommandLine}  ({WorkingDirectory})"
    )]
    static partial void Starting(ILogger logger, string commandLine, string workingDirectory);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information, Message = "{Line}")]
    static partial void Line(ILogger logger, string line);

    /// <summary>
    /// stderr, at warning rather than error, because <b>stderr is not an error</b>. A green
    /// <c>ci-build-frontend</c> printed four red lines the first time this ran — Vite's advice about
    /// chunks over 500 kB, which it writes to stderr as every well-behaved build tool does. A step
    /// that passes while printing errors teaches people that red means nothing here, and then the
    /// one that matters scrolls past. What a step failed is said once, by the exit code, in the
    /// exception below.
    /// </summary>
    [LoggerMessage(EventId = 9003, Level = LogLevel.Warning, Message = "{Line}")]
    static partial void ErrorLine(ILogger logger, string line);
}
