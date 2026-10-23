using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Pbis;

/// <summary>
/// The PBI as the vendor has it: the list, the issue with its comments, the proposal and the review
/// of its pull request, read and written through <c>IPbiProvider</c>.
/// <para>
/// <b>Nothing here is stored.</b> The vendor owns the PBI (principle 1 of README.md),
/// so this module has no table of its own and registers nothing beyond its use cases.
/// </para>
/// </summary>
public sealed class PbisModule : ModuleBase
{
    public override string Name => "Pbis";

    public override void Add(IServiceCollection services, IConfiguration configuration) { }
}
