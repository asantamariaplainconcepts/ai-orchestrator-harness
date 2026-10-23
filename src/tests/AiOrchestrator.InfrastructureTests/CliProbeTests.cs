using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #515. What Settings shows under "On this machine": each adapter's CLI, found where its own spawn
/// would find it and asked its version, or reported as not on the PATH.
/// </summary>
public sealed class CliProbeTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-cli-probe-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task An_agent_CLI_that_is_not_on_the_PATH_is_reported_as_missing()
    {
        var probe = await new ClaudeCodeProvider("aio-no-such-agent-cli").ProbeAsync(CancellationToken.None);

        probe.Cli.ShouldBe("aio-no-such-agent-cli");
        probe.Path.ShouldBeNull();
        probe.Version.ShouldBeNull();
    }

    [Fact]
    public async Task An_agent_CLI_that_is_there_says_where_and_which_version()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var cli = Path.Combine(_root, "claude");
        await File.WriteAllTextAsync(cli, "#!/bin/sh\necho '2.1.4 (Claude Code)'\n");
        File.SetUnixFileMode(cli, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var probe = await new ClaudeCodeProvider(cli).ProbeAsync(CancellationToken.None);

        probe.ShouldBe(new CliProbe("claude", cli, "2.1.4"));
    }

    [Fact]
    public async Task The_GitHub_adapter_names_gh_whether_or_not_this_machine_has_it()
    {
        var probe = await new GitHubPbiProvider().ProbeAsync(CancellationToken.None);

        probe.Cli.ShouldBe("gh");
        (probe.Path is null || File.Exists(probe.Path)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("gh version 2.99.0 (2026-09-30)\nhttps://github.com/cli/cli/releases/tag/v2.99.0\n", "2.99.0")]
    [InlineData("2.1.4 (Claude Code)\n", "2.1.4")]
    [InlineData("opencode 0.9.2-beta.1\n", "0.9.2-beta.1")]
    [InlineData("GitHub Copilot CLI 1.0.95-2.\nRun 'copilot update' to check for updates.\n", "1.0.95-2")]
    [InlineData("no number here\n", null)]
    public void The_version_is_the_first_number_printed(string printed, string? version) =>
        CliProbing.Version(printed).ShouldBe(version);

    // #680. What `claude auth status` prints on 2.1.x, and what an older CLI without it prints.
    [Theory]
    [InlineData("""{"loggedIn": true, "authMethod": "claude.ai"}""", null)]
    [InlineData("""{"loggedIn": false}""", "Claude Code is not signed in.")]
    [InlineData("2.1.4 (Claude Code)\n", null)]
    [InlineData("42", null)]
    [InlineData(null, null)]
    public void Claude_is_signed_out_only_when_its_status_says_so(string? printed, string? problem) =>
        ClaudeCodeProvider.SignedOut(printed).ShouldBe(problem);

    [Theory]
    [InlineData("ID=ubuntu\nID_LIKE=debian\n", "apt")]
    [InlineData("ID=\"rocky\"\nID_LIKE=\"rhel centos fedora\"\n", "dnf")]
    [InlineData("ID=fedora\n", "dnf")]
    [InlineData("ID=arch\n", "pacman")]
    [InlineData("ID=nixos\n", null)]
    public void A_linux_distribution_is_told_by_its_os_release(string release, string? manager) =>
        MachineProbe.FromOsRelease(release).ShouldBe(manager);

    [Theory]
    [InlineData("brew", "gh", "brew install gh")]
    [InlineData("winget", "gh", "winget install GitHub.cli")]
    [InlineData("apt", "node", "sudo apt install nodejs")]
    [InlineData("pacman", "gh", "sudo pacman -S github-cli")]
    [InlineData("brew", "claude", "brew install --cask claude-code")]
    [InlineData("winget", "claude", "npm install -g @anthropic-ai/claude-code")]
    [InlineData(null, "git", null)]
    [InlineData("brew", "sbx", null)]
    public void The_install_command_is_the_package_managers(string? manager, string cli, string? command)
    {
        var remedy = new MachineProbe(manager).Install(cli);

        remedy.Command.ShouldBe(command);
        remedy.Link.ShouldStartWith("https://");
    }

    [Fact]
    public void Podman_is_started_where_it_runs_in_a_machine()
    {
        new MachineProbe("brew").MakeReady("podman").ShouldBe("podman machine start");
        new MachineProbe("apt").MakeReady("podman").ShouldBeNull();
        new MachineProbe("apt").MakeReady("gh").ShouldBe("gh auth login");
    }

    [Fact]
    public async Task Git_on_this_machine_is_found_with_its_version()
    {
        var probe = await new MachineProbe().ProbeAsync("git", CancellationToken.None);

        probe.Path.ShouldNotBeNull();
        probe.Version.ShouldNotBeNull();
    }
}
