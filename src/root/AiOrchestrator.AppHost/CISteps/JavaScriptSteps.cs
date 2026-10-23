using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

#pragma warning disable ASPIREPIPELINES001 // Pipelines are experimental in 13.4.6; the reason for taking that on is in AppHost.cs.

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// The SPA's half of <c>ci</c>: install, then the three gates AGENTS.md has always listed —
/// <c>pnpm typecheck</c>, <c>pnpm lint</c>, <c>pnpm build</c>.
/// <para>
/// Each one names a script in <c>src/frontend/package.json</c> and nothing else. The package
/// manager comes from the resource, so <c>WithPnpm()</c> in <c>AppHost.cs</c> is what decides it,
/// and the working directory comes from the resource too — the AppHost already knows where the SPA
/// is, and a second copy of that path is a second thing to get wrong when it moves.
/// </para>
/// <para>
/// <b>What is deliberately absent: <c>pnpm format:check</c>.</b> It exits 1 on <c>master</c> today,
/// on 17 files nobody in this change touched. A gate that is red the day it is written teaches
/// people to read past it, and fixing those 17 files here would widen this change into a diff whose
/// every line is Prettier's opinion. It is its own change, and until then this gate is exactly the
/// three commands it replaces.
/// </para>
/// </summary>
static class JavaScriptSteps
{
    /// <summary>Adds <c>pnpm install</c> to the <c>install</c> stage.</summary>
    public static IResourceBuilder<T> WithInstallStep<T>(this IResourceBuilder<T> builder)
        where T : IResource =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            Name = InstallStep(factory.Resource.Name),
            Description = $"{factory.Resource.PackageManager()} install",
            Action = Runs(factory.Resource, ["install"]),
            RequiredBySteps = [WellKnownCIStepNames.Install],
        });

    /// <summary>
    /// Adds <c>pnpm &lt;script&gt;</c> to <paramref name="stage"/>, after the install step.
    /// </summary>
    /// <param name="script">
    /// A script name from <c>package.json</c>, run through <c>pnpm run</c> rather than as a bare
    /// argument: <c>pnpm build</c> works by shorthand, <c>pnpm typecheck</c> only because pnpm falls
    /// through to <c>run</c>, and one of those shorthands colliding with a future pnpm subcommand is
    /// a gate that starts checking something else without saying so.
    /// </param>
    /// <param name="stage">Which of <see cref="WellKnownCIStepNames"/> this belongs to.</param>
    /// <param name="after">
    /// Another step this one reads the output of. <c>typecheck</c> and <c>build</c> generate the
    /// contract from the OpenAPI document <c>dotnet build</c> writes (#508), so they wait for it, or
    /// they would check the frontend against the document the last build left.
    /// </param>
    public static IResourceBuilder<T> WithScriptStep<T>(
        this IResourceBuilder<T> builder,
        string script,
        string stage,
        string? after = null
    )
        where T : IResource =>
        builder.WithPipelineStepFactory(factory => new PipelineStep
        {
            // Named for the script it runs, not the stage that requires it: `ci-lint-lint-frontend`
            // was what naming it after the stage produced.
            Name = WellKnownCIStepNames.For(script, factory.Resource.Name),
            Description = $"{factory.Resource.PackageManager()} run {script}",
            Action = Runs(factory.Resource, ["run", script]),
            DependsOnSteps = after is null ? [InstallStep(factory.Resource.Name)] : [InstallStep(factory.Resource.Name), after],
            RequiredBySteps = [stage],
        });

    static string InstallStep(string resource) =>
        WellKnownCIStepNames.For("install", resource);

    /// <summary>
    /// Binds the command to the resource's package manager and directory <b>now</b>, while the
    /// resource still has its annotations, and hands the step an action that closes over the two
    /// strings.
    /// </summary>
    static Func<PipelineStepContext, Task> Runs(IResource resource, string[] arguments)
    {
        var packageManager = resource.PackageManager();
        var workingDirectory = resource.WorkingDirectory();

        return context =>
            StepCommand.Run(
                packageManager,
                arguments,
                workingDirectory,
                context.Logger,
                context.CancellationToken
            );
    }
}
