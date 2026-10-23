using AiOrchestrator.AppHost.CISteps;

var builder = DistributedApplication.CreateBuilder(args);

// The gate in one place, so `aspire do ci` is the whole of it. On 15 August an agent derived it from
// prose and ran `dotnet test` without the solution: a subset, reported as 118 tests when the suite
// was 250. `CISteps/` is a vendored, adapted copy of andonisan/aspire-pipelines; each departure is
// explained in its file.
var solution = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "AiOrchestrator.slnx")
);

// The dev database is a SQLite file inside the checkout, not a container on the machine (ADR-0003):
// two checkouts are two files, nothing outlives the AppHost, and deleting a worktree deletes its
// database, which is what #52's per-worktree Docker volumes approximated.
var databaseConnectionString = $"Data Source={DevDatabase.ForAppHostAt(builder.AppHostDirectory)}";

// Migrations are a resource, not a side effect of the Server starting. The Server waits for this
// to exit 0, so a fresh clone or a deleted database file boots with an up-to-date schema.
var migrations = builder
    .AddProject<Projects.AiOrchestrator_MigrationService>("migrations")
    .WithEnvironment("ConnectionStrings__aiorchestratordb", databaseConnectionString);

var server = builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithEnvironment("ConnectionStrings__aiorchestratordb", databaseConnectionString)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints()
    // The .NET gates, hanging off the project that is the .NET side of this application. Each names
    // the solution, so `dotnet test` cannot be run here without one.
    .WithRestoreStep(solution)
    .WithBuildStep(solution)
    .WithTestStep(solution)
    // What the suite exercised — artifacts/coverage/index.html, CRAP per method in crap.tsv — and,
    // beside it rather than after it, whether the tests would notice the changed files being wrong.
    .WithToolRestoreStep(solution)
    .WithCoverageStep(solution)
    .WithMutationStep(solution);

// The Vite dev server. It is declared in both modes because `aspire do` runs the pipeline in publish
// mode and the frontend's half of `ci` hangs off it; outside run mode it is excluded from the
// manifest, since a published build serves the bundle from disk.
var frontend = builder.AddViteApp("frontend", "../../frontend").WithPnpm();

// `dotnet build` writes the API's OpenAPI document into the frontend, and `typecheck` and `build`
// generate the contract from it, so both wait for the server's build: a DTO changed in this run is
// checked against the frontend in this run (#508).
var serverBuild = WellKnownCIStepNames.For("build", server.Resource.Name);

frontend
    .WithInstallStep()
    .WithScriptStep("typecheck", WellKnownCIStepNames.Lint, after: serverBuild)
    .WithScriptStep("lint", WellKnownCIStepNames.Lint)
    .WithScriptStep("build", WellKnownCIStepNames.Compile, after: serverBuild);

if (builder.ExecutionContext.IsRunMode)
{
    // Development and the dev-server reference are one decision: `Program.cs` proxies the SPA to Vite
    // only when the environment is Development *and* `services:frontend:http:0` is set, so both are
    // declared here, on the resource.
    //
    // **Not inherited from this process.** `aspire start --isolated` does not read this AppHost's
    // launch profile, so the Server came up in **Production** and answered `GET /` with **404, 0
    // bytes** from a `wwwroot` a fresh worktree does not have; a variable set in the shell does not
    // reach it either, because the Server is the AppHost's child. **Not the Server's launch profile
    // either:** Aspire starts it with `--no-launch-profile` (read off the resource's log), so that
    // file is never opened here.
    //
    // The quiet failure is worse: any checkout that ran `aspire do ci` has a `Server/wwwroot`, and
    // the Server serves that old bundle as if nothing were wrong. Measured on 15 August: a string
    // changed in `en.ts`, the server restarted, and `GET /` answered 200 with the old asset. Three
    // browser tests in the E2E suite fail if this line goes.
    //
    // Run mode only: a published manifest carrying `Development` would be the same bug the other way.
    server.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development").WithReference(frontend);

    // Where Vite's own `/api` proxy sends requests, from the composition that allocated the ports. A
    // hard-coded `http://localhost:5080` is not the Server's port under `--isolated`, and with a
    // second worktree running it is the **other worktree's application**: whoever opened the Vite
    // origin to get past the 404 above was served their neighbour's data.
    frontend.WithEnvironment("API_PROXY_TARGET", server.GetEndpoint("http"));
}
else
{
    frontend.ExcludeFromManifest();
}

builder.WithCiSteps();

builder.Build().Run();

/// <summary>
/// Where this checkout's SQLite database file lives. Drawn from the path this process runs out of,
/// so two checkouts never share a database.
/// <para>
/// Composition may read its own location; it may not call into the product. So nothing here asks
/// <c>GitWorktreeManager</c>, which lives in Infrastructure and belongs to the modules — the
/// AppHost depending on a module to decide how to compose the modules is the cycle that makes an
/// AppHost impossible to reason about.
/// </para>
/// </summary>
file static class DevDatabase
{
    internal static string ForAppHostAt(string appHostDirectory)
    {
        // Inside the checkout, so deleting the worktree deletes its database. Under `src/artifacts/`,
        // beside the coverage and test results the gate already writes there and ignored by the same
        // `artifacts/` line; `.aio/` holds only what a person reads and edits (#757).
        var databaseDirectory = Path.Combine(
            RepositoryRootAbove(appHostDirectory),
            "src",
            "artifacts",
            "data"
        );

        // Created at composition time because the migration resource is the first to open the
        // file, and Microsoft.Data.Sqlite creates a missing file but not a missing directory.
        Directory.CreateDirectory(databaseDirectory);

        return Path.Combine(databaseDirectory, "aiorchestrator.db");
    }

    static string RepositoryRootAbove(string appHostDirectory)
    {
        for (
            var directory = new DirectoryInfo(appHostDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            // A main checkout keeps `.git` as a directory; a linked worktree keeps it as a file
            // holding one `gitdir:` line. Either way that directory is the checkout root. Two stats
            // and no git process, because anything slow or failable at composition time takes the
            // whole application down with it.
            var git = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(git) || File.Exists(git))
            {
                // Resolved, because on macOS a checkout reached through a symlink (`/var` is
                // `/private/var`) is the same checkout at two spellings, and two spellings must
                // not become two databases. One syscall removes the class.
                return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? directory.FullName;
            }
        }

        // Not a checkout at all: a source copy, or an unpacked release. The AppHost's own layout
        // is still known — src/root/AiOrchestrator.AppHost — so the database lands where the
        // repository root would be, rather than guessing at something shared.
        return Path.GetFullPath(Path.Combine(appHostDirectory, "..", "..", ".."));
    }
}
