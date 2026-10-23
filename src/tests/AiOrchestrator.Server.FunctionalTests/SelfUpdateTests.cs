using AiOrchestrator.Server.Installed;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>#703. Update and restart updates the tool where it is installed, and nowhere else.</summary>
public sealed class SelfUpdateTests
{
    [Fact]
    public void The_global_tools_folder_is_updated_with_g()
    {
        SelfUpdate.UpdateArguments("/Users/someone/.dotnet/tools", "/Users/someone").ShouldBe(["tool", "update", "-g", "aio"]);
    }

    [Fact]
    public void Another_tool_path_is_updated_there_and_not_globally()
    {
        SelfUpdate.UpdateArguments("/tmp/check/tools", "/Users/someone").ShouldBe(["tool", "update", "--tool-path", "/tmp/check/tools", "aio"]);
    }

    [Fact]
    public void A_restart_under_the_service_is_an_exit_launchd_and_systemd_restart()
    {
        // EX_TEMPFAIL: not a success, so KeepAlive's SuccessfulExit false and Restart=on-failure apply.
        SelfUpdate.RestartExitCode.ShouldBe(75);
        SelfUpdate.ExitCode.ShouldBe(0);
    }
}
