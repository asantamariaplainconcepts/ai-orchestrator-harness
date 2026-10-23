using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.BuildingBlocks.Folders;

/// <summary>
/// Whether a folder carries this product's harness, read from <c>.aio/version</c>, which the
/// install writes. What the app writes to a repository's vendor on its own, the evidence branch and
/// the journey comment (#697), waits for it: a client's repository opened as a folder only to look,
/// or to run a terminal, gets nothing pushed and nothing commented.
/// </summary>
public static class HarnessInstall
{
    /// <summary>Read now, from the folder's checkout as it is on disk.</summary>
    public static bool In(string folderPath) => File.Exists(HarnessScripts.Own(LocalPath.Expand(folderPath), "version"));
}
