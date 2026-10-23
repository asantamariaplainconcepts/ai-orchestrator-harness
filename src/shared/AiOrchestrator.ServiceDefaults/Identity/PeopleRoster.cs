using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.ServiceDefaults.Identity;

/// <summary>
/// The people this deployment serves, and what each may do — the stored <c>IProjectPermissions</c>
/// the single-owner composition stands in for.
/// <para>
/// Configuration rather than a table, and that is a decision rather than a shortcut. A table would
/// have to live in some module's schema, and there is no identity module — permissions belong to
/// no more of this product than secrets do, which is the same reasoning that put
/// <see cref="BuildingBlocks.Secrets.ProtectedFileSecretStore"/> in files rather than rows. It also
/// means the roster is reviewable: <c>git diff</c> shows who gained Admin, which no `UPDATE`
/// does.
/// </para>
/// <para>
/// <b>No value here is a credential.</b> Each person names one, and the name is resolved through
/// <see cref="BuildingBlocks.Secrets.ISecretResolver"/> at the moment a request presents a token —
/// so the roster can sit in a repository, and rotating somebody's token is not a deployment.
/// </para>
/// </summary>
public sealed class PeopleRoster
{
    /// <summary>
    /// <code>
    /// Identity:People:0:Id               = ada
    /// Identity:People:0:Name             = Ada Lovelace
    /// Identity:People:0:CredentialName   = keychain:aio-ada
    /// Identity:People:0:Administers      = true          # every project, present and future
    /// Identity:People:0:Projects:&lt;guid&gt; = Admin | Member
    /// </code>
    /// </summary>
    public const string SectionName = "Identity:People";

    readonly Dictionary<string, Person> _byId;

    PeopleRoster(Dictionary<string, Person> byId) => _byId = byId;

    public static PeopleRoster ReadFrom(IConfiguration configuration)
    {
        var people = new Dictionary<string, Person>(StringComparer.Ordinal);

        foreach (var entry in configuration.GetSection(SectionName).GetChildren())
        {
            var id = entry["Id"]?.Trim();

            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:{entry.Key} has no Id. Every person needs one: it is what a "
                        + "request names, and what a run is attributed to."
                );
            }

            // The id travels in the credential as `<id>:<token>`, so a colon in it would make the
            // split ambiguous — and an ambiguous split is one that can be made to name somebody
            // else. Refused at startup rather than mis-parsed per request.
            if (id.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The person id '{id}' contains a colon, which separates the id from the token "
                        + "in a presented credential. Use a colon-free id."
                );
            }

            var credentialName = entry["CredentialName"]?.Trim();

            if (string.IsNullOrEmpty(credentialName))
            {
                throw new InvalidOperationException(
                    $"'{id}' names no credential. Set {SectionName}:{entry.Key}:CredentialName to "
                        + "the *name* of a secret — never a token — and store the value where this "
                        + "deployment's resolver reads."
                );
            }

            if (!people.TryAdd(id, Read(id, credentialName, entry)))
            {
                // Two entries, one id: whichever won would decide whose token authenticates as
                // whom, and last-one-wins is not a thing to discover in production.
                throw new InvalidOperationException(
                    $"The person id '{id}' appears twice in {SectionName}."
                );
            }
        }

        return new PeopleRoster(people);
    }

    static Person Read(string id, string credentialName, IConfigurationSection entry)
    {
        var roles = new Dictionary<Guid, ProjectRole>();

        foreach (var grant in entry.GetSection("Projects").GetChildren())
        {
            if (!Guid.TryParse(grant.Key, out var projectId))
            {
                throw new InvalidOperationException(
                    $"'{grant.Key}' under {SectionName}:{entry.Key}:Projects is not a project id."
                );
            }

            if (!Enum.TryParse<ProjectRole>(grant.Value, ignoreCase: true, out var role))
            {
                // Named rather than defaulted. A typo silently becoming Member is a quiet
                // downgrade; a typo silently becoming Admin is worse.
                throw new InvalidOperationException(
                    $"'{grant.Value}' is not a role. Use {nameof(ProjectRole.Member)} or "
                        + $"{nameof(ProjectRole.Admin)}."
                );
            }

            roles[projectId] = role;
        }

        return new Person(
            new Principal(id, entry["Name"]?.Trim() is { Length: > 0 } name ? name : id),
            credentialName,
            bool.TryParse(entry["Administers"], out var administers) && administers,
            roles
        );
    }

    public bool IsEmpty => _byId.Count == 0;

    public int Count => _byId.Count;

    public Person? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// One person and their whole answer.
    /// </summary>
    /// <param name="Administers">
    /// Design D4's bootstrap administrator: Admin on every project, including ones created later.
    /// Kept as a flag rather than as rows for each project, because the alternative is a roster
    /// that has to be edited every time somebody creates a project — and a permission model people
    /// route around is not one.
    /// </param>
    public sealed record Person(
        Principal Principal,
        string CredentialName,
        bool Administers,
        IReadOnlyDictionary<Guid, ProjectRole> Roles
    ) : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectRole?>(
                Administers ? ProjectRole.Admin
                    : Roles.TryGetValue(projectId, out var role) ? role
                    : null
            );

        /// <summary>
        /// Null means all of them, per the interface — so an administrator costs no enumeration,
        /// and everybody else is filtered to exactly what the roster names.
        /// </summary>
        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(Administers ? null : Roles.Keys.ToHashSet());
    }
}
