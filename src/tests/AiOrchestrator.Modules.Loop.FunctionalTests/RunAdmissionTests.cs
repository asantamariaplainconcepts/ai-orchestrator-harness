using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Engine;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #667. Whether a run can start here now, decided on its own against Runs and the agent adapter
/// held in memory: the live count the WIP limit reads, and a CLI with no adapter. The environment's
/// answer (#614) and this machine's trust (#606) are SandboxPlacementTests' and UntrustedFolderTests'
/// through a pass (#668).
/// </summary>
public sealed class RunAdmissionTests
{
    static readonly RunRequest Request = new(
        new PbiRef(FakeFolders.Repository, "1"),
        "ready-for-proposal",
        "/tmp/folder",
        "change/1",
        "claude-code",
        null,
        "/aio:refine 1",
        ["ready", "gaps"]
    );

    readonly FakeRuns _runs = new(new FakeScripts());
    readonly FakeAgent _agent = new();

    [Fact]
    public async Task It_counts_the_live_runs_once_and_one_more_for_each_it_starts()
    {
        _runs.Elsewhere = 2;
        var admission = new RunAdmission(_runs, [_agent]);

        await admission.CountLiveAsync(CancellationToken.None);
        admission.Live.ShouldBe(2);

        var id = await admission.StartAsync(Request, CancellationToken.None);

        admission.Live.ShouldBe(3);
        _runs.Started.Single().Status.RunId.ShouldBe(id);
        _runs.Started.Single().Request.ShouldBe(Request);
    }

    [Fact]
    public async Task A_trusted_folder_and_a_CLI_with_no_adapter_both_start()
    {
        (await new RunAdmission(_runs, [_agent]).WaitsAsync(Request, CancellationToken.None)).ShouldBeNull();
        (await new RunAdmission(_runs).WaitsAsync(Request, CancellationToken.None)).ShouldBeNull();
    }
}
