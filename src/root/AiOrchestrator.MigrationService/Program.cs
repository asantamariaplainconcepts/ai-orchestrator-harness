using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Persistence;
using AiOrchestrator.ServiceDefaults.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// The migration step as its own process, not a side effect of the Server starting. The AppHost
// runs it before the Server (WaitForCompletion), so a fresh clone — or a deleted data volume —
// boots with an up-to-date schema regardless of which environment name the Server happens to
// carry. An in-process migration gated on `!IsProduction()` guesses, and under `aspire run` the
// Server's environment silently defaults to Production: fresh database, no schema, 500s. A
// dedicated resource has no gate to guess wrong.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Same secret composition as the Server: the migrator must reach the connection string exactly
// the way the app does, or the two disagree about where configuration lives.
builder.AddSecretResolution();

// And the same default database, for the same reason: the process that creates the schema and the
// process that queries it must not be able to disagree about which file that is.
builder.AddLocalDatabase();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);

// The seed asks git which branch and remote the checkout it seeds is on, as AddFolder does.
builder.Services.AddFiles();

// Build without running: module registrations (DbContexts, options) become resolvable, but
// hosted services never start, because migrating must not also mean polling.
using var host = builder.Build();

// Every module migrates, and Workspace drops the tables nothing migrates any more (RetiredTables).
await host.Services.MigrateModules(modules);

// Reaching here means every module migrated; the process exits 0 and dependents may start.
// Any failure above throws, the process exits non-zero, and the Server deliberately never
// comes up — a missing schema surfacing at the first query would be strictly worse.
