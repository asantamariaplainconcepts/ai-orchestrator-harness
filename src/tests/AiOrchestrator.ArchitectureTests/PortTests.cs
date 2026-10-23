using System.Xml.Linq;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;

namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// #497. The shape the console is built on: five ports in <c>BuildingBlocks/Ports</c>, and <c>Loop</c> and
/// <c>Runs</c> meeting at <see cref="IRunRequests"/> without either referencing the other.
/// </summary>
public sealed class PortTests
{
    static readonly string[] LoopAndRuns = ["Loop", "Runs"];

    /// <summary>
    /// Read in two places because each misses what the other sees. The assembly's reference table
    /// holds only what the code uses, so a ProjectReference nothing calls yet is absent from it;
    /// the csproj holds that reference, but not one smuggled in through another project.
    /// </summary>
    [Fact]
    public void Loop_and_Runs_do_not_reference_each_other()
    {
        var assemblies = Modules
            .Assemblies.Where(assembly => LoopAndRuns.Any(module => assembly.GetName().Name == Prefixed(module)))
            .ToArray();

        // The guard: with either assembly missing, the assertions below hold over nothing.
        assemblies.Select(assembly => assembly.GetName().Name).ShouldBe(LoopAndRuns.Select(Prefixed));

        var crossings = assemblies
            .SelectMany(assembly =>
                assembly
                    .GetReferencedAssemblies()
                    .Where(reference => LoopAndRuns.Select(Prefixed).Contains(reference.Name))
                    .Where(reference => reference.Name != assembly.GetName().Name)
                    .Select(reference => $"{assembly.GetName().Name} → {reference.Name} (assembly)")
            )
            .Concat(
                from module in LoopAndRuns
                from other in LoopAndRuns.Where(other => other != module)
                from reference in ProjectReferences(module)
                where reference.Contains(Prefixed(other), StringComparison.Ordinal)
                select $"{Prefixed(module)} → {reference} (csproj)"
            )
            .ToArray();

        crossings.ShouldBeEmpty(
            "Loop asks Runs for a run through IRunRequests in BuildingBlocks; a reference between "
                + "them is the coupling that contract exists to prevent"
        );
    }

    [Fact]
    public void The_five_ports_are_the_interfaces_in_Ports()
    {
        typeof(ISession)
            .Assembly.GetExportedTypes()
            .Where(type => type is { IsInterface: true, Namespace: "AiOrchestrator.BuildingBlocks.Ports" })
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["IAgentProvider", "IEnvironment", "IPbiProvider", "IScriptRunner", "ISession"]);
    }

    static string Prefixed(string module) => $"AiOrchestrator.Modules.{module}";

    static IEnumerable<string> ProjectReferences(string module)
    {
        var project = Path.Combine(
            SourceRoot(),
            "modules",
            module,
            Prefixed(module),
            $"{Prefixed(module)}.csproj"
        );

        File.Exists(project).ShouldBeTrue($"{project} is where the module's project should be");

        return XDocument
            .Load(project)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? "");
    }

    /// <summary>Walks up from the test binary to <c>src/</c>, from a worktree as readily as from the main checkout.</summary>
    static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiOrchestrator.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No AiOrchestrator.slnx above the test binary.");
    }
}
