using System.Security.Cryptography;
using System.Text;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AiOrchestrator.ServiceDefaults.Identity;

/// <summary>
/// What a request presented, and who — if anybody — it makes the caller.
/// <para>
/// The credential is <c>&lt;person-id&gt;:&lt;token&gt;</c>, which is orca's shape
/// (<c>src/main/runtime/device-registry.ts</c>: a <c>deviceId</c> beside its own <c>token</c>) and
/// is here for a reason that is not symmetry. A bare token would have to be checked against every
/// person in the roster to find out whose it is, and each check is a secret resolution — which on
/// macOS is a <c>security</c> process, measured at ~124 ms. Naming the person makes it exactly one
/// lookup per request whether the roster holds two people or two hundred, and a wrong id costs one
/// failed lookup rather than a sweep.
/// </para>
/// </summary>
static class PresentedCredential
{
    const string BearerPrefix = "Bearer ";

    /// <summary>
    /// SignalR cannot set headers on a WebSocket handshake, so its <c>accessTokenFactory</c> puts
    /// the value here instead. Accepted only for that reason: a credential in a query string is a
    /// credential in an access log, which is why it is not an option anywhere a header will do.
    /// </summary>
    const string WebSocketQueryKey = "access_token";

    public static (string Id, string Token)? Read(HttpRequest request)
    {
        var presented = FromHeader(request) ?? FromQuery(request);

        if (presented is null)
        {
            return null;
        }

        var separator = presented.IndexOf(':', StringComparison.Ordinal);

        return separator <= 0 || separator == presented.Length - 1
            ? null
            : (presented[..separator], presented[(separator + 1)..]);
    }

    static string? FromHeader(HttpRequest request) =>
        request.Headers.Authorization.ToString() is { Length: > 0 } value
        && value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[BearerPrefix.Length..].Trim()
            : null;

    static string? FromQuery(HttpRequest request) =>
        request.Query.TryGetValue(WebSocketQueryKey, out var value)
        && value.ToString() is { Length: > 0 } token
            ? token
            : null;

    /// <summary>
    /// Whether the presented token is the stored one, in time that does not depend on how much of
    /// it was right. Both sides are hashed first so that a length difference — which
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> reports by returning early — cannot
    /// itself be measured.
    /// </summary>
    public static bool Matches(string presented, string stored) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)),
            SHA256.HashData(Encoding.UTF8.GetBytes(stored))
        );
}

/// <summary>
/// Resolves the caller once per request and holds the answer for the rest of it.
/// <para>
/// Scoped, which is the whole difference between this habitat and the owner's: there, one person
/// is a process-lifetime fact and a singleton is honest about that. Here the answer belongs to the
/// request, and a singleton would be the bug where the second person to call sees the first one's
/// permissions.
/// </para>
/// </summary>
sealed class RequestPrincipal(PeopleRoster.Person person)
    : ICurrentPrincipal,
        IProjectPermissions
{
    public Principal Current => person.Principal;

    public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
        person.RoleOn(projectId, cancellationToken);

    public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
        person.VisibleProjects(cancellationToken);
}

/// <summary>
/// Refuses a request that named nobody this deployment knows, before a handler is resolved.
/// <para>
/// The refusal is the same for an unknown id and a wrong token, and says neither which it was nor
/// whether the person exists — the same rule <see cref="PermissionDeniedException"/> keeps for the
/// same reason: a refusal that varies is a refusal that answers questions.
/// </para>
/// </summary>
sealed class BearerIdentityMiddleware(RequestDelegate next, ISecretResolver secrets)
{
    /// <summary>
    /// Everything the product does, including the run-log hub at <c>/api/hubs/run-log</c>. The SPA
    /// bundle is deliberately outside it: a caller has to load the page before they can present
    /// anything, and the bundle is not a secret — the API it talks to is.
    /// </summary>
    const string Protected = "/api";

    /// <summary>
    /// The two exceptions, and they are exceptions rather than an oversight. Both live under
    /// <c>/api</c> and neither is data: they answer "is this process up", which whatever started
    /// it already knows. Behind a credential they would refuse the packaged launcher's own
    /// readiness check and every orchestrator's probe, and the failure would read as an
    /// application that never came up.
    /// </summary>
    static readonly string[] Unprotected = [ServiceDefaults.HealthEndpoint, ServiceDefaults.AliveEndpoint];

    public async Task Invoke(HttpContext context, PeopleRoster roster)
    {
        if (
            !context.Request.Path.StartsWithSegments(Protected, StringComparison.Ordinal)
            || Array.Exists(
                Unprotected,
                endpoint => context.Request.Path.StartsWithSegments(endpoint, StringComparison.Ordinal)
            )
        )
        {
            await next(context);
            return;
        }

        if (await Authenticate(context, roster) is not { } person)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        context.Items[typeof(PeopleRoster.Person)] = person;

        await next(context);
    }

    async Task<PeopleRoster.Person?> Authenticate(HttpContext context, PeopleRoster roster)
    {
        if (PresentedCredential.Read(context.Request) is not { } presented)
        {
            return null;
        }

        var (id, token) = presented;

        if (roster.Find(id) is not { } person)
        {
            return null;
        }

        try
        {
            var stored = await secrets.Resolve(person.CredentialName, context.RequestAborted);

            return PresentedCredential.Matches(token, stored) ? person : null;
        }
        catch (SecretNotFoundException)
        {
            // A person on the roster whose credential was never stored. Refused like any other
            // failure to authenticate, and deliberately not distinguished in the response: "that
            // account exists but is not set up" is an answer to a question nobody asking it is
            // entitled to.
            return null;
        }
    }
}
