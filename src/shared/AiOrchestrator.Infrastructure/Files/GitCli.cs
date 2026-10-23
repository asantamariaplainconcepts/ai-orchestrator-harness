using System.Text;
using CliWrap;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// Runs git and hands back what it said. The only place in the product that starts a git process.
/// <para>
/// Every invocation carries <c>-c core.quotePath=false</c>. Git's default is to <b>c-quote</b> any
/// path containing a non-ASCII or special byte — wrapping it in double quotes and escaping bytes as
/// octal — which means naive line parsing silently mangles exactly the filenames people complain
/// about. Turning it off at the invocation is strictly better than decoding it afterwards, and it
/// is available to us because we own the call site.
/// </para>
/// <para>
/// Where a command supports it, callers ask for <c>-z</c> as well. NUL-separated output cannot be
/// confused by a path containing a newline, which quoting alone does not solve.
/// </para>
/// </summary>
sealed class GitCli(TimeSpan timeout)
{
    /// <summary>
    /// Long enough for a cold `status` on a large repository, short enough that a hung git — a
    /// credential prompt on a network remote, most often — surfaces as a failure rather than a
    /// screen that never loads.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public GitCli()
        : this(DefaultTimeout) { }

    /// <summary>
    /// Runs git in <paramref name="workingDirectory"/>. Returns the result rather than throwing on
    /// a non-zero exit: "not a git repository" and "no such branch" are answers this product acts
    /// on, not exceptions.
    /// </summary>
    public Task<GitResult> Run(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    ) => RunCore(workingDirectory, arguments, null, null, cancellationToken);

    public Task<GitResult> RunBounded(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? standardInput,
        int maxOutputCharacters,
        CancellationToken cancellationToken = default
    ) => RunCore(workingDirectory, arguments, standardInput, maxOutputCharacters, cancellationToken);

    async Task<GitResult> RunCore(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? standardInput,
        int? maxOutputCharacters,
        CancellationToken cancellationToken
    )
    {
        var command = Cli.Wrap("git")
            .WithWorkingDirectory(workingDirectory)
            .WithArguments(["-c", "core.quotePath=false", .. arguments])
            .WithEnvironmentVariables(
                new Dictionary<string, string?>
                {
                    // Git reads the environment for pagers, editors and credential helpers. A pager
                    // would block forever with no terminal attached, and a credential prompt would
                    // do the same.
                    ["GIT_PAGER"] = "cat",
                    ["GIT_TERMINAL_PROMPT"] = "0",
                    ["GIT_OPTIONAL_LOCKS"] = "0",
                    // Callers read git's sentences ("couldn't find remote ref"), and git translates
                    // them into the machine's language: on a Spanish one the first push to
                    // aio/evidence read as a refusal (#762). The C locale is git's English.
                    ["LC_ALL"] = "C",
                    // The control registry runs against local bare repositories in temp
                    // directories. A host that sets safe.bareRepository=explicit would refuse
                    // exactly those, so the harness states its own policy rather than inheriting
                    // the operator's global one.
                    ["GIT_CONFIG_COUNT"] = "1",
                    ["GIT_CONFIG_KEY_0"] = "safe.bareRepository",
                    ["GIT_CONFIG_VALUE_0"] = "all",
                }
            );

        if (standardInput is not null)
        {
            command = command.WithStandardInputPipe(
                PipeSource.FromString(standardInput, new UTF8Encoding(false))
            );
        }

        var run = await ProcessRun.Capture(
            command,
            timeout,
            cancellationToken,
            maxOutputCharacters ?? int.MaxValue
        );

        // git is not installed, or not on PATH: a real answer for a screen to render.
        return run.NotStarted is { } reason ? new GitResult(-1, string.Empty, reason)
            : run.TimedOut ? new GitResult(-1, string.Empty, $"git did not answer within {timeout.TotalSeconds:0}s.")
            : run.Exceeded ? new GitResult(-1, string.Empty, "git output exceeded its bound.")
            : new GitResult(run.ExitCode, run.StandardOutput, run.StandardError);
    }
}

/// <summary>
/// 
/// </summary>
/// <param name="ExitCode">-1 when git could not be started or did not answer in time.</param>
sealed record GitResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Output split on NUL, with the trailing empty entry dropped.</summary>
    public string[] NulSeparated() =>
        StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    public string[] Lines() =>
        StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
