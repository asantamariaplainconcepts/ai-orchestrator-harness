using System.ComponentModel;
using AiOrchestrator.BuildingBlocks.Secrets;
using CliWrap;
using CliWrap.Buffered;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Borrows the credential the <c>gh</c> CLI is already holding, by host.
/// <para>
/// This exists so that reading your own repositories costs nobody a personal access token. A
/// developer who has run <c>gh auth login</c> — which is every developer who uses the CLI — has a
/// working credential in their OS keyring already. Asking them to mint a second one, paste it into
/// a keychain item, and then rotate it by hand is asking them to duplicate state the machine is
/// maintaining correctly on its own.
/// </para>
/// <para>
/// It is a sibling of <see cref="KeychainSecretResolver"/> and works the same way: it claims names
/// beginning with <see cref="Prefix"/> and passes everything else down. The two are composed, not
/// chosen between, so one deployment can name a <c>gh:</c> credential for GitHub and a
/// <c>keychain:</c> one for Jira without a second resolver interface.
/// </para>
/// <para>
/// <b>Why shell out rather than read the keyring directly.</b> Where <c>gh</c> keeps its token is
/// its own business and it has moved — a keyring item on some machines, <c>hosts.yml</c> on others,
/// <c>GH_TOKEN</c> in CI. <c>gh auth token</c> is the supported way to ask, it answers correctly in
/// all three cases, and it is the same command a person would run to check what this product can
/// see. Reading the file ourselves would be faster in a way nothing here measures and wrong the
/// first time the CLI changed where it stores things.
/// </para>
/// <para>
/// <b>The value is never logged, never written, and never returned anywhere but to the caller.</b>
/// There is no store counterpart, deliberately: this resolver borrows a credential somebody else
/// owns, and a product that could write to <c>gh</c>'s store is a product that could be made to
/// clobber a login the whole machine depends on.
/// </para>
/// </summary>
sealed class GitHubCliSecretResolver(ISecretResolver fallback) : ISecretResolver
{
    /// <summary>
    /// Names beginning with this are <c>gh</c> lookups. What follows is the <b>host</b>, so
    /// <c>gh:</c> means github.com and <c>gh:ghe.example.com</c> means an Enterprise Server — the
    /// same axis <c>gh auth login --hostname</c> uses, because a person with both logged in needs
    /// to say which one they mean.
    /// </summary>
    public const string Prefix = "gh:";

    /// <summary>What an empty host after the prefix means. The overwhelmingly common case.</summary>
    public const string DefaultHost = "github.com";

    /// <summary>
    /// Shorter than the keychain's fifteen. Nothing here can raise a modal dialog — <c>gh</c>
    /// either has a token or does not — so a slow answer means the binary is wedged, not that
    /// somebody is being asked to authorise something.
    /// </summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<string> Resolve(
        string secretName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        if (!secretName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return await fallback.Resolve(secretName, cancellationToken);
        }

        var host = secretName[Prefix.Length..].Trim();

        if (host.Length == 0)
        {
            host = DefaultHost;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);

        BufferedCommandResult run;

        try
        {
            run = await Cli.Wrap("gh")
                .WithArguments(["auth", "token", "--hostname", host])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(deadline.Token);
        }
        catch (Win32Exception)
        {
            // Its own failure, with its own remedy: a missing `gh` is not a missing login, and
            // telling somebody to run `gh auth login` when the binary is not installed sends them
            // to a command they cannot run.
            throw new SecretNotFoundException(
                secretName,
                "The GitHub CLI is not on PATH. Install it (https://cli.github.com) and run "
                    + $"'gh auth login --hostname {host}', or name a different credential."
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SecretNotFoundException(
                secretName,
                $"'gh auth token' did not answer for {host} within {Timeout.TotalSeconds:0} "
                    + "seconds. Check that the CLI runs from a terminal."
            );
        }

        if (run.ExitCode != 0)
        {
            // What gh said, not what we assume it meant. "not logged into any GitHub hosts" and
            // "no token found for <host>" need different fixes, and only gh knows which it is.
            throw new SecretNotFoundException(
                secretName,
                $"{Describe(run.StandardError)} {Remedy(host)}"
            );
        }

        var value = run.StandardOutput.TrimEnd('\n', '\r');

        return value.Length == 0
            ? throw new SecretNotFoundException(
                secretName,
                $"'gh auth token' returned nothing for {host}. {Remedy(host)}"
            )
            : value;
    }

    static string Remedy(string host) => $"Run: gh auth login --hostname {host}";

    /// <summary>
    /// One line of what <c>gh</c> wrote — a whole stderr dump in an exception message is
    /// unreadable, and the first line is the one that names the cause.
    /// </summary>
    static string Describe(string diagnostics)
    {
        var first = diagnostics
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrEmpty(first) ? "The GitHub CLI has no token for that host." : first;
    }
}
