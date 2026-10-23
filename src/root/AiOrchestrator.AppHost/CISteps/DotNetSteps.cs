using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

#pragma warning disable ASPIREPIPELINES001 // Pipelines are experimental in 13.4.6; the reason for taking that on is in AppHost.cs.

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// The .NET half of <c>ci</c>: restore, build, test — every one of them against a solution this
/// file was handed by name.
/// <para>
/// <b>Naming the solution is the point of this change.</b> On 15 August an agent ran
/// <c>dotnet test</c> with no argument. From the repository root that is
/// <c>MSB1003: Specify a project or solution file</c>; from a directory holding one project it is
/// worse, because it succeeds — it ran a subset and reported <b>118 tests as a full run</b> when
/// the suite is 250. The 132 it skipped included <c>InfrastructureTests</c> and
/// <c>ArchitectureTests</c>, the two that police the module boundaries, and #24 merged with them
/// unrun. So <paramref name="solution"/> has no default and no overload without it: the argument
/// that was forgotten is now one the compiler will not let you forget.
/// </para>
/// </summary>
static class DotNetSteps
{
    /// <summary>Adds <c>dotnet restore &lt;solution&gt;</c> to the <c>install</c> stage.</summary>
    public static IResourceBuilder<ProjectResource> WithRestoreStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = RestoreStep(factory.Resource.Name),
            Description = $"dotnet restore {Path.GetFileName(solution)}",
            Action = context =>
                StepCommand.Run(
                    "dotnet",
                    ["restore", solution],
                    Beside(solution),
                    context.Logger,
                    context.CancellationToken
                ),
            RequiredBySteps = [WellKnownCIStepNames.Install],
        });

    /// <summary>Adds <c>dotnet build &lt;solution&gt; --no-restore</c> to the <c>compile</c> stage.</summary>
    public static IResourceBuilder<ProjectResource> WithBuildStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = BuildStep(factory.Resource.Name),
            Description = $"dotnet build {Path.GetFileName(solution)}",
            Action = context =>
                StepCommand.Run(
                    "dotnet",
                    ["build", solution, "--no-restore"],
                    Beside(solution),
                    context.Logger,
                    context.CancellationToken
                ),
            // No configuration argument, here or below. `dotnet build src/AiOrchestrator.slnx` is
            // what AGENTS.md has always told people to run and what every warning-as-error in this
            // repository has been measured against; a gate that quietly builds Release instead is a
            // gate that disagrees with the command it replaced.
            DependsOnSteps = [RestoreStep(factory.Resource.Name)],
            RequiredBySteps = [WellKnownCIStepNames.Compile],
        });

    /// <summary>
    /// Adds <c>dotnet test &lt;solution&gt; --no-restore</c> to the <c>test</c> stage: all 250 of
    /// them, including the end-to-end suite that starts a real AppHost.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithTestStep(
        this IResourceBuilder<ProjectResource> builder,
        string solution
    ) =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = WellKnownCIStepNames.For("test", factory.Resource.Name),
            Description = $"dotnet test {Path.GetFileName(solution)}",
            Action = context =>
                TestStep.Run(solution, context.Logger, context.CancellationToken),
            // The whole `compile` stage, not just this project's build. Two reasons, and the
            // second one is why it is `compile` rather than the `install` this first said.
            //
            // `EndToEndTests` starts the AppHost, which starts the Vite dev server as a resource,
            // and a checkout whose `src/frontend/node_modules` is missing fails all three of those
            // tests on the same line. `compile` comes after `install`, so that still holds.
            //
            // And `dotnet test` builds the Server, whose `wwwroot` is written by the frontend's
            // `vite build` in this same stage. Vite names its output by content hash and deletes
            // what it replaces, so a Server build that globs `wwwroot` while that is happening
            // resolves an asset that has just stopped existing: *No file exists for the asset at
            // either location 'wwwroot/assets/index-D0ek07l7.css'*. Depending on `install` left the
            // two racing — three consecutive runs of `aspire do ci` on an unchanged tree went pass,
            // fail, pass. **A gate that is green twice out of three teaches people to re-run it**,
            // which is worse than the list of commands it replaced.
            //
            // Waiting on the stage is how a step says "after everything anyone compiles" without
            // naming a step in another file that this one cannot know exists.
            DependsOnSteps = [BuildStep(factory.Resource.Name), WellKnownCIStepNames.Compile],
            RequiredBySteps = [WellKnownCIStepNames.Test],
        });

    static string RestoreStep(string resource) =>
        WellKnownCIStepNames.For("restore", resource);

    static string BuildStep(string resource) => WellKnownCIStepNames.For("build", resource);

    /// <summary>
    /// Runs the command in the solution's own directory. The solution is passed as a full path so
    /// this only decides where relative output — a test log, an artefact — lands, but it decides it
    /// the same way whatever directory <c>aspire do</c> was invoked from, which is the property
    /// that makes the gate the same command everywhere.
    /// </summary>
    static string Beside(string solution) =>
        Path.GetDirectoryName(Path.GetFullPath(solution))
        ?? throw new InvalidOperationException($"{solution} has no directory to run in");
}
