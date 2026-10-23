using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.Modules.Workspace.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// A checkout probe the test drives, in place of git: the case worth driving is a directory git
/// cannot describe, and manufacturing that from real git means corrupting a repository.
/// </summary>
public sealed class StubProbe : ICheckoutProbe
{
    /// <summary>Null means "cannot be described".</summary>
    public CheckoutState? State { get; set; }

    public Task<CheckoutState?> ReadAsync(string path, CancellationToken ct) => Task.FromResult(State);
}

/// <summary>
/// The editor the test drives, in place of the machine's. <b>Registering this is not optional</b>:
/// the real one starts an editor on whoever's screen the suite happens to run on.
/// </summary>
public sealed class StubDesktop : IDesktopIntegration
{
    /// <summary>Every path handed to the editor, with the editor named (#514).</summary>
    public List<(string Path, string? Editor)> Edited { get; } = [];

    public Task<DesktopResult> OpenInEditor(
        string path,
        string? command = null,
        CancellationToken cancellationToken = default
    )
    {
        Edited.Add((path, command));
        return Task.FromResult(DesktopResult.Ok);
    }
}

public sealed class WorkspaceFixture : ApiServiceFixtureBase
{
    public StubProbe Probe { get; } = new();

    public StubDesktop Desktop { get; } = new();

    /// <summary>The remote every seeded folder carries, which is what names its vendor's repository.</summary>
    public string? RemoteUrl { get; set; } = "https://github.com/example/portal.git";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICheckoutProbe>();
            services.AddSingleton<ICheckoutProbe>(Probe);
            services.RemoveAll<IDesktopIntegration>();
            services.AddSingleton<IDesktopIntegration>(Desktop);
        });
    }

    /// <summary>The stubs reset alongside the database, so one test cannot leak into the next.</summary>
    public override async Task ResetDatabase()
    {
        Probe.State = null;
        Desktop.Edited.Clear();
        RemoteUrl = "https://github.com/example/portal.git";
        await base.ResetDatabase();
    }

    /// <summary>A folder row at <paramref name="path"/>, on <c>main</c>, with <see cref="RemoteUrl"/>.</summary>
    public async Task<Guid> Folder(string path)
    {
        var id = Guid.Empty;

        await With<WorkspaceDbContext>(async database =>
        {
            var folder = Domain.Folder.Create("repo", path, "claude-code");
            folder.RecordProbe("main", RemoteUrl);

            database.Folders.Add(folder);
            await database.SaveChangesAsync();
            id = folder.Id;
        });

        return id;
    }
}

[CollectionDefinition(Name)]
public sealed class WorkspaceCollection : ICollectionFixture<WorkspaceFixture>
{
    public const string Name = "workspace";
}
