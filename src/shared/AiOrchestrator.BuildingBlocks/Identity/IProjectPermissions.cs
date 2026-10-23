namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// What the caller may do, on a named project (#13, BR-009). The question the principal cannot
/// answer: roles are per project, so "their role" is only a fact once a project is named
/// (design D2).
/// <para>
/// Composed per habitat like every other seam here. Where one person owns the machine there is
/// nothing to look up; where people sign in, the answer is rows plus the configured bootstrap
/// administrators (design D4).
/// </para>
/// </summary>
public interface IProjectPermissions
{
    /// <summary>
    /// The caller's role on this project, or <c>null</c> when they hold none — which is also the
    /// answer for a project that does not exist, deliberately: the two must be indistinguishable
    /// or a refusal becomes a way to enumerate projects.
    /// </summary>
    Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The projects this caller may see, or <c>null</c> meaning <b>all of them</b> — the owner, the
    /// self-host habitat, a bootstrap administrator. Null rather than an enumeration of every id
    /// so that "everything" costs no query, and so consumers filter with one expression:
    /// <c>if (visible is not null) query = query.Where(row => visible.Contains(row.ProjectId));</c>
    /// </summary>
    Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken);
}

/// <summary>
/// BR-009's bundles, as DEC-034 fixed them: Admin is everything on that project, Member observes
/// and triggers. Scoped to one project — a tenant-wide administrator is deliberately not a
/// concept here (design D6).
/// </summary>
public enum ProjectRole
{
    Member = 1,
    Admin = 2,
}

/// <summary>
/// Whether anything on this request actually asked which projects the caller may see.
/// <para>
/// <see cref="Access.FiltersToCaller"/> is a claim — "reaches across projects, and narrows its own
/// answer to the ones the caller may see" — and until this existed it was only a claim. Every one
/// of the nine use cases declaring it went to the database unfiltered, and the declaration's own
/// doc comment names the failure exactly: without it, <i>"I filter" would be indistinguishable
/// from "I hand everything to everyone"</i>. They were indistinguishable because they were the
/// same thing.
/// </para>
/// <para>
/// Invisible under one owner, because the owner sees everything and an unfiltered answer is the
/// right answer. It becomes a leak the moment a second person exists, which is why it surfaced
/// with H3 and not before.
/// </para>
/// </summary>
public sealed class ProjectVisibilityAudit
{
    public bool Asked { get; private set; }

    public void Record() => Asked = true;
}

/// <summary>
/// Notes that the caller's visible set was asked for, and otherwise answers exactly like the
/// implementation it wraps.
/// <para>
/// Composed by the host beside the real one, in habitats where the answer can narrow. Where it
/// cannot — one owner, or a bootstrap administrator — nothing needs auditing, because there is no
/// narrower answer for a handler to have missed.
/// </para>
/// </summary>
public sealed class AuditedProjectPermissions(
    IProjectPermissions inner,
    ProjectVisibilityAudit audit
) : IProjectPermissions
{
    public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
        inner.RoleOn(projectId, cancellationToken);

    public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken)
    {
        audit.Record();
        return inner.VisibleProjects(cancellationToken);
    }
}
