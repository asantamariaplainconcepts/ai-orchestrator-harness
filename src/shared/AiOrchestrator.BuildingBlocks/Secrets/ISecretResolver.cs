namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// Resolves a secret value from its name, <b>per read</b>.
/// <para>
/// Per read is the whole point: Connector secrets are created while the application is running,
/// so anything that snapshots the set of secrets at startup cannot see one an Admin adds
/// afterwards — and the failure would read as "credential missing" rather than "process is
/// stale". It also means a rotated value is picked up with no restart and no cache to invalidate.
/// </para>
/// <para>
/// Implementations are registered by the <b>host</b>, never by a module: modules receive only
/// <c>IServiceCollection</c>/<c>IConfiguration</c> and must stay free of any secret-store SDK.
/// </para>
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// The value stored under <paramref name="secretName"/>.
    /// Throws <see cref="SecretNotFoundException"/> when it is absent — never returns null,
    /// empty, or a default, because a silently empty credential fails far from its cause.
    /// </summary>
    Task<string> Resolve(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a named secret does not exist. Carries the name so the fix is obvious.</summary>
public sealed class SecretNotFoundException : Exception
{
    public SecretNotFoundException(string secretName)
        : this(secretName, "Add it to the configured secret store.") { }

    /// <summary>
    /// The same, with a <paramref name="remedy"/> the resolver knows and the default sentence does
    /// not — the exact <c>security add-generic-password</c> line, say.
    /// <para>
    /// It exists because the alternative is folding the remedy into the name, and that is not a
    /// hypothetical: <c>KeychainSecretResolver</c> passed three whole sentences to the
    /// single-argument constructor above, which rendered every keychain failure as
    /// <i>"No secret named 'Unlock it and try again.' was found"</i> and left
    /// <see cref="SecretName"/> holding a paragraph.
    /// </para>
    /// </summary>
    public SecretNotFoundException(string secretName, string remedy)
        : base($"No secret named '{secretName}' was found. {remedy}") => SecretName = secretName;

    public SecretNotFoundException() { }

    public SecretNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }

    public string? SecretName { get; }
}
