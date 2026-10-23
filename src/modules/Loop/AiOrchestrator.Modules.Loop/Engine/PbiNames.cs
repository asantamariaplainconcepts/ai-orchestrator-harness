using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>What the engine calls a PBI in a request and in a sentence.</summary>
static class PbiNames
{
    /// <summary>The branch the claim pushes for a PBI, and the one its runs and scripts work on.</summary>
    public static string Branch(this Pbi pbi) => $"change/{pbi.Ref.Id}";

    public static string Named(this Pbi pbi) => $"{pbi.Ref.Repository}#{pbi.Ref.Id}";
}
