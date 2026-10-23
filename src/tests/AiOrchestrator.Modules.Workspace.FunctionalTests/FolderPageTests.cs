using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Features.Harness;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #565. What the folder page reads: the checkout's loop file, its harness version against the one
/// this build carries, each file of the package and whether the checkout has it, read from disk on
/// every request so an edit shows on the next poll; and the states its repository already has at the
/// vendor, asked of the adapter.
/// </summary>
[Collection(WorkspaceCollection.Name)]
public sealed class FolderPageTests(WorkspaceFixture fixture) : IAsyncLifetime
{
    readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("folder-page-");

    public Task InitializeAsync() => fixture.ResetDatabase();

    public Task DisposeAsync()
    {
        _directory.Delete(recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_page_reads_the_checkout_again_on_every_request()
    {
        var id = await fixture.Folder(_directory.FullName);
        fixture.Probe.State = new CheckoutState("feature/x", "https://github.com/example/portal.git");
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}");

        page.GetProperty("path").GetString().ShouldBe(_directory.FullName);
        page.GetProperty("vendor").GetString().ShouldBe("github");
        page.GetProperty("repository").GetString().ShouldBe("example/portal");
        page.GetProperty("branch").GetString().ShouldBe("feature/x");
        page.GetProperty("defaultAgent").GetString().ShouldBe("claude-code");
        page.GetProperty("loopYaml").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("harnessVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("shippedVersion").GetString().ShouldBe(HarnessPackage.Version);
        var files = page.GetProperty("harness").EnumerateArray().ToList();
        files.Select(file => file.GetProperty("path").GetString()).ShouldBe(HarnessPackage.Files.Select(file => file.Path));
        files.ShouldAllBe(file => !file.GetProperty("present").GetBoolean());

        var harness = Directory.CreateDirectory(Path.Combine(_directory.FullName, ".aio"));
        await File.WriteAllTextAsync(Path.Combine(harness.FullName, "loop.yaml"), "name: notes\n");
        await File.WriteAllTextAsync(Path.Combine(harness.FullName, "version"), "1\nthe rest is ignored\n");
        var stub = HarnessPackage.Files.Single(file => file.Path == ".aio/gate").Content;
        await File.WriteAllBytesAsync(Path.Combine(harness.FullName, "gate"), stub);
        Directory.CreateDirectory(Path.Combine(_directory.FullName, ".claude"));
        File.CreateSymbolicLink(Path.Combine(_directory.FullName, ".claude", "commands"), "../.ai/commands");

        page = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}");

        page.GetProperty("loopYaml").GetString().ShouldBe("name: notes\n");
        page.GetProperty("harnessVersion").GetString().ShouldBe("1");
        Row(page, ".aio/gate").GetProperty("present").GetBoolean().ShouldBeTrue();
        Row(page, ".aio/gate").GetProperty("stub").GetBoolean().ShouldBeTrue();
        Row(page, ".claude/commands").GetProperty("present").GetBoolean().ShouldBeTrue("a link counts even when it points at nothing yet");
        Row(page, ".aio/version").GetProperty("stub").GetBoolean().ShouldBeFalse();

        // Broken in an editor: the next read has it, with no cache in between.
        await File.WriteAllTextAsync(Path.Combine(harness.FullName, "loop.yaml"), "states: [\n");
        await File.AppendAllTextAsync(Path.Combine(harness.FullName, "gate"), "# ours now\n");

        page = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}");

        page.GetProperty("loopYaml").GetString().ShouldBe("states: [\n");
        Row(page, ".aio/gate").GetProperty("stub").GetBoolean().ShouldBeFalse();

        (await client.GetAsync($"/api/folders/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_states_are_asked_of_the_vendor_and_a_refusal_is_said_not_thrown()
    {
        var vendor = new StatesVendor();
        var id = await fixture.Folder(_directory.FullName);
        fixture.RemoteUrl = null;
        var plain = await fixture.Folder(Path.Combine(_directory.FullName, "plain"));
        using var client = fixture
            .ClassHost<FolderPageTests>(services =>
            {
                services.RemoveAll<IPbiProvider>();
                services.AddSingleton<IPbiProvider>(vendor);
            })
            .CreateClient();

        var states = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}/states");

        states.GetProperty("vendor").GetString().ShouldBe("github");
        states.GetProperty("states").EnumerateArray().Select(state => state.GetString()).ShouldBe(["in-progress", "verifying"]);
        vendor.Asked.ShouldBe(["example/portal"]);

        vendor.Refusal = "HTTP 401: Bad credentials";
        states = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}/states");
        states.GetProperty("states").ValueKind.ShouldBe(JsonValueKind.Null);
        states.GetProperty("problem").GetString().ShouldBe("HTTP 401: Bad credentials");

        states = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{plain}/states");
        states.GetProperty("vendor").ValueKind.ShouldBe(JsonValueKind.Null);
        states.GetProperty("problem").GetString().ShouldBe("Its origin is not on github.com, so there is no vendor to ask.");
        vendor.Asked.Count.ShouldBe(2, "a folder with no vendor asks none");
    }

    [Fact]
    public async Task The_page_says_whether_each_agent_that_asks_trusts_the_folder()
    {
        var id = await fixture.Folder(_directory.FullName);
        using var client = fixture
            .ClassHost<UntrustingAgent>(services =>
            {
                services.RemoveAll<IAgentProvider>();
                services.AddSingleton<IAgentProvider>(new UntrustingAgent());
                services.AddSingleton<IAgentProvider>(new UntrustingAgent("opencode", asks: false));
            })
            .CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}");

        // #606: one row per CLI that asks; the one that asks nothing has none.
        var trust = page.GetProperty("trust").EnumerateArray().Single();
        trust.GetProperty("provider").GetString().ShouldBe("claude-code");
        trust.GetProperty("trusted").GetBoolean().ShouldBeFalse();
        trust.GetProperty("why").GetString().ShouldBe($"Not trusted: {_directory.FullName}");
        trust.GetProperty("remedy").GetString().ShouldBe($"cd {_directory.FullName} && claude");
    }

    /// <summary>An adapter that answers only whether it trusts a folder: never, or not a question it asks.</summary>
    sealed class UntrustingAgent(string name = "claude-code", bool asks = true) : IAgentProvider
    {
        public string Name => name;

        public AgentTrust? Trust(string folder) => asks ? new(false, $"Not trusted: {folder}", $"cd {folder} && claude") : null;

        public SessionOptions Launch(AgentRequest request) => throw new NotSupportedException();

        public Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<CliProbe> ProbeAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    static JsonElement Row(JsonElement page, string path) =>
        page.GetProperty("harness").EnumerateArray().Single(file => file.GetProperty("path").GetString() == path);

    /// <summary>A vendor that answers only the one question the page asks it.</summary>
    sealed class StatesVendor : VendorStub, IPbiProvider
    {
        public List<string> Asked { get; } = [];

        public string? Refusal { get; set; }

        public Task<IReadOnlyList<string>> ListStatesAsync(string repository, CancellationToken ct)
        {
            Asked.Add(repository);
            return Refusal is { } said
                ? throw new PbiProviderException(said)
                : Task.FromResult<IReadOnlyList<string>>(["in-progress", "verifying"]);
        }
    }
}
