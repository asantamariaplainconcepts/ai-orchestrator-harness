using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>
/// The <c>Environments:Sbx</c> section of the configuration, for <see cref="SbxEnvironment"/>. Each
/// value has the default the #613 probe recommends, so a machine with sbx needs none of them.
/// </summary>
/// <param name="Cli">The executable; <c>sbx</c> on the login shell's PATH unless a test names another.</param>
/// <param name="Permissions">The agents' mode in a sandbox: <c>yolo</c>, or lower.</param>
/// <param name="Allow">Hosts each sandbox may reach beyond sbx's <c>balanced</c> policy.</param>
/// <param name="Deny">Hosts <c>balanced</c> lets out that a run has no use for.</param>
sealed record SbxSettings(string Cli, AgentPermissions Permissions, IReadOnlyList<string> Allow, IReadOnlyList<string> Deny)
{
    public const string Section = "Environments:Sbx";

    /// <summary>
    /// What .NET restores from, which <c>balanced</c> answers with 403 (measured 11 October). The kit
    /// adds Claude's own hosts, and GitHub and npm are in <c>balanced</c> already.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultAllow = ["api.nuget.org", "*.nuget.org", "builds.dotnet.microsoft.com", "dotnetcli.azureedge.net"];

    /// <summary>Where an agent in <c>yolo</c> could post what it read, and <c>balanced</c> allows.</summary>
    public static readonly IReadOnlyList<string> DefaultDeny = ["gist.github.com", "raw.githubusercontent.com", "*.amazonaws.com", "*.blob.core.windows.net"];

    public static readonly SbxSettings Default = new("sbx", AgentPermissions.Yolo, DefaultAllow, DefaultDeny);

    /// <summary>The section as configured, each value missing or unreadable taken from <see cref="Default"/>.</summary>
    public static SbxSettings From(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(Section);
        if (section is null)
        {
            return Default;
        }

        return new(
            section["Cli"] is { Length: > 0 } cli ? cli : Default.Cli,
            Enum.TryParse<AgentPermissions>(section["Permissions"]?.Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out var mode)
                ? mode
                : Default.Permissions,
            Hosts(section.GetSection("Allow")) ?? Default.Allow,
            Hosts(section.GetSection("Deny")) ?? Default.Deny
        );
    }

    static IReadOnlyList<string>? Hosts(IConfigurationSection section)
    {
        var hosts = section.GetChildren().Select(child => child.Value?.Trim()).OfType<string>().Where(host => host.Length > 0).ToList();
        return section.Exists() ? hosts : null;
    }
}
