using Aspire.Hosting.Pipelines;

#pragma warning disable ASPIREPIPELINES001 // Pipelines are experimental in 13.4.6; the reason for taking that on is in AppHost.cs.

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// Declares <c>ci</c> and the four stages it is made of, so that <c>aspire do ci</c> is the gate.
/// <para>
/// The stages are empty on purpose. Each is a name a resource's own step can point at with
/// <c>RequiredBySteps</c>, which is what keeps the gate's contents beside the resource that
/// produces them — add a resource with a test step and <c>ci</c> runs it, with nothing here to
/// update and therefore nothing here to forget.
/// </para>
/// </summary>
static class CiPipeline
{
    /// <summary>
    /// Adds the <c>ci</c> step and its four stages. Call it after every resource has declared its
    /// own steps; the pipeline is a graph, so the order of these calls does not matter, but reading
    /// it last matches the order it runs in.
    /// </summary>
    public static IDistributedApplicationBuilder WithCiSteps(
        this IDistributedApplicationBuilder builder
    )
    {
        Stage(builder, WellKnownCIStepNames.Install, "Dependencies are present", []);
        Stage(builder, WellKnownCIStepNames.Compile, "Everything compiles", [WellKnownCIStepNames.Install]);
        Stage(builder, WellKnownCIStepNames.Lint, "Static checks pass", [WellKnownCIStepNames.Install]);
        Stage(builder, WellKnownCIStepNames.Test, "The suite passes", [WellKnownCIStepNames.Compile]);

        builder.Pipeline.AddStep(
            new PipelineStep
            {
                Name = WellKnownCIStepNames.Ci,
                Description = "The gate: everything that must pass before a change is pushed",
                // Nothing of its own to do. `ci` is a name for "all four of those", and the value of
                // it being one name is that there are no halves to run by accident — which is the
                // failure this exists to remove, not a tidier way to write four commands.
                Action = _ => Task.CompletedTask,
                DependsOnSteps =
                [
                    WellKnownCIStepNames.Install,
                    WellKnownCIStepNames.Compile,
                    WellKnownCIStepNames.Lint,
                    WellKnownCIStepNames.Test,
                ],
            }
        );

        return builder;
    }

    static void Stage(
        IDistributedApplicationBuilder builder,
        string name,
        string description,
        List<string> dependsOn
    ) =>
        builder.Pipeline.AddStep(
            new PipelineStep
            {
                Name = name,
                Description = description,
                Action = _ => Task.CompletedTask,
                DependsOnSteps = dependsOn,
            }
        );
}
