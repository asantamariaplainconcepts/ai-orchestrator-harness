using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.PbiProviders;
using AiOrchestrator.Infrastructure.Scripts;
using AiOrchestrator.Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure;

/// <summary>
/// Registers the adapters behind the five ports in <c>BuildingBlocks/Ports</c>. Each port's
/// adapters register in their own folder, so an issue that adds one edits its folder and not this
/// file. What Workspace needs of a checkout beyond them is <c>Files/FilesRegistration</c>.
/// </summary>
public static class Ports
{
    public static IServiceCollection AddPorts(this IServiceCollection services) =>
        services.AddPbiProviders().AddAgentProviders().AddEnvironments().AddSessions().AddScripts();
}
