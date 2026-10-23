using System.Reflection;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;

namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// What there is to check: the module assemblies, their requests, their handlers, and the
/// permission constants they declare.
/// <para>
/// <b>Everything here is a list, and a list is how an architecture test lies.</b> A rule expressed
/// as "no request may…" is vacuously true over an empty set, and the set is empty for entirely
/// ordinary reasons — a module assembly not copied beside the test, a discovery predicate that
/// stopped matching, a rename. That has already happened once in this repository, in the SQL
/// recorder that captured nothing while every test using it passed. So the guards in
/// <see cref="SurfaceTests"/> are not ceremony: they are the only thing standing between these
/// files and a suite that proves nothing while staying green.
/// </para>
/// <para>
/// Discovery mirrors <see cref="ModuleRegistration.Discover"/> — same filename prefix, same
/// contracts-assembly exclusion — but rooted at this test's own directory rather than the entry
/// assembly's, because under a test runner the entry assembly is the runner.
/// </para>
/// </summary>
static class Modules
{
    /// <summary>The modules that must be checked. A new one belongs in this list and in the csproj.
    /// #518: Coordination went with the old tree; the loop's journal is the Loop module's.</summary>
    public static readonly string[] Expected = ["Loop", "Pbis", "Runs", "Workspace"];

    /// <summary>
    /// #497. Modules created empty, exempt from contributing requests until their first use
    /// case lands. The issue that adds one removes the module from this list; #498 removed Loop.
    /// </summary>
    public static readonly string[] Skeletons = [];

    public static IReadOnlyList<Assembly> Assemblies { get; } = Discover();

    public static IReadOnlyList<IModule> Instances { get; } =
        [
            .. Assemblies
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type =>
                    typeof(IModule).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsInterface: false }
                )
                .Select(type => (IModule)Activator.CreateInstance(type)!)
                .OrderBy(module => module.Name, StringComparer.Ordinal),
        ];

    /// <summary>Every command and query a module defines, in the order a failure reads best in.</summary>
    public static IReadOnlyList<Type> Requests { get; } =
        [
            .. Types()
                .Where(type =>
                    Implements(type, typeof(ICommand<>)) || Implements(type, typeof(IQuery<>))
                )
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];

    public static IReadOnlyList<Type> Handlers { get; } =
        [
            .. Types()
                .Where(type =>
                    Implements(type, typeof(IAppCommandHandler<,>))
                    || Implements(type, typeof(IAppQueryHandler<,>))
                )
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];

    public static IReadOnlyList<Type> UseCases { get; } =
        [
            .. Types()
                .Where(type => typeof(IUseCase).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];

    /// <summary>Every entity type, for the rule that none of them is visible outside its module.</summary>
    public static IReadOnlyList<Type> Entities { get; } =
        [
            .. Types()
                .Where(type => typeof(BuildingBlocks.Domain.BaseEntity).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];

    /// <summary>
    /// The permission strings a module declares, as <c>constant name → value</c>. Read off the
    /// <c>*Permissions</c> classes rather than listed here, so a new permission is checked the
    /// moment it is written and nobody has to remember this file exists.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Permissions { get; } =
        Types()
            .Where(type =>
                type is { IsClass: true, IsAbstract: true, IsSealed: true }
                && type.Name.EndsWith("Permissions", StringComparison.Ordinal)
            )
            .SelectMany(type =>
                type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field is { IsLiteral: true, FieldType: { } fieldType }
                        && fieldType == typeof(string)
                    )
                    .Select(field => (Name: $"{type.Name}.{field.Name}", Value: (string)field.GetRawConstantValue()!))
            )
            .ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);

    /// <summary>The declaration on a request, or null when it has none. Null is the finding.</summary>
    public static RequiresAttribute? Declaration(Type request) =>
        request.GetCustomAttribute<RequiresAttribute>();

    /// <summary>Requests that name a permission rather than one of the two <see cref="Access"/> values.</summary>
    public static IReadOnlyList<Type> RequestsNamingAPermission { get; } =
        [.. Requests.Where(request => Declaration(request)?.Permission is not null)];

    /// <summary>The response type of a request's <c>ICommand&lt;T&gt;</c> or <c>IQuery&lt;T&gt;</c>.</summary>
    public static Type ResponseOf(Type request) =>
        request
            .GetInterfaces()
            .First(contract =>
                contract.IsGenericType
                && (
                    contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                    || contract.GetGenericTypeDefinition() == typeof(IQuery<>)
                )
            )
            .GetGenericArguments()[0];

    public static bool IsQuery(Type request) => Implements(request, typeof(IQuery<>));

    static IEnumerable<Type> Types() =>
        Assemblies.SelectMany(assembly =>
            assembly
                .GetTypes()
                .Where(type =>
                    type is { IsInterface: false, IsGenericTypeDefinition: false }
                    // Compiler-generated closures and iterator classes are not anybody's design.
                    && !type.Name.Contains('<', StringComparison.Ordinal)
                )
        );

    static bool Implements(Type type, Type openGeneric) =>
        !type.IsAbstract
        && type
            .GetInterfaces()
            .Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == openGeneric
            );

    static IReadOnlyList<Assembly> Discover()
    {
        var directory = AppContext.BaseDirectory;

        return
        [
            .. Directory
                .EnumerateFiles(directory, $"{ModuleRegistration.ModuleAssemblyPrefix}*.dll")
                .Where(path =>
                    !Path.GetFileNameWithoutExtension(path)
                        .EndsWith(".Contracts", StringComparison.Ordinal)
                    && !Path.GetFileNameWithoutExtension(path)
                        .EndsWith("Tests", StringComparison.Ordinal)
                )
                .Select(Assembly.LoadFrom)
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>How a failure names a type: the module and the nested path, without the noise.</summary>
    public static string Name(Type type) =>
        $"{type.Assembly.GetName().Name![ModuleRegistration.ModuleAssemblyPrefix.Length..]}: "
        + $"{type.FullName![(type.Namespace!.Length + 1)..].Replace('+', '.')}";
}
