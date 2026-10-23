using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #680. The requirements screen's rows, through <c>GET /api/machine/tools</c>, from probes the test
/// sets: every requirement present, missing with this machine's install command, or signed out with
/// the command that signs it in; and the preference that stops the screen opening on start.
/// <para>
/// One host for the class, as <see cref="ApiServiceFixtureBase.ClassHost{TClass}"/> keeps it, so the
/// fakes read the state of the test running now.
/// </para>
/// </summary>
[Collection(WorkspaceCollection.Name)]
public sealed class RequirementsTests(WorkspaceFixture fixture) : IAsyncLifetime
{
    static readonly FakeMachine Machine = new();

    public Task InitializeAsync() => fixture.ResetDatabase();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Everything_present_is_ready_and_offers_no_command()
    {
        var tools = await Tools("present");

        Rows(tools).ShouldBe(
        [
            "git present required",
            "gh present required",
            "node present required",
            "claude present agent",
            "sbx present optional",
            "podman present optional",
        ]);
        Commands(tools).ShouldAllBe(command => command == null);
        tools.GetProperty("ready").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Everything_missing_carries_this_machines_install_command_and_its_page()
    {
        var tools = await Tools("missing");

        Rows(tools).ShouldBe(
        [
            "git missing required",
            "gh missing required",
            "node missing required",
            "claude missing agent",
            "sbx missing optional",
            "podman missing optional",
        ]);
        Commands(tools).ShouldBe(["install git", "install gh", "install node", "install claude", "install sbx", "install podman"]);
        tools.GetProperty("requirements")[0].GetProperty("link").GetString().ShouldBe("https://example.com/git");
        tools.GetProperty("ready").GetBoolean().ShouldBeFalse();
    }

    // git and node have no session; the runtime that does not answer is missing and is started; sbx
    // says its own remedy.
    [Fact]
    public async Task Signed_out_carries_the_command_that_signs_in()
    {
        var tools = await Tools("signed-out");

        Rows(tools).ShouldBe(
        [
            "git present required",
            "gh signed-out required",
            "node present required",
            "claude signed-out agent",
            "sbx signed-out optional",
            "podman missing optional",
        ]);
        Commands(tools).ShouldBe([null, "sign in gh", null, "sign in claude", "sbx login", "sign in podman"]);
        tools.GetProperty("requirements")[1].GetProperty("problem").GetString().ShouldBe("gh is not signed in.");
        tools.GetProperty("ready").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task The_requirements_screen_is_seen_once_and_stays_seen()
    {
        using var client = fixture.CreateClient();

        (await client.GetFromJsonAsync<JsonElement>("/api/preferences")).GetProperty("requirementsSeen").GetBoolean().ShouldBeFalse();

        (await client.PutAsJsonAsync("/api/preferences", new { requirementsSeen = true })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/preferences", new { editor = "cursor" })).EnsureSuccessStatusCode();

        var stored = await client.GetFromJsonAsync<JsonElement>("/api/preferences");
        stored.GetProperty("requirementsSeen").GetBoolean().ShouldBeTrue();
        stored.GetProperty("editor").GetString().ShouldBe("cursor");
    }

    async Task<JsonElement> Tools(string state)
    {
        Machine.State = state;
        using var client = fixture
            .ClassHost<RequirementsTests>(services =>
            {
                services.RemoveAll<IAgentProvider>();
                services.AddSingleton<IAgentProvider>(new Agent());
                services.RemoveAll<IPbiProvider>();
                services.AddSingleton<IPbiProvider>(new Vendor());
                services.RemoveAll<IEnvironment>();
                services.AddSingleton<IEnvironment>(new Sandbox());
                services.RemoveAll<IMachineProbe>();
                services.AddSingleton<IMachineProbe>(Machine);
            })
            .CreateClient();

        return await client.GetFromJsonAsync<JsonElement>("/api/machine/tools");
    }

    static IEnumerable<string> Rows(JsonElement tools) =>
        tools.GetProperty("requirements").EnumerateArray().Select(row =>
            $"{row.GetProperty("name").GetString()} {row.GetProperty("state").GetString()} "
            + (row.GetProperty("agent").GetBoolean() ? "agent" : row.GetProperty("required").GetBoolean() ? "required" : "optional"));

    static IEnumerable<string?> Commands(JsonElement tools) =>
        tools.GetProperty("requirements").EnumerateArray().Select(row =>
            row.GetProperty("command") is { ValueKind: JsonValueKind.String } command ? command.GetString() : null);

    /// <summary>
    /// A machine where every CLI that can be is in one state. The container runtime has no session:
    /// installed and not answering, it is reported with why, which the screen calls missing.
    /// </summary>
    sealed class FakeMachine : IMachineProbe
    {
        public string State { get; set; } = "present";

        public CliProbe Probe(string cli) =>
            State switch
            {
                "missing" => new CliProbe(cli, null, null),
                "signed-out" => new CliProbe(cli, $"/opt/bin/{cli}", "1.0", Problem: $"{cli} is not signed in."),
                _ => new CliProbe(cli, $"/opt/bin/{cli}", "1.0", Account: "octocat"),
            };

        public Task<CliProbe> ProbeAsync(string cli, CancellationToken ct) =>
            Task.FromResult(Probe(cli) with { Problem = null });

        public Task<CliProbe> ContainerRuntimeAsync(CancellationToken ct) => Task.FromResult(Probe("podman"));

        public MachineRemedy Install(string cli) => new($"install {cli}", $"https://example.com/{cli}");

        public string? MakeReady(string cli) => $"sign in {cli}";
    }

    sealed class Agent : IAgentProvider
    {
        public string Name => "claude-code";

        public SessionOptions Launch(AgentRequest request) => throw new NotSupportedException();

        public Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(Machine.Probe("claude"));
    }

    sealed class Vendor : VendorStub, IPbiProvider
    {
        public override Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(Machine.Probe("gh"));
    }

    /// <summary>The sandbox environment as sbx answers: ready, or signed out of Docker.</summary>
    sealed class Sandbox : IEnvironment
    {
        public string Name => "sbx";

        public AgentPermissions? Permissions => null;

        public Task<EnvironmentReadiness> ReadinessAsync(CancellationToken ct) =>
            Task.FromResult(Machine.State == "present" ? EnvironmentReadiness.Ready : new EnvironmentReadiness(false, "sbx did not answer.", "sbx login"));

        public Task<RunPlace> PrepareAsync(PlaceRequest request, CancellationToken ct) => throw new NotSupportedException();

        public SessionOptions Enter(RunPlace place, SessionOptions launch) => throw new NotSupportedException();

        public Task RetireAsync(RunPlace place, CancellationToken ct) => throw new NotSupportedException();
    }
}
