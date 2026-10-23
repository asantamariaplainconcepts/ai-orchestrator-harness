using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// BR-009 checked at runtime: every operation declares what it requires, and every permission it
/// names is one that exists.
/// <para>
/// The analyzers cannot see any of this. A declaration is an attribute the pipeline reads by
/// reflection, so a missing one is not a compile error — it is a 403 the first time somebody clicks
/// the button, and a misspelt one is worse than that. <c>PermissionGrants.Holds</c> answers true for
/// Admin by rule, so a typo in <c>[Requires("tasks:sycn")]</c> is invisible to the person who owns
/// the machine and refuses every Member forever. Nothing in the code or in the response says why.
/// </para>
/// </summary>
public sealed class PermissionTests
{
    /// <summary>
    /// Default deny is the pipeline's behaviour and this is what keeps it from being a surprise.
    /// A request with no declaration is refused, so forgetting one ships a screen that 403s.
    /// </summary>
    [Fact]
    public void Every_request_declares_what_it_requires()
    {
        var undeclared = Modules
            .Requests.Where(request => Modules.Declaration(request) is null)
            .Select(Modules.Name)
            .ToArray();

        undeclared.ShouldBeEmpty(
            "every command and query declares a permission or a named Access value — the pipeline "
                + "refuses anything that declares nothing, so a missing attribute is a 403 nobody "
                + "sees until a person clicks the button"
        );
    }

    /// <summary>
    /// A declaration is one thing or the other, never both and never neither. The decorator refuses
    /// an attribute in a third state rather than guessing, which is right and also silent.
    /// </summary>
    [Fact]
    public void A_declaration_names_a_permission_or_an_access_value_and_not_both()
    {
        foreach (var request in Modules.Requests)
        {
            var declared = Modules.Declaration(request)!;

            (declared.Permission is not null ^ declared.Access is not null).ShouldBeTrue(
                Modules.Name(request)
            );
        }
    }

    [Fact]
    public void Every_permission_a_request_names_is_a_known_constant()
    {
        var known = Modules.Permissions.Values.ToHashSet(StringComparer.Ordinal);

        var unknown = Modules
            .RequestsNamingAPermission.Where(request =>
                !known.Contains(Modules.Declaration(request)!.Permission!)
            )
            .Select(request =>
                $"{Modules.Name(request)} requires '{Modules.Declaration(request)!.Permission}'"
            )
            .ToArray();

        unknown.ShouldBeEmpty(
            "a permission nothing declares as a constant is a permission no bundle can hold, so it "
                + "is refused to every Member and granted to every Admin by rule — the typo shows "
                + "up as nothing at all for whoever owns the machine"
        );
    }

    /// <summary>
    /// The pairing the authorization decorator says a test asserts, now asserted. Every permission
    /// here is project-scoped, so a request that names one without naming a project is a wiring
    /// mistake — and the decorator turns it into an <c>InvalidOperationException</c>, which is a 500
    /// on somebody's screen rather than a compile error.
    /// </summary>
    [Fact]
    public void A_request_that_names_a_permission_names_a_project_to_check_it_against()
    {
        var unscoped = Modules
            .RequestsNamingAPermission.Where(request =>
                !typeof(IScopedToProject).IsAssignableFrom(request)
            )
            .Select(Modules.Name)
            .ToArray();

        unscoped.ShouldBeEmpty(
            $"a request naming a permission must implement {nameof(IScopedToProject)} — there is "
                + "otherwise no project to check the permission against, and the pipeline throws"
        );
    }

    [Fact]
    public void Every_permission_granted_to_a_bundle_is_a_known_constant()
    {
        var known = Modules.Permissions.Values.ToHashSet(StringComparer.Ordinal);
        var granted = Granted(ProjectRole.Member);

        granted.ShouldNotBeEmpty("no module granted anything, so this rule checked nothing");
        granted.ShouldBeSubsetOf(known);
    }

    /// <summary>
    /// <b>Admin is a rule, not a list.</b> DEC-034 says "Admin = all", and <c>Holds</c> implements
    /// that directly; enumerating it in the table instead would be one line per permission that can
    /// silently miss the next one — a permission nobody remembered to grant Admin would be refused
    /// to the only bundle defined as holding it. So the table carries the bundles that are a subset,
    /// and Admin appearing in it at all is the mistake.
    /// </summary>
    [Fact]
    public void No_module_grants_anything_to_admin_explicitly()
    {
        Granted(ProjectRole.Admin).ShouldBeEmpty(
            "Admin holds every permission by rule in PermissionGrants.Holds — granting one "
                + "explicitly starts a list that will eventually be missing an entry"
        );
    }

    /// <summary>
    /// The inverse rule, and the one with a finding in it: a permission constant that no operation
    /// requires is enforced nowhere.
    /// <para>
    /// Nine are in that state today, and they are not all the same thing. Some are ahead of their
    /// operation — nothing runs a folder command through its own use case yet. Others are behind
    /// one: <c>runs:start</c> is documented as "the operation that spends money and changes source"
    /// and <c>workflows:edit</c> as the reason editing is deliberately not granted to Member, and
    /// both of those operations declare <see cref="Access.AnyCaller"/> instead of naming them. That
    /// is invisible while one person owns the machine, because that person is Admin and Admin holds
    /// everything; it becomes a privilege gap the moment H3 grows identity past one owner.
    /// </para>
    /// <para>
    /// Written as an exact list rather than a warning so that it can only shrink quietly. A new
    /// orphan fails here, and so does fixing one — which is the point: both are decisions somebody
    /// should read.
    /// </para>
    /// </summary>
    [Fact]
    public void The_permissions_no_operation_requires_are_the_ones_we_know_about()
    {
        var required = Modules
            .RequestsNamingAPermission.Select(request => Modules.Declaration(request)!.Permission!)
            .ToHashSet(StringComparer.Ordinal);

        var orphans = Modules
            .Permissions.Values.Where(permission => !required.Contains(permission))
            .Order(StringComparer.Ordinal)
            .ToArray();

        orphans.ShouldBeEmpty(
            "a permission constant nothing requires is enforced nowhere: wire it to the operation "
                + "it was written for, or delete it"
        );
    }

    /// <summary>
    /// Runs a module's own <c>Add()</c> and reads the table back, rather than trusting a list in a
    /// test. <c>GrantedTo</c> exists for exactly this.
    /// </summary>
    static HashSet<string> Granted(ProjectRole role)
    {
        var granted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var module in Modules.Instances)
        {
            var services = new ServiceCollection();
            services.AddOptions();

            module.Add(services, new ConfigurationBuilder().Build());

            using var provider = services.BuildServiceProvider();
            granted.UnionWith(
                provider.GetRequiredService<IOptions<PermissionGrants>>().Value.GrantedTo(role)
            );
        }

        return granted;
    }
}
