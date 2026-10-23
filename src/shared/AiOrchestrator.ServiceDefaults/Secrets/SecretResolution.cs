using AiOrchestrator.BuildingBlocks.Secrets;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Composes secret resolution for a host, choosing by configuration rather than by environment
/// name. A store directory is configured or it is not; there is nothing to infer.
/// <para>
/// This lives in ServiceDefaults rather than in each host so the Server and the MigrationService
/// cannot drift apart on how they reach secrets. It is an <c>IHostApplicationBuilder</c>
/// extension, which is precisely what a module structurally cannot call — that is what keeps
/// modules free of any secret-store SDK.
/// </para>
/// <para>
/// Two habitats today, neither of them a cloud vault. Values arrive as configuration — user
/// secrets in the dev loop, environment variables elsewhere — or they live in a protected
/// directory this process encrypts. The OS keychain is the intended third, and it arrives as one
/// more implementation registered here and nowhere else.
/// </para>
/// </summary>
public static class SecretResolution
{
    public static TBuilder AddSecretResolution<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var directory = builder.Configuration[ProtectedFileSecretStore.DirectoryKey];

        if (string.IsNullOrWhiteSpace(directory))
        {
            builder.Services.AddSingleton<ISecretResolver>(provider => Borrowed(
                new ConfigurationSecretResolver(provider.GetRequiredService<IConfiguration>())
            ));
            builder.Services.AddSingleton<ISecretStore>(
                new UnavailableSecretStore(
                    $"Set {ProtectedFileSecretStore.DirectoryKey} to store values here, or add "
                        + $"{ConfigurationSecretResolver.SectionName}__<name> to the environment "
                        + "and name that secret on the connector."
                )
            );
            return builder;
        }

        // The key ring lives apart from the values on purpose: one directory holding both is
        // obfuscation, not encryption. Defaulting it beside the store would quietly undo that,
        // so an unset key-ring path is a refusal to start, not a default.
        var keyRing = builder.Configuration[ProtectedFileSecretStore.KeyRingKey];
        if (string.IsNullOrWhiteSpace(keyRing))
        {
            throw new InvalidOperationException(
                $"{ProtectedFileSecretStore.DirectoryKey} is set without "
                    + $"{ProtectedFileSecretStore.KeyRingKey}. Protected values and the key that "
                    + "protects them must be persisted separately, or a single stolen volume "
                    + "yields both. Mount a second location and name it."
            );
        }

        builder
            .Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRing))
            .SetApplicationName(DataProtectionApplicationName);

        builder.Services.AddSingleton<ISecretResolver>(provider => Borrowed(
            new ProtectedFileSecretResolver(
                Protector(provider),
                directory,
                provider.GetRequiredService<IConfiguration>()
            )
        ));

        builder.Services.AddSingleton<ISecretStore>(provider => new ProtectedFileSecretStore(
            Protector(provider),
            directory
        ));

        return builder;
    }

    /// <summary>
    /// Wraps any resolver with the two that borrow a credential the machine already holds, each
    /// claiming its own prefix and passing everything else down: <c>gh:</c> for a token the GitHub
    /// CLI is logged in with, <c>keychain:</c> for an item in the OS keychain.
    /// <para>
    /// Composed here rather than registered separately so there is exactly one
    /// <c>ISecretResolver</c> and no call site has to choose between three. The prefixes are
    /// disjoint, so the nesting order decides nothing; <c>gh:</c> is outermost only because it is
    /// the one that costs a person no setup at all.
    /// </para>
    /// <para>
    /// The keychain layer stays macOS-only because <c>security</c> is. The <c>gh</c> layer does
    /// not: the CLI runs everywhere, and a resolver that claimed <c>gh:</c> on a Mac and silently
    /// fell through to configuration on Linux would turn one deployment's working credential into
    /// another's "secret not found".
    /// </para>
    /// </summary>
    static ISecretResolver Borrowed(ISecretResolver inner) =>
        new GitHubCliSecretResolver(Keychain(inner));

    static ISecretResolver Keychain(ISecretResolver inner) =>
        OperatingSystem.IsMacOS() ? new KeychainSecretResolver(inner) : inner;

    /// <summary>
    /// Fixed, because Data Protection derives keys from it: letting it default to the entry
    /// assembly's name would make the Server unable to read what the migration host stored, and
    /// the failure would look like a rejected credential rather than a naming mismatch.
    /// </summary>
    const string DataProtectionApplicationName = "ai-orchestrator";

    const string ProtectorPurpose = "AiOrchestrator.Secrets.v1";

    static IDataProtector Protector(IServiceProvider provider) =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(ProtectorPurpose);
}
