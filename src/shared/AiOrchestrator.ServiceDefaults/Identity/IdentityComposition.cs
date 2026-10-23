using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Identity;

/// <summary>
/// Composes who the caller is. Two habitats, and <b>the deployment says which</b> — it is never
/// inferred from the address the host happens to be listening on.
/// <para>
/// By default this product runs on the machine in front of you and there is exactly one answer:
/// its owner, holding everything, with no provider, no sign-in and no users table. Declaring
/// <see cref="ReachKey"/> as <c>shared</c> swaps that for the roster in
/// <see cref="PeopleRoster"/> and a credential on every request. Nothing above this file changes
/// either way — the authorization decorator asks <see cref="ICurrentPrincipal"/> and
/// <see cref="IProjectPermissions"/>, and gets whichever this composed.
/// </para>
/// <para>
/// <b>Why a declaration rather than a look at the bind address.</b> Orca learned this one the
/// expensive way and wrote it down in <c>src/shared/runtime-pairing-reach.ts</c>: widening a
/// listener "must follow the reach the user picked, not the shape of the address they typed: a
/// Custom <c>127.0.0.1:8443</c> is usually an SSH tunnel or reverse proxy that still needs
/// off-host reach, while 'This computer only' must never publish the runtime beyond loopback."
/// That is exactly the hole the previous version of this file had. It decided the habitat by
/// parsing <c>ASPNETCORE_URLS</c>, so the single case that matters most — this app behind a tunnel
/// or an nginx, listening on loopback and reachable from anywhere — passed the check and started
/// with one anonymous caller holding Admin on everything. An address is evidence about a socket,
/// not about who can reach it.
/// </para>
/// <para>
/// The loopback check is kept, narrowed to what it is actually good for: catching a deployment
/// that binds a routable address <i>without</i> having said it is shared. It is a guard against
/// the mistake, not the definition of the habitat.
/// </para>
/// </summary>
public static class IdentityComposition
{
    /// <summary>What the owner is called in the app and in attribution.</summary>
    public const string OwnerNameKey = "Identity:OwnerName";

    /// <summary>
    /// <c>this-machine</c> (the default) or <c>shared</c>. The one thing a deployment must say out
    /// loud, because everything else here follows from it.
    /// </summary>
    public const string ReachKey = "Identity:Reach";

    public const string Shared = "shared";

    public const string ThisMachine = "this-machine";

    public static TBuilder AddIdentity<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var reach = builder.Configuration[ReachKey]?.Trim();

        return string.IsNullOrEmpty(reach) || reach.Equals(ThisMachine, StringComparison.OrdinalIgnoreCase)
            ? OneOwner(builder)
            : reach.Equals(Shared, StringComparison.OrdinalIgnoreCase) ? Roster(builder)
            : throw new InvalidOperationException(
                $"'{reach}' is not a reach. Set {ReachKey} to '{ThisMachine}' or '{Shared}'."
            );
    }

    static TBuilder OneOwner<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var reason = NotThisMachine(builder.Configuration);

        if (reason is not null)
        {
            throw new InvalidOperationException(
                $"This host is not a machine one person owns: {reason}. The owner holds every "
                    + "permission with no sign-in, so serving it here would hand administration to "
                    + $"anyone who can reach the address. Bind to loopback, or set {ReachKey} to "
                    + $"'{Shared}' and give {PeopleRoster.SectionName} the people it serves."
            );
        }

        // Singletons, and honestly so: one person owning one machine is a fact about the process,
        // not about the request.
        builder.Services.AddSingleton<ICurrentPrincipal>(
            new MachineOwner(builder.Configuration[OwnerNameKey])
        );
        builder.Services.AddSingleton<IProjectPermissions, OwnerHoldsEverything>();

        return builder;
    }

    static TBuilder Roster<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var roster = PeopleRoster.ReadFrom(builder.Configuration);

        if (roster.IsEmpty)
        {
            // Starting with an empty roster would serve a shared deployment that refuses everyone,
            // which reads as a broken product rather than as an unfinished configuration — and the
            // temptation at that point is to turn the checking off.
            throw new InvalidOperationException(
                $"{ReachKey} is '{Shared}' but {PeopleRoster.SectionName} names nobody, so every "
                    + "request would be refused. List the people this deployment serves, each with "
                    + "the name of a credential."
            );
        }

        builder.Services.AddSingleton(roster);

        // Per request, because that is what the answer now depends on. The middleware has already
        // refused anything that reaches a handler without a person, so this resolves or the
        // pipeline never got here.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<RequestPrincipal>(provider =>
            new RequestPrincipal(Caller(provider))
        );
        builder.Services.AddScoped<ICurrentPrincipal>(provider =>
            provider.GetRequiredService<RequestPrincipal>()
        );
        // Audited, because this is the habitat where the answer can narrow — so a read slice that
        // declared FiltersToCaller and never asked is a leak here, where under one owner it was
        // merely redundant. See ProjectVisibilityAudit; the refusal is the pipeline's.
        builder.Services.AddScoped<IProjectPermissions>(provider => new AuditedProjectPermissions(
            provider.GetRequiredService<RequestPrincipal>(),
            provider.GetRequiredService<ProjectVisibilityAudit>()
        ));

        // An IStartupFilter rather than a line in the Server's Program.cs, so that composing
        // identity and enforcing it are the same act. A habitat that can be configured to require
        // credentials but where somebody has to remember a second call to turn the checking on is
        // a habitat that ships one day with the checking off.
        builder.Services.AddSingleton<IStartupFilter, RequireIdentity>();

        return builder;
    }

    static PeopleRoster.Person Caller(IServiceProvider provider) =>
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext is { } context
        && context.Items.TryGetValue(typeof(PeopleRoster.Person), out var caller)
        && caller is PeopleRoster.Person person
            ? person
            : throw new InvalidOperationException(
            "No caller on this request. Identity is composed per request from the credential the "
                + "middleware verified, so resolving a principal outside a request — a background "
                + "sweep, a hosted service — has no answer and must not be given a default one."
        );

    /// <summary>The reason this host is not somebody's own machine, or null when it is.</summary>
    static string? NotThisMachine(IConfiguration configuration)
    {
        var addresses = configuration["ASPNETCORE_URLS"] ?? string.Empty;

        // A wildcard bind is deliberately not treated as reachable: every container binds every
        // interface, and using that as the signal would refuse to start under a perfectly ordinary
        // `docker run -p 127.0.0.1:8080:8080`. What is being detected is a named, routable host.
        var reachable = addresses
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(address => Uri.TryCreate(address, UriKind.Absolute, out _))
            .Select(address => new Uri(address).Host)
            .FirstOrDefault(host =>
                host is not ("localhost" or "127.0.0.1" or "[::1]" or "+" or "*" or "0.0.0.0")
            );

        return reachable is null ? null : $"it listens on {reachable}, which is not loopback";
    }
}

/// <summary>
/// Puts <see cref="BearerIdentityMiddleware"/> in front of everything the Server maps, without the
/// Server having to ask for it.
/// </summary>
sealed class RequireIdentity : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<BearerIdentityMiddleware>();
            next(app);
        };
}

/// <summary>
/// The machine's owner: constructed from configuration, never stored. There is no users table to
/// seed and no row to migrate, because what this knows is simply that one person has the machine.
/// </summary>
sealed class MachineOwner(string? name) : ICurrentPrincipal
{
    public Principal Current { get; } =
        new(Principal.LocalOwnerId, string.IsNullOrWhiteSpace(name) ? "Local owner" : name);
}

/// <summary>
/// The owner administers everything on their own machine, and sees every project, so both answers
/// are constants. <c>null</c> from <see cref="VisibleProjects"/> means "all of them" — the same
/// contract every other implementation would honour, which is why callers need no special case.
/// </summary>
sealed class OwnerHoldsEverything : IProjectPermissions
{
    public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<ProjectRole?>(ProjectRole.Admin);

    public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>?>(null);
}
