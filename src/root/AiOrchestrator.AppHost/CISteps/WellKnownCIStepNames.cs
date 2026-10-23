namespace AiOrchestrator.AppHost.CISteps;

/// <summary>
/// The gate's name, the four stages it is made of, and the prefix everything here wears.
/// <para>
/// <b>Every name is prefixed <c>ci-</c>, and that is not decoration.</b> Aspire's pipeline is one
/// flat namespace shared with the steps Aspire adds itself, and it adds a per-resource one for
/// every project: a stage called <c>build</c> threw <i>a step with the name 'build' has already
/// been added to the pipeline</i>, and renaming it did not help, because a step called
/// <c>build-server</c> then collided with Aspire's own build step for the <c>server</c> resource —
/// <i>duplicate step name: 'build-server'</i>, this time at execution rather than composition.
/// Prefixing takes the whole class of that away, including the collisions a later Aspire version
/// would introduce.
/// </para>
/// <para>
/// Four stages, not the five upstream carries. <c>setup</c> is where
/// <see href="https://github.com/andonisan/aspire-pipelines">aspire-pipelines</see> puts machine
/// prerequisites — <c>winget</c>, <c>apt install</c> — and this repository has none: the .NET SDK,
/// pnpm and Docker are the README's prerequisites, and a stage that only ever logs "completed
/// successfully" is a step somebody eventually believes is checking something.
/// </para>
/// </summary>
static class WellKnownCIStepNames
{
    /// <summary>The one command that is the gate. The only name here without the prefix.</summary>
    public const string Ci = "ci";

    /// <summary>Fetching dependencies: <c>dotnet restore</c>, <c>pnpm install</c>.</summary>
    public const string Install = "ci-install";

    /// <summary>Compiling: <c>dotnet build</c>, <c>vite build</c>.</summary>
    public const string Compile = "ci-compile";

    /// <summary>Static checks that read the source rather than run it: ESLint, <c>tsc</c>.</summary>
    public const string Lint = "ci-lint";

    /// <summary>Running the suite.</summary>
    public const string Test = "ci-test";

    /// <summary>
    /// The name of a step this repository owns for one resource — <c>ci-test-server</c>,
    /// <c>ci-lint-frontend</c>. These are an interface, not bookkeeping: running one gate without
    /// the others is <c>aspire do ci-test-server</c>, and that has to be guessable from the outside.
    /// </summary>
    public static string For(string verb, string resource) => $"ci-{verb}-{resource}";
}
