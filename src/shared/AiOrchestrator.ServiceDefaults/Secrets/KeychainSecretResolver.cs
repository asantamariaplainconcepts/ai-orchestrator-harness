using AiOrchestrator.BuildingBlocks.Secrets;
using CliWrap;
using CliWrap.Buffered;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Reads credentials from the macOS keychain, by name.
/// <para>
/// The Settings screen already promises this: "the credential is named, never pasted — the value
/// stays in your OS keychain." This is what makes that true rather than aspirational.
/// </para>
/// <para>
/// It shells out to <c>security</c> rather than binding the Security framework. The CLI is stable,
/// present on every macOS, and — importantly — it is the same command a person would run to check
/// what this product can see. A P/Invoke binding would be faster in a way nothing here measures,
/// and opaque in a way that matters when somebody is debugging a credential.
/// </para>
/// <para>
/// There is a second, larger reason, and it is the one that survives packaging (H4). A keychain
/// item created from a terminal carries <c>/usr/bin/security</c> in its access-control list, so
/// reading it through that binary is silent — measured at 95 ms, no dialog. The same read from a
/// P/Invoke binding is a *different* application asking, which is the modal
/// "wants to use your confidential information" prompt, once per launch of an unsigned build and
/// again after every re-sign. Bundling this app is what would have discovered that, and by then
/// the seam would have been the hard thing to change.
/// </para>
/// <para>
/// <b>The value is never logged, never returned anywhere but to the caller, and never written.</b>
/// There is no store counterpart here on purpose: adding a credential is something a person does
/// in Keychain Access or with <c>security add-generic-password</c>, and a product that can write
/// to the keychain is a product that can be made to overwrite one.
/// </para>
/// </summary>
sealed class KeychainSecretResolver(ISecretResolver fallback) : ISecretResolver
{
    /// <summary>
    /// Names beginning with this are keychain lookups; anything else falls through to
    /// configuration. That is what lets one deployment mix a keychain credential and an
    /// environment variable without a second resolver interface.
    /// </summary>
    public const string Prefix = "keychain:";

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<string> Resolve(
        string secretName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        if (!OperatingSystem.IsMacOS() || !secretName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return await fallback.Resolve(secretName, cancellationToken);
        }

        var account = secretName[Prefix.Length..].Trim();

        if (account.Length == 0)
        {
            throw new SecretNotFoundException(
                secretName,
                $"'{Prefix}' on its own names nothing. Write {Prefix}<item> and give that item's "
                    + "service name."
            );
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);

        BufferedCommandResult run;

        try
        {
            // `-w` prints the password and nothing else, so nothing has to be parsed out of a dump
            // that also contains the item's attributes.
            run = await Cli.Wrap("security")
                .WithArguments(["find-generic-password", "-s", account, "-w"])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The keychain is locked and macOS is showing an unlock dialog nobody is looking at.
            // Reported as absent rather than hung, with the remedy in the sentence.
            //
            // The deadline kills it, and that is the point. Abandoning the process leaves
            // `security` sitting on its dialog forever, and connectors are polled on a schedule
            // (D5) — so one locked keychain became one stranded process and one more modal dialog
            // per poll, until the screen was a stack of them. Confirmed by leaving a stub
            // `security` running past the deadline: the process outlived the resolver until it was
            // killed by hand.
            throw new SecretNotFoundException(
                secretName,
                $"The keychain did not answer for '{account}' within {Timeout.TotalSeconds:0} "
                    + "seconds. Unlock it and try again."
            );
        }

        if (run.ExitCode != 0)
        {
            // What `security` said, not what we assume it meant. Exit 44 really is "no such item",
            // but the same non-zero exit also covers a keychain that is not in the search list and
            // a user who cancelled the prompt, and those need different fixes.
            throw new SecretNotFoundException(
                secretName,
                $"{Describe(run.StandardError)} {Remedy(account)}"
            );
        }

        // Trailing newline only — the value itself is returned exactly as stored, because a token
        // with meaningful whitespace is a token this must not silently alter.
        var value = run.StandardOutput.TrimEnd('\n', '\r');

        return value.Length == 0
            ? throw new SecretNotFoundException(
                secretName,
                $"The keychain item '{account}' exists but holds an empty value. {Remedy(account)}"
            )
            : value;
    }

    static string Remedy(string account) =>
        $"Add one with: security add-generic-password -s {account} -a $USER -w";

    /// <summary>
    /// One line of what <c>security</c> wrote, with its own prefix stripped — a whole stderr dump
    /// in an exception message is unreadable, and the first line is the one that names the cause.
    /// </summary>
    static string Describe(string diagnostics)
    {
        var first = diagnostics
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrEmpty(first)
            ? "The keychain has no such item."
            : first.StartsWith("security:", StringComparison.Ordinal)
                ? first["security:".Length..].Trim()
                : first;
    }
}
