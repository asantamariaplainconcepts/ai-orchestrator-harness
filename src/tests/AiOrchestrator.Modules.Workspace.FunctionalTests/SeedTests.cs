using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #548. What Workspace's migration does to a database file, start after start: it seeds this
/// checkout on the first one only, and it drops the tables nothing migrates any more on every one.
/// Each start is <see cref="WorkspaceModule.Migrate"/> on a fresh service provider over the same
/// file, the call the MigrationService and the installed app both make.
/// </summary>
public sealed class SeedTests : IAsyncLifetime
{
    readonly string _file = Path.Combine(Path.GetTempPath(), $"aio-seed-{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        // This file's pool only: ClearAllPools would close a connection another test is using (#710).
        using (var own = new SqliteConnection($"Data Source={_file}"))
        {
            SqliteConnection.ClearPool(own);
        }

        foreach (var file in new[] { _file, $"{_file}-wal", $"{_file}-shm" })
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_folder_removed_after_the_first_start_does_not_come_back_on_the_next()
    {
        var checkout = SelfCheckout.Root();
        checkout.ShouldNotBeNull("the suite runs from a checkout, which is what the seed adds");

        await Start();
        (await Folders()).ShouldBe([checkout]);

        await Use(async database =>
        {
            database.Folders.RemoveRange(database.Folders);
            await database.SaveChangesAsync();
        });

        await Start();
        (await Folders()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_start_drops_a_table_that_nothing_migrates_any_more()
    {
        await Start();
        await Sql("CREATE TABLE \"tasks\" (\"id\" INTEGER)");

        await Start();

        (await Sql("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'tasks'")).ShouldBe(0L);
    }

    async Task Start()
    {
        await using var services = Services();
        await new WorkspaceModule().Migrate(services, CancellationToken.None);
    }

    async Task<List<string>> Folders()
    {
        var paths = new List<string>();
        await Use(async database => paths.AddRange(await database.Folders.Select(folder => folder.Path).ToListAsync()));
        return paths;
    }

    async Task Use(Func<WorkspaceDbContext, Task> work)
    {
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<WorkspaceDbContext>());
    }

    async Task<object?> Sql(string text)
    {
        await using var connection = new SqliteConnection($"Data Source={_file}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        return await command.ExecuteScalarAsync();
    }

    ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{WorkspaceModule.ConnectionStringName}"] = $"Data Source={_file}",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        new WorkspaceModule().Add(services, configuration);
        return services.BuildServiceProvider();
    }
}
