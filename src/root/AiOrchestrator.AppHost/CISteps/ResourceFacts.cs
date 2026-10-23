using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;

namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// What a step needs to know about the resource it hangs off, read from the resource's annotations
/// rather than repeated as a literal beside it.
/// <para>
/// <b>A pipeline step gets an <see cref="IResource"/>, not the type it was declared on.</b> By the
/// time a step factory runs, the <c>IResourceBuilder&lt;ViteAppResource&gt;</c> that
/// <c>AppHost.cs</c> called this on is gone and only the annotations survive. So these read
/// annotations, and they are read once in the factory — not inside the step's action — so that a
/// resource declared wrong fails while the pipeline is being assembled rather than four minutes
/// into it.
/// </para>
/// </summary>
static class ResourceFacts
{
    /// <summary>
    /// The directory the resource runs in. For an executable-backed resource — which is what a
    /// Vite app is — this is the folder holding its <c>package.json</c>.
    /// </summary>
    public static string WorkingDirectory(this IResource resource) =>
        resource.TryGetLastAnnotation<ExecutableAnnotation>(out var executable)
            ? executable.WorkingDirectory
            : throw new InvalidOperationException(
                $"{resource.Name} has no working directory to run a CI step in"
            );

    /// <summary>
    /// The package manager the AppHost declared for a JavaScript resource — <c>pnpm</c> here,
    /// because <c>AppHost.cs</c> says <c>WithPnpm()</c>.
    /// <para>
    /// Read rather than hard-coded, so the two cannot drift. A gate that runs <c>npm</c> against a
    /// <c>pnpm-lock.yaml</c> does not fail: it succeeds, having installed a different dependency
    /// tree than the one the app runs on.
    /// </para>
    /// </summary>
    public static string PackageManager(this IResource resource) =>
        resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var manager)
            ? manager.ExecutableName
            : throw new InvalidOperationException(
                $"{resource.Name} declares no package manager — say WithPnpm() on it"
            );
}
