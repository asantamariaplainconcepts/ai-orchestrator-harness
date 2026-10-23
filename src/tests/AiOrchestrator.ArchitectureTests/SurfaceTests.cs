namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// The tests that police the other tests.
/// <para>
/// Every rule in this project is a statement about a set, and a statement about an empty set is
/// true. These assert the sets are not empty and are the size they should be, because the failure
/// mode of an architecture suite is not a false alarm — it is silence. The first SQL recorder in
/// this repository captured nothing and every test that used it passed; this is the same guard, for
/// the same reason, one layer up.
/// </para>
/// </summary>
public sealed class SurfaceTests
{
    [Fact]
    public void Every_module_is_present_to_be_checked()
    {
        Modules
            .Instances.Select(module => module.Name)
            .ShouldBe(
                Modules.Expected,
                "a module missing here is a module whose requests and permissions nothing in this "
                    + "project checks — add a ProjectReference to it and name it in Modules.Expected"
            );
    }

    /// <summary>
    /// A count, not merely "not empty". Discovery that quietly matched three of twenty-one requests
    /// would pass a not-empty check and check almost nothing.
    /// </summary>
    [Fact]
    public void Every_module_contributes_requests_and_handlers()
    {
        Modules.Requests.Count.ShouldBeGreaterThanOrEqualTo(20);
        Modules.Handlers.Count.ShouldBeGreaterThanOrEqualTo(20);
        Modules.UseCases.Count.ShouldBeGreaterThanOrEqualTo(20);
        Modules.Entities.ShouldNotBeEmpty();

        foreach (var module in Modules.Expected.Except(Modules.Skeletons))
        {
            Modules
                .Requests.Where(request => request.Assembly.GetName().Name!.EndsWith(module, StringComparison.Ordinal))
                .ShouldNotBeEmpty($"{module} contributed no requests, which cannot be right");
        }
    }

    [Fact]
    public void The_permission_constants_are_found_where_they_live()
    {
        Modules.Permissions.Count.ShouldBeGreaterThanOrEqualTo(5);

        // The shape as well as the count: a permission is "module:operation", lowercase, and the
        // prefix is what makes two modules' "read" different permissions.
        foreach (var (name, value) in Modules.Permissions)
        {
            value.ShouldContain(":", customMessage: name);
            value.ShouldBe(value.ToLowerInvariant(), $"{name} is not lowercase");
        }
    }

    /// <summary>
    /// Without this, the two rules about permission-naming requests hold over nothing. Six name a
    /// permission today; all six are in Workspace.
    /// </summary>
    [Fact]
    public void Some_requests_name_a_permission_rather_than_an_access_value()
    {
        Modules
            .RequestsNamingAPermission.Count.ShouldBeGreaterThanOrEqualTo(
                4,
                "the permission rules below are vacuous if nothing names a permission"
            );
    }
}
