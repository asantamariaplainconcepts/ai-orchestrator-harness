using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>The adapters of <see cref="IEnvironment"/>: <c>worktree</c> (#503) and <c>sbx</c> (#614), later ACA.</summary>
public static class EnvironmentRegistration
{
    public static IServiceCollection AddEnvironments(this IServiceCollection services) =>
        services
            .AddSingleton<IEnvironment, WorktreeEnvironment>()
            .AddSingleton<IEnvironment>(provider => new SbxEnvironment(
                new WorktreeEnvironment(),
                SbxSettings.From(provider.GetService<IConfiguration>()),
                () => ControlPlaneAddress.Of(provider.GetService<IServer>())
            ));
}
