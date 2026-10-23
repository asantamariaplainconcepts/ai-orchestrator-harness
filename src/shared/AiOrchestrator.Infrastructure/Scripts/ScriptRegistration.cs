using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure.Scripts;

/// <summary>The adapter of <see cref="IScriptRunner"/>: the local runner of the harness's scripts (#502).</summary>
public static class ScriptRegistration
{
    public static IServiceCollection AddScripts(this IServiceCollection services) =>
        services.AddSingleton<IScriptRunner, LocalScriptRunner>();
}
