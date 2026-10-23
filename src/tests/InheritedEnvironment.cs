using System.Runtime.CompilerServices;

namespace AiOrchestrator.Tests;

/// <summary>
/// Every test assembly starts without the <c>AIO_</c> variables of the process that runs the gate
/// (#762). The installed service runs the gate with <c>AIO_SERVICE</c> set, and an agent's session
/// with <c>AIO_API</c>, <c>AIO_REPOSITORY</c> and <c>AIO_DONE_FILE</c>: the server a test hosts and
/// every process a test starts read them as their own. Linked into each test project by
/// <c>src/Directory.Build.props</c>, so a new one is covered without remembering to.
/// <para>
/// The variables a person sets to opt into a test against something real are not the product's,
/// and stay.
/// </para>
/// </summary>
static class InheritedEnvironment
{
    static readonly string[] OptIns = ["AIO_REAL_AGENTS", "AIO_PBI_TEST_"];

#pragma warning disable CA2255 // A test assembly is the one library whose load is the start of every test in it.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Forget()
    {
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (name.StartsWith("AIO_", StringComparison.Ordinal)
                && !OptIns.Any(optIn => name.StartsWith(optIn, StringComparison.Ordinal)))
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }
}
