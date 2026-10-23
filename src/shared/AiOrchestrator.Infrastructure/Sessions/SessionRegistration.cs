using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Infrastructure.Sessions;

/// <summary>
/// The adapters of <see cref="ISession"/>: the Unix PTY and Windows ConPTY with their asciicast
/// recording, both <see cref="PtySession"/> (#501). Transient because one instance is one session.
/// </summary>
public static class SessionRegistration
{
    public static IServiceCollection AddSessions(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services.AddTransient<ISession, PtySession>();
    }
}
