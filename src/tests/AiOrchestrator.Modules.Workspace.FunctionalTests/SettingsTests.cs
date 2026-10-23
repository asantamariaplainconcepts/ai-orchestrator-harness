using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #515. What Settings reads and changes, through the routes it calls: a folder added from the
/// rail is listed with what it lacks for the loop, its defaults are stored and read back, and the
/// machine's CLIs are asked of the adapters, a missing one reported as missing.
/// <para>
/// The folders are real temporary directories, because what a row lacks is read from disk.
/// </para>
/// </summary>
[Collection(WorkspaceCollection.Name)]
public sealed class SettingsTests(WorkspaceFixture fixture) : IAsyncLifetime
{
    readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("settings-");

    public Task InitializeAsync() => fixture.ResetDatabase();

    public Task DisposeAsync()
    {
        _directory.Delete(recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_folder_added_from_the_rail_is_listed_with_what_it_lacks_until_the_checkout_has_it()
    {
        fixture.Probe.State = null;
        using var client = fixture.CreateClient();

        var added = await client.PostAsJsonAsync("/api/folders", new { path = _directory.FullName });

        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var row = await Row(client, id);
        row.GetProperty("loopYaml").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("hasGate").GetBoolean().ShouldBeFalse();
        row.GetProperty("vendor").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("defaultAgent").GetString().ShouldBe("claude-code");
        row.GetProperty("defaultModel").ValueKind.ShouldBe(JsonValueKind.Null);

        var harness = Directory.CreateDirectory(Path.Combine(_directory.FullName, ".aio"));
        await File.WriteAllTextAsync(Path.Combine(harness.FullName, "loop.yaml"), "name: notes\n");
        await File.WriteAllTextAsync(Path.Combine(harness.FullName, "gate"), "#!/bin/sh\n");

        row = await Row(client, id);
        row.GetProperty("loopYaml").GetString().ShouldBe("name: notes\n");
        row.GetProperty("hasGate").GetBoolean().ShouldBeTrue();

        var again = await client.PostAsJsonAsync("/api/folders", new { path = _directory.FullName });
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var removed = await client.DeleteAsync($"/api/folders/{id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<JsonElement[]>("/api/folders"))!.ShouldNotContain(entry => entry.GetProperty("id").GetGuid() == id);
        Directory.Exists(_directory.FullName).ShouldBeTrue("removing a folder touches nothing on disk");
    }

    [Fact]
    public async Task A_folders_default_agent_and_model_are_stored_and_read_back()
    {
        var id = await fixture.Folder(_directory.FullName);
        using var client = fixture.CreateClient();

        var saved = await client.PutAsJsonAsync($"/api/folders/{id}/defaults", new { agent = "opencode", model = " gpt-6-sol " });

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = await Row(client, id);
        row.GetProperty("defaultAgent").GetString().ShouldBe("opencode");
        row.GetProperty("defaultModel").GetString().ShouldBe("gpt-6-sol");

        // #546. The loop reads them too, for a state that names no agent.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var walked = await scope.ServiceProvider.GetRequiredService<IFolderDirectory>().ListAsync(CancellationToken.None);
            walked.ShouldHaveSingleItem().ShouldBe(new LoopFolder(_directory.FullName, "github", "example/portal", "opencode", "gpt-6-sol"));
        }

        (await client.PutAsJsonAsync($"/api/folders/{id}/defaults", new { agent = "claude-code", model = "" })).EnsureSuccessStatusCode();
        (await Row(client, id)).GetProperty("defaultModel").ValueKind.ShouldBe(JsonValueKind.Null);

        (await client.PutAsJsonAsync($"/api/folders/{id}/defaults", new { agent = "claude-code", model = "two words" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/folders/{Guid.NewGuid()}/defaults", new { agent = "claude-code" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_clones_of_one_repository_are_one_folder_for_the_loop_the_first_by_path()
    {
        var second = _directory.CreateSubdirectory("b-clone").FullName;
        var first = _directory.CreateSubdirectory("a-clone").FullName;
        await fixture.Folder(second);
        await fixture.Folder(first);

        await using var scope = fixture.Services.CreateAsyncScope();
        var walked = await scope.ServiceProvider.GetRequiredService<IFolderDirectory>().ListAsync(CancellationToken.None);

        walked.ShouldHaveSingleItem().Path.ShouldBe(first);
        walked[0].Repository.ShouldBe("example/portal");
    }

    [Fact]
    public async Task Open_in_editor_opens_a_file_inside_the_folder_and_nothing_outside_it()
    {
        var id = await fixture.Folder(_directory.FullName);
        using var client = fixture.CreateClient();

        var opened = await client.PostAsJsonAsync($"/api/folders/{id}/open-in-editor", new { file = ".aio/loop.yaml" });

        opened.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await opened.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("opened").GetString().ShouldBe(Path.Combine(_directory.FullName, ".aio/loop.yaml"));
        body.GetProperty("editor").GetString().ShouldBe("code");

        (await client.PostAsJsonAsync($"/api/folders/{id}/open-in-editor", new { file = "../elsewhere" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"/api/folders/{id}/open-in-editor", new { file = "/etc/hosts" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_editor_is_changed_and_read_back()
    {
        using var client = fixture.CreateClient();

        (await client.PutAsJsonAsync("/api/preferences", new { editor = "cursor" })).EnsureSuccessStatusCode();

        (await client.GetFromJsonAsync<JsonElement>("/api/preferences")).GetProperty("editor").GetString().ShouldBe("cursor");
        (await client.PutAsJsonAsync("/api/preferences", new { editor = "code; rm -rf ~" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_machine_lists_each_adapters_CLI_and_one_not_on_the_PATH_as_missing()
    {
        using var client = fixture
            .ClassHost<SettingsTests>(services =>
            {
                services.RemoveAll<IAgentProvider>();
                services.AddSingleton<IAgentProvider>(new ProbedAgent("claude-code", new CliProbe("claude", "/opt/bin/claude", "2.1.4")));
                services.AddSingleton<IAgentProvider>(new ProbedAgent("opencode", new CliProbe("opencode", null, null)));
                services.RemoveAll<IPbiProvider>();
                services.AddSingleton<IPbiProvider>(new SignedOutVendor());
            })
            .CreateClient();

        var tools = await client.GetFromJsonAsync<JsonElement>("/api/machine/tools");

        var agents = tools.GetProperty("agents").EnumerateArray().ToArray();
        agents.Select(agent => agent.GetProperty("adapter").GetString()).ShouldBe(["claude-code", "opencode"]);
        agents[0].GetProperty("path").GetString().ShouldBe("/opt/bin/claude");
        agents[0].GetProperty("version").GetString().ShouldBe("2.1.4");
        agents[1].GetProperty("path").ValueKind.ShouldBe(JsonValueKind.Null);

        var vendor = tools.GetProperty("vendors").EnumerateArray().Single();
        vendor.GetProperty("cli").GetString().ShouldBe("gh");
        vendor.GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
        vendor.GetProperty("problem").GetString().ShouldBe("You are not logged into any GitHub hosts.");
    }

    static async Task<JsonElement> Row(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<JsonElement[]>("/api/folders"))!.Single(entry => entry.GetProperty("id").GetGuid() == id);

    sealed class ProbedAgent(string name, CliProbe probe) : IAgentProvider
    {
        public string Name => name;

        public SessionOptions Launch(AgentRequest request) => throw new NotSupportedException();

        public Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(probe);
    }

    /// <summary>A vendor whose CLI is installed and holds no credential, the case Settings names.</summary>
    sealed class SignedOutVendor : VendorStub, IPbiProvider
    {
        public override Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(new CliProbe("gh", "/opt/bin/gh", "2.99.0", Problem: "You are not logged into any GitHub hosts."));
    }
}
