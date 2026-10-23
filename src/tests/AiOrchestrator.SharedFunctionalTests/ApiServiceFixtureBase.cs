using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace AiOrchestrator.SharedFunctionalTests;

/// <summary>
/// The shared functional-test host: the real Server driven through
/// <see cref="WebApplicationFactory{T}"/> against a real SQLite database file — no in-memory
/// substitutes, because a substitute proves nothing about the database the product runs on.
/// <para>
/// That is not a matter of taste here. The two worst bugs found in this codebase so far were both
/// invisible to anything but a real database with realistic volume: an owned collection being
/// eagerly loaded (which only shows up when a run has more than a couple of log lines), and a
/// whole-set reconciliation against an answer that might be incomplete. A fake would have passed
/// both. Since ADR-0003 the database the product runs on is a SQLite file, so that is what this
/// starts — which also removed the container, the port-mapping wait (#144) and Respawn: a temp
/// file needs none of them.
/// </para>
/// <para>
/// One database file per collection via <c>ICollectionFixture</c>. Between tests
/// <see cref="ResetDatabase"/> restores a clean slate by emptying every table except the
/// per-module migration histories — Respawn has no SQLite adapter, and with every module's tables in
/// one file, "everything but history" is exactly what its schema filter used to mean.
/// </para>
/// </summary>
public abstract class ApiServiceFixtureBase : WebApplicationFactory<Program>, IAsyncLifetime
{
    readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"aio-tests-{Guid.NewGuid():N}.db"
    );

    public string DatabaseConnectionString => $"Data Source={_databasePath}";

    public Task InitializeAsync()
    {
        // Boot the host, which migrates first (see MigrateFirst). The Server deliberately never
        // migrates itself — in the composed application that is the AppHost's `migrations`
        // resource — so this fixture, which owns its own database lifecycle, runs the same call.
        CreateClient().Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs every module's migrations before any other hosted service starts: the host starts them
    /// in registration order and awaits each one, and this is registered first. Until #640 the
    /// fixture migrated after the boot, and the boot recorded on 11 October had the loop's schedule
    /// asking <c>loop_settings</c> while the Loop migration was creating it; a test of Runs failed
    /// once in a gate with <c>no such table: pbi_runs</c>, under the same window.
    /// </summary>
    sealed class MigrateFirst(IServiceProvider services) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            services.MigrateModules(ModuleRegistration.Discover(), cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public virtual async Task ResetDatabase()
    {
        await using var connection = new SqliteConnection(DatabaseConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' "
                + "AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrationsHistory%'";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var table in tables)
        {
            await using var wipe = connection.CreateCommand();
            wipe.CommandText = $"DELETE FROM \"{table}\"";
            await wipe.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Runs work against the module's own DbContext, outside a request.</summary>
    public async Task With<TContext>(Func<TContext, Task> work)
        where TContext : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    /// <summary>
    /// A per-class host: one <c>WebApplicationFactory</c> derived from this fixture, substituted
    /// once with a single service graph, for every test in a class. The measurement that bought it
    /// (#388) put the boot at a ~2.4s floor per test, and 100 boots are 100 × that; a class whose
    /// tests each substitute the same singleton graph shares the boot.
    /// <para>
    /// One host per <typeparamref name="TClass"/> name — a second call with the same class returns
    /// the host the first call built. Tests whose substitution differs per case keep the fixture's
    /// <c>WithWebHostBuilder</c> per test; this is the shared path, not the only one. The substitute
    /// instances are one object per host: where a test needs per-test state inside a shared graph,
    /// the substitute itself resets in <c>IAsyncLifetime.InitializeAsync</c> alongside
    /// <see cref="ResetDatabase"/>, which already runs per test.
    /// </para>
    /// </summary>
    /// <typeparam name="TClass">The test class the host is for; its name keys the cache.</typeparam>
    /// <param name="configure">
    /// The class's substitutions, exactly as a per-test <c>WithWebHostBuilder</c> would write them.
    /// Called once, on the first request for the class.
    /// </param>
    public WebApplicationFactory<Program> ClassHost<TClass>(
        Action<IServiceCollection> configure
    )
        where TClass : class
    {
        var key = typeof(TClass).FullName!;
        return _classHosts.GetOrAdd(key, _ =>
            WithWebHostBuilder(builder => builder.ConfigureServices(configure)));
    }

    readonly ConcurrentDictionary<string, WebApplicationFactory<Program>> _classHosts = new();

    /// <summary>The SQL the host has issued. See <see cref="SqlRecorder"/> for why this exists.</summary>
    public SqlRecorder Sql { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:aiorchestratordb", DatabaseConnectionString);

        // The host serves the built SPA, never a Vite dev server. The variable that names one is
        // set by the AppHost on the running app, and a gate the loop starts inherits it: the host
        // then proxied every request with no endpoint to the live Vite, which answers 200 with
        // `no-cache` for any path, and SpaCacheTests failed twice in a row on #575 (#584).
        builder.UseSetting("services:frontend:http:0", "");

        // EF logs an executed command at Information and a transaction begin at Debug; the default
        // here is higher, so a test that asserts on either would otherwise be asserting on an
        // empty list.
        builder.UseSetting($"Logging:LogLevel:{SqlRecorder.Category}", "Information");
        builder.UseSetting($"Logging:LogLevel:{SqlRecorder.TransactionCategory}", "Debug");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(Sql);
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, MigrateFirst>());
        });

        // The one background loop left is the engine's schedule, and it is off unless
        // Loop:Enabled says otherwise (LoopSettings): a pass that fires mid-assertion is a test that
        // fails once a week for reasons nobody can reproduce. ScheduleTests turns it on on purpose.
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // The pool keeps the file handle after the host is gone; without this the deletes below
        // fail on Windows and silently leave the temp directory growing everywhere else. This
        // file's pool only: ClearAllPools disposed the connection under a query of a fixture still
        // running beside this one, and that query's request answered 500 (#710, measured on
        // Microsoft.Data.Sqlite 10.0.9 at 4 in 244,766 queries against a loop of ClearAllPools).
        using (var own = new SqliteConnection(DatabaseConnectionString))
        {
            SqliteConnection.ClearPool(own);
        }

        foreach (
            var file in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" }
        )
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        GC.SuppressFinalize(this);
    }
}
