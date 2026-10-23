using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure.PbiProviders;

/// <summary>
/// The adapters of <see cref="IPbiProvider"/>: GitHub (#500), later Azure DevOps. Each is registered
/// behind <see cref="CachedPbiProvider"/> (#617), so its reads are kept a few seconds in a
/// <see cref="HybridCache"/> (#669) and forgotten on every write.
/// </summary>
public static class PbiProviderRegistration
{
    public static IServiceCollection AddPbiProviders(this IServiceCollection services)
    {
        // A proposal's files are one key, a path per file; the default 1024 characters would leave it unkept.
        services.AddHybridCache(options => options.MaximumKeyLength = 8192);
        return services.AddSingleton<IPbiProvider>(provider => new CachedPbiProvider(new GitHubPbiProvider(), provider.GetRequiredService<HybridCache>()));
    }
}
