using AiOrchestrator.BuildingBlocks.Files;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// The adapters of what Workspace asks of a checkout beyond the five ports: its branch and remote
/// (<see cref="ICheckoutProbe"/>), the files of a PBI's branch (<see cref="IBranchFiles"/>, #514)
/// the editor (<see cref="IDesktopIntegration"/>, #515) and a branch of files proposed to origin
/// (<see cref="IBranchPublisher"/>, #566), the branch of evidence a run's recording is copied to
/// (<see cref="IEvidenceBranch"/>, #697), and what the requirements screen asks of the machine
/// (<see cref="IMachineProbe"/>, #680). Singletons, because none holds state
/// a request owns.
/// </summary>
public static class FilesRegistration
{
    public static IServiceCollection AddFiles(this IServiceCollection services) =>
        services
            .AddSingleton<GitCli>()
            .AddSingleton<ICheckoutProbe, GitCheckoutProbe>()
            .AddSingleton<IBranchFiles, GitBranchFiles>()
            .AddSingleton<IDesktopIntegration, DesktopIntegration>()
            .AddSingleton<IBranchPublisher, GitBranchPublisher>()
            .AddSingleton<IMachineProbe>(_ => new MachineProbe())
            .AddSingleton<IEvidenceBranch, GitEvidenceBranch>();
}
