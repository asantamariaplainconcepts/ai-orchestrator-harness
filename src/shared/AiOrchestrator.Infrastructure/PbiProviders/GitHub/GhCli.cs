using CliWrap;

namespace AiOrchestrator.Infrastructure.PbiProviders.GitHub;

/// <summary>
/// One question put to the GitHub CLI. A delegate so the adapter's tests can answer in its place;
/// the product only ever gets <see cref="GhCli.Run"/>.
/// </summary>
delegate Task<ProcessOutcome> Gh(IReadOnlyList<string> arguments, CancellationToken ct);

/// <summary>
/// One answer of the GitHub CLI read as bytes rather than text, for an image: null when <c>gh</c>
/// did not answer with them. A delegate for the same reason as <see cref="Gh"/>.
/// </summary>
delegate Task<byte[]?> GhBytes(IReadOnlyList<string> arguments, CancellationToken ct);

/// <summary>
/// <c>gh</c> with the spawn discipline of <c>GhPullRequestLookup</c>, transplanted: the login
/// shell's PATH, no pager, no prompt, one language, and a deadline. The credential is the one
/// <c>gh</c> already holds; this process never sees it.
/// </summary>
static class GhCli
{
    /// <summary>
    /// Long enough for a cold network call, short enough that a credential prompt nobody can see is
    /// reported as a timeout instead of a loop that waits forever.
    /// </summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// An upload of attachments (<c>--attach</c>, #512) sends whole images before it answers, which a
    /// 20-second deadline would cut for a few screenshots on a slow link.
    /// </summary>
    internal static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(3);

    public static Task<ProcessOutcome> Run(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        // Without the login shell's PATH, `gh` is not found when the app was not started from a
        // terminal. A pager or a prompt would block with nobody to answer it. C.UTF-8 because the
        // adapter reads a few of gh's sentences, and a machine set to Spanish words them differently.
        var environment = LoginShellEnvironment.PathOverride();
        environment["GH_PAGER"] = "cat";
        environment["GH_PROMPT_DISABLED"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["LANG"] = "C.UTF-8";
        environment["LC_ALL"] = "C.UTF-8";

        return ProcessRun.Capture(
            Cli.Wrap("gh").WithArguments(arguments).WithEnvironmentVariables(environment),
            arguments.Contains("--attach") ? UploadTimeout : Timeout,
            ct
        );
    }

    /// <summary>The most an image of a comment may weigh: the 10 MiB a capture of the evidence may (#418).</summary>
    internal const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// <see cref="Run"/> for an answer that is not text. Bounded by <see cref="MaxBytes"/>: an answer
    /// over it is no answer, rather than an image cut short. #513.
    /// </summary>
    public static async Task<byte[]?> ReadBytes(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var environment = LoginShellEnvironment.PathOverride();
        environment["GH_PAGER"] = "cat";
        environment["GH_PROMPT_DISABLED"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(Timeout);
        using var output = new MemoryStream();

        try
        {
            var result = await Cli.Wrap("gh")
                .WithArguments(arguments)
                .WithEnvironmentVariables(environment)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStream(output))
                .ExecuteAsync(deadline.Token);

            return result.ExitCode == 0 && output.Length is > 0 and <= MaxBytes ? output.ToArray() : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
