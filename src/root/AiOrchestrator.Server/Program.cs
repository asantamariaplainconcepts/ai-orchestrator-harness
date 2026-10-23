using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Identity;
using AiOrchestrator.ServiceDefaults.Persistence;
using AiOrchestrator.ServiceDefaults.Secrets;
using AiOrchestrator.Server;
using AiOrchestrator.Server.Contracts;
using AiOrchestrator.Server.Installed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;

// #678. The .NET tool takes no arguments: `aio issue …` is the harness's command, not this app.
if (Installation.IsTool && Installation.Answer(args, Console.Out, Console.Error) is { } exitCode)
{
    return exitCode;
}

// #517. The installed app is this one process. A second launch only opens the browser; one of
// another version asks the running one to stop and takes its place (#703).
if (Installation.Prepare() is { } handedOver)
{
    return handedOver;
}

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = Installation.ContentRoot }
);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddServiceDefaults();

// Before the modules, because they are what reads the connection string. An orchestrator sets it
// on the resource and this call does nothing; started by a launcher, a dotnet tool or by hand,
// this is where the database file comes from (ADR-0003).
builder.AddLocalDatabase();

// Modules are found on disk, not listed here. Adding one is a new project and a solution entry;
// this file does not change, which is the property the arrangement exists to protect.
var modules = ModuleRegistration.Discover(ApiContract.IsBeingDescribed ? Path.GetDirectoryName(typeof(Program).Assembly.Location) : null);

builder.Services.AddModules(modules, builder.Configuration);
builder.Services.AddVsaCqsArchitecture(modules.Assemblies());

builder.AddSecretResolution();
builder.AddIdentity();

// #497. Every reach outside this process goes through the five ports, and each adapter registers
// in its own folder of Infrastructure, so this line does not change when one lands. What Workspace
// asks of a checkout beyond them — its branch and remote, a branch's files, the editor — is
// registered beside them.
builder.Services.AddPorts().AddFiles();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// #508. The API's document: every route under /api. `dotnet build` writes it to the frontend, which
// generates its contract from it — see ApiContract.cs.
builder.Services.AddApiContract();

// #679. Whether nuget.org has a newer aio, for the settings screen.
builder.Services.AddUpdates();

// The build runs this entry point to write that document, with a server that listens nowhere. It
// must not also start the watchers, the engine or the sweeps against the checkout's database.
if (ApiContract.IsBeingDescribed)
{
    builder.Services.RemoveAll<IHostedService>();
}

var app = builder.Build();

app.UseExceptionHandler();

// The terminal of a live run is a WebSocket (#508, AttachSession in Modules.Runs).
app.UseWebSockets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// #627. A request sent with `Cache-Control: no-cache` (the PBI screen's refresh button) reads the
// vendor again rather than the copy CachedPbiProvider keeps for a few seconds.
app.Use(
    async (context, next) =>
    {
        using var fresh = context.Request.Headers.CacheControl.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase)
            ? FreshVendorReads.Begin()
            : null;
        await next();
    }
);

app.MapDefaultEndpoints();
app.MapVersionEndpoint();
app.MapModules(modules);
app.MapUpdates();
app.MapStopConsole();
// #703. Start at login for Settings; the stop a newer launch asks for exists only in the tool.
app.MapStartAtLogin();
if (Installation.IsTool)
{
    app.MapAppStop();
}

// An /api route nobody mapped is a 404 here, in every habitat. Without it the development branch
// below handed it to the Vite dev server, whose proxy sends /api back to this host, and the request
// went round until something timed out; the installed branch answered it with index.html (#548).
app.MapFallback("/api/{**rest}", () => TypedResults.NotFound()).ExcludeFromDescription();

// Same origin in every habitat: the SPA is served by this host, so there is no API base URL to
// configure and no CORS. In the dev loop the Aspire-managed Vite server is proxied through;
// everywhere else the built bundle is on disk beside the binary.
var frontendDevServer = app.Configuration["services:frontend:http:0"];

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(frontendDevServer))
{
    app.UseWhen(
        context => context.GetEndpoint() is null,
        spaPipeline =>
            spaPipeline.UseSpa(spa => spa.UseProxyToSpaDevelopmentServer(frontendDevServer))
    );
}
else
{
    // A window held open across an install revalidates nothing unless told to: its document
    // carries the old hashes, the chunks it named are gone from disk, and the lazy import of one
    // 404s into a dead route. So the document always revalidates — `no-cache` makes the client
    // ask before trusting its copy, and a stale index can never stay authoritative. The hashed
    // assets go the other way: their hash *is* the freshness, so they are immutable and skip the
    // wire until the hash itself moves.
    //
    // The directive is set before the request goes further: setting it after the static files
    // have served is a no-op, because sending the file starts the response in the same call, and
    // a header written to a started response never reaches the client. Existence is checked
    // rather than asserted from the path alone, so the 404 an old chunk receives carries no
    // `immutable` to be obeyed.
    var webRoot = app.Services.GetRequiredService<IWebHostEnvironment>();

    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            var path = context.Request.Path.Value;

            if (path is { } p && p.StartsWith("/assets/", StringComparison.Ordinal))
            {
                // The web root is the SPA's `wwwroot`, so the file's name there drops only the
                // leading slash: `/assets/foo.js` is `assets/foo.js` on the provider. A chunk
                // the install has renamed is 404 with no directive — nothing to obey, and the
                // failure stays a clear one that `RouteErrorElement` can name.
                if (File.Exists(Path.Combine(webRoot.WebRootPath, p[1..])))
                {
                    context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                }
            }
            else
            {
                context.Response.Headers.CacheControl = "no-cache";
            }
        }

        await next();
    });

    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

// The installed app has no AppHost to run the MigrationService before it, so it migrates itself.
if (Installation.IsTool)
{
    await app.Services.MigrateModules(modules);
}

Installation.OpenBrowserWhenStarted(app);

await app.RunAsync();

// 0, or the exit an update's restart asks launchd or systemd to answer by starting it again (#703).
return SelfUpdate.ExitCode;

public partial class Program;
