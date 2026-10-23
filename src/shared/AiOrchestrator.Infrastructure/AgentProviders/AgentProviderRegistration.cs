using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>The adapters of <see cref="IAgentProvider"/>: Claude Code (#503), OpenCode and Copilot.</summary>
public static class AgentProviderRegistration
{
    /// <summary>
    /// The executable the claude-code provider spawns, when it is not <c>claude</c> on the PATH: an
    /// install outside it, or a stand-in that commits, pushes and calls <c>aio done</c> without a
    /// model, which is how #507 drove a PBI through the loop end to end.
    /// </summary>
    public const string ClaudeCodeCliSetting = "Agents:ClaudeCode:Cli";

    public static IServiceCollection AddAgentProviders(this IServiceCollection services) =>
        services
            .AddSingleton<IAgentProvider>(provider => ClaudeCode(provider.GetService<IConfiguration>()))
            .AddSingleton<IAgentProvider>(_ => new OpenCodeProvider())
            .AddSingleton<IAgentProvider>(_ => new CopilotProvider());

    internal static ClaudeCodeProvider ClaudeCode(IConfiguration? configuration, string? configFile = null, string? transcripts = null) =>
        configuration?[ClaudeCodeCliSetting] is { Length: > 0 } cli
            ? new ClaudeCodeProvider(cli, configFile, transcripts)
            : new ClaudeCodeProvider(configFile: configFile, transcripts: transcripts);
}
