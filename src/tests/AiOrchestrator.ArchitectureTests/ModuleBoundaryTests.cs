using System.Reflection;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Routing;
using NetArchTest.Rules;

namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// The module boundary, checked against the assemblies that shipped rather than the source that was
/// compiled.
/// <para>
/// MOD001 to MOD005 and CQS001 already say most of this at build time, and where they do, this file
/// is the second opinion that survives a <c>#pragma warning disable</c>, a generated file the
/// analyzer skipped, and an analyzer that stopped being attached — which is a real possibility here,
/// because attaching them is one <c>Condition</c> in Directory.Build.props that a new project can
/// fail to match. Three of the rules below have no analyzer at all.
/// </para>
/// </summary>
public sealed class ModuleBoundaryTests
{
    /// <summary>
    /// MOD002's complement. The analyzer sees a source reference; this sees the assembly's own
    /// reference table, which also catches a ProjectReference added for a build reason and never
    /// used — the thing that makes the next cross-module call compile.
    /// </summary>
    [Fact]
    public void No_module_references_another_module()
    {
        var crossings = Modules
            .Assemblies.SelectMany(assembly =>
                assembly
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name!)
                    .Where(name =>
                        name.StartsWith(ModuleRegistration.ModuleAssemblyPrefix, StringComparison.Ordinal)
                        && name != assembly.GetName().Name
                    )
                    .Select(name => $"{assembly.GetName().Name} → {name}")
            )
            .ToArray();

        crossings.ShouldBeEmpty(
            "modules talk through the shared building blocks and integration events, never to each "
                + "other — a reference here is the coupling the boundary exists to prevent"
        );
    }

    /// <summary>MOD003's complement: an entity is a module's own business.</summary>
    [Fact]
    public void No_module_exports_a_domain_entity()
    {
        foreach (var assembly in Modules.Assemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .Inherit(typeof(BaseEntity))
                .Should()
                .NotBePublic()
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"{assembly.GetName().Name} exports "
                    + string.Join(", ", result.FailingTypeNames ?? [])
            );
        }
    }

    /// <summary>MOD001's complement: a request is addressed by URL, never by type.</summary>
    [Fact]
    public void No_module_exports_a_command_or_a_query()
    {
        var exported = Modules
            .Requests.Where(request => request.IsVisible && request.IsPublic)
            .Select(Modules.Name)
            .ToArray();

        exported.ShouldBeEmpty(
            "a public request is a request another module can send — the seam between modules is "
                + "HTTP and integration events, not a type reference"
        );
    }

    /// <summary>CQS001's complement.</summary>
    [Fact]
    public void Every_handler_is_internal_and_sealed()
    {
        var wrong = Modules
            .Handlers.Where(handler => handler.IsVisible || !handler.IsSealed)
            .Select(Modules.Name)
            .ToArray();

        wrong.ShouldBeEmpty("handlers are internal and sealed: nothing extends or replaces one");
    }

    /// <summary>
    /// <b>No analyzer says this, and getting it wrong is silent.</b> The host maps a slice by
    /// reflecting for a <c>public static AddRoutes(IEndpointRouteBuilder)</c> and skipping anything
    /// that does not have one. A use case with the wrong signature — an instance method, a different
    /// parameter, a rename — compiles, registers, and is never routed. The failure is a 404 on one
    /// screen, found by a person.
    /// </summary>
    [Fact]
    public void Every_use_case_exposes_the_routes_method_the_host_looks_for()
    {
        var unroutable = Modules
            .UseCases.Where(useCase =>
                useCase.GetMethod(
                    UseCaseDiscovery.AddRoutesMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    [typeof(IEndpointRouteBuilder)]
                )
                is null
            )
            .Select(Modules.Name)
            .ToArray();

        unroutable.ShouldBeEmpty(
            $"an IUseCase without a public static {UseCaseDiscovery.AddRoutesMethodName}"
                + "(IEndpointRouteBuilder) is skipped by discovery without a word — its endpoints "
                + "simply do not exist"
        );

        // The guard: discovery finding nothing would make the assertion above vacuous.
        Modules
            .Assemblies.SelectMany(UseCaseDiscovery.FindIn)
            .Count()
            .ShouldBe(Modules.UseCases.Count);
    }

    /// <summary>
    /// Also nobody's analyzer. <c>Sender</c> resolves a handler out of the container by the closed
    /// generic, so a request with no handler — or with two — is an exception the first time somebody
    /// sends it, at whatever hour they happen to.
    /// </summary>
    [Fact]
    public void Every_request_has_exactly_one_handler()
    {
        var counted = Modules.Requests.ToDictionary(
            Modules.Name,
            request =>
                Modules
                    .Handlers.Count(handler =>
                        handler
                            .GetInterfaces()
                            .Any(contract =>
                                contract.IsGenericType
                                && contract.GetGenericTypeDefinition()
                                    == (
                                        Modules.IsQuery(request)
                                            ? typeof(IAppQueryHandler<,>)
                                            : typeof(IAppCommandHandler<,>)
                                    )
                                && contract.GetGenericArguments()[0] == request
                                && contract.GetGenericArguments()[1] == Modules.ResponseOf(request)
                            )
                    )
        );

        counted.Where(entry => entry.Value != 1)
            .Select(entry => $"{entry.Key} has {entry.Value} handlers")
            .ShouldBeEmpty();
    }

    /// <summary>
    /// MOD004's complement. Slices map their own endpoints; a controller in a module is a second way
    /// to do the same thing, which is how two ways of authorizing the same operation appear.
    /// </summary>
    [Fact]
    public void No_module_declares_a_controller()
    {
        var controllers = Modules
            .Assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(Modules.Name)
            .ToArray();

        controllers.ShouldBeEmpty(
            "a slice maps its own endpoints; a controller beside them is a second way to reach the "
                + "same operation, which is how two ways of authorizing it appear"
        );
    }
}
