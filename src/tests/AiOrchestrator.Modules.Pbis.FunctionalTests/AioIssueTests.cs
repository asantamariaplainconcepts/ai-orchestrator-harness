using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>The host with a vendor held in memory that creates, edits, searches and lists children.</summary>
public sealed class AioIssueFixture : ApiServiceFixtureBase
{
    internal IssueVendor Vendor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPbiProvider>();
            services.AddSingleton<IPbiProvider>(Vendor);
            services.RemoveAll<IFolderDirectory>();
            services.AddSingleton<IFolderDirectory>(new OneFolder());
        });
    }
}

/// <summary>
/// #634. <c>.aio/aio issue</c> run for real through <see cref="AioScript"/>. What the
/// script prints is what an agent reads, and what the vendor holds afterwards is what the script did.
/// </summary>
public sealed class AioIssueTests(AioIssueFixture fixture) : IClassFixture<AioIssueFixture>, IDisposable
{
    const string Repo = IssueVendor.Repository;

    readonly string _checkout = Directory.CreateTempSubdirectory("aio-issue-").FullName;

    public void Dispose() => Directory.Delete(_checkout, recursive: true);

    [Fact]
    public async Task Create_with_a_parent_files_the_new_issue_under_it_and_answers_one_json_line()
    {
        fixture.Vendor.Arrange();
        var body = Body("issue.md", "## Qué pasa hoy\n\nNada.\n");

        var (code, line) = await Aio("issue", "create", "--title", "Una hija", "--body-file", body, "--parent", "1");

        code.ShouldBe(0, line);
        var created = fixture.Vendor.Created.ShouldHaveSingleItem();
        created.ShouldBe(("Una hija", "## Qué pasa hoy\n\nNada.\n", "1"));
        fixture.Vendor.Children["1"].ShouldContain("8", "the new issue is among the parent's children");

        using var answer = JsonDocument.Parse(line);
        var issue = answer.RootElement.GetProperty("issue");
        issue.GetProperty("repository").GetString().ShouldBe(Repo);
        issue.GetProperty("id").GetString().ShouldBe("8");
        issue.GetProperty("title").GetString().ShouldBe("Una hija");
        issue.GetProperty("parent").GetString().ShouldBe("1");
        issue.GetProperty("state").ValueKind.ShouldBe(JsonValueKind.Null, "a new issue carries no state: entering the loop is a person's decision");
        answer.RootElement.GetProperty("why").GetString().ShouldBe($"{Repo}#8 is created, under #1, with no state.");
        line.ShouldNotContain("\n", customMessage: "one JSON line");

        File.Exists(body).ShouldBeFalse("a body under .aio/ is removed once posted, so the worktree stays clean");
    }

    [Fact]
    public async Task View_list_children_edit_and_comment_go_through_the_folder_vendor()
    {
        fixture.Vendor.Arrange();

        var (viewed, view) = await Aio("issue", "view", "1");
        viewed.ShouldBe(0, view);
        using (var read = JsonDocument.Parse(view))
        {
            var issue = read.RootElement.GetProperty("issue");
            issue.GetProperty("title").GetString().ShouldBe("La épica");
            issue.GetProperty("state").GetString().ShouldBe("refinement");
            issue.GetProperty("body").GetString().ShouldBe("El cuerpo.");
            issue.GetProperty("children").EnumerateArray().Select(child => child.GetProperty("id").GetString()).ShouldBe(["7"]);
            issue.GetProperty("comments").EnumerateArray().Single().GetProperty("body").GetString().ShouldBe("Una pregunta.");
        }

        var (_, markdown) = await Aio("issue", "view", "1", "--markdown");
        markdown.ShouldStartWith("# La épica");
        markdown.ShouldContain("El cuerpo.");
        markdown.ShouldContain("**dev** at");
        markdown.ShouldContain("Una pregunta.");

        var (listed, list) = await Aio("issue", "list", "--search", "épica", "--state", "all");
        listed.ShouldBe(0, list);
        fixture.Vendor.Searched.ShouldBe([("épica", PbiSearchState.All)]);
        JsonDocument.Parse(list).RootElement.GetProperty("issues").GetArrayLength().ShouldBe(1);

        var (_, children) = await Aio("issue", "children", "1");
        JsonDocument.Parse(children).RootElement.GetProperty("issues").EnumerateArray().Single().GetProperty("id").GetString().ShouldBe("7");

        var (edited, edit) = await Aio("issue", "edit", "1", "--body-file", Body("body.md", "El cuerpo, cerrado."));
        edited.ShouldBe(0, edit);
        fixture.Vendor.Bodies["1"].ShouldBe("El cuerpo, cerrado.");

        var (commented, comment) = await Aio("issue", "comment", "1", "--body-file", Body("comment.md", "Listo para propuesta."));
        commented.ShouldBe(0, comment);
        fixture.Vendor.Commented.ShouldBe([("1", "Listo para propuesta.")]);
        JsonDocument.Parse(comment).RootElement.GetProperty("comment").GetProperty("id").GetString().ShouldBe("501");
    }

    [Fact]
    public async Task What_the_vendor_refuses_is_said_in_why_with_exit_1()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.LinkRefused = true;

        var (code, line) = await Aio("issue", "create", "--title", "Huérfana", "--body-file", Body("issue.md", "x"), "--parent", "1");

        code.ShouldBe(1, line);
        using var answer = JsonDocument.Parse(line);
        answer.RootElement.GetProperty("issue").ValueKind.ShouldBe(JsonValueKind.Null);
        answer.RootElement.GetProperty("why").GetString().ShouldBe($"aio: {Repo}#8 was created but is not among the children of #1.");
    }

    [Fact]
    public async Task Without_aio_api_it_says_what_is_missing_and_how_to_set_it()
    {
        var (code, line) = await Run(relay: false, ["issue", "view", "1"]);

        code.ShouldBe(2, line);
        var why = JsonDocument.Parse(line).RootElement.GetProperty("why").GetString()!;
        why.ShouldContain("AIO_API is not set");
        why.ShouldContain("export AIO_API=");
    }

    string Body(string name, string text)
    {
        var path = Path.Combine(_checkout, ".aio", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    Task<(int Code, string Said)> Aio(params string[] arguments) => Run(relay: true, arguments);

    Task<(int Code, string Said)> Run(bool relay, string[] arguments) =>
        AioScript.Run(relay ? fixture.CreateClient() : null, _checkout, Repo, arguments);
}

/// <summary>A repository of two issues, #1 the epic and #7 its child, that creates the next as #8.</summary>
sealed class IssueVendor : VendorStub, IPbiProvider
{
    public const string Repository = "owner/repo";

    readonly Dictionary<string, Pbi> _pbis = [];

    public Dictionary<string, List<string>> Children { get; } = [];

    public Dictionary<string, string> Bodies { get; } = [];

    public List<(string Title, string Body, string? Parent)> Created { get; } = [];

    public List<(string Text, PbiSearchState State)> Searched { get; } = [];

    public List<(string Id, string Body)> Commented { get; } = [];

    public bool LinkRefused { get; set; }

    public void Arrange()
    {
        _pbis.Clear();
        _pbis["1"] = new Pbi(new PbiRef(Repository, "1"), "La épica", "refinement", false, null);
        _pbis["7"] = new Pbi(new PbiRef(Repository, "7"), "Una hija", "ready-for-proposal", false, null);
        Children.Clear();
        Children["1"] = ["7"];
        Bodies.Clear();
        Bodies["1"] = "El cuerpo.";
        Created.Clear();
        Searched.Clear();
        Commented.Clear();
        LinkRefused = false;
    }

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<PullRequestInfo?>(null);

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(
            _pbis.TryGetValue(pbi.Id, out var found)
                ? new PbiThread(
                    found,
                    new Uri($"https://github.com/{Repository}/issues/{pbi.Id}"),
                    "dev",
                    DateTimeOffset.Parse("2026-10-11T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    Bodies.GetValueOrDefault(pbi.Id, ""),
                    [],
                    [],
                    null,
                    [],
                    [new PbiComment("101", "dev", DateTimeOffset.Parse("2026-10-11T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Una pregunta.")]
                )
                : null
        );

    public Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Pbi>>([.. Children.GetValueOrDefault(pbi.Id, []).Select(id => _pbis[id])]);

    public Task<Written<Pbi>> CreateAsync(string repository, string title, string body, string? parent, CancellationToken ct)
    {
        var id = "8";
        Created.Add((title, body, parent));
        _pbis[id] = new Pbi(new PbiRef(repository, id), title, null, false, null);
        Bodies[id] = body;
        if (parent is not null && !LinkRefused)
        {
            Children[parent].Add(id);
        }

        return Task.FromResult(
            parent is not null && LinkRefused
                ? new Written<Pbi>(_pbis[id], $"{repository}#{id} was created but is not among the children of #{parent}.")
                : new Written<Pbi>(_pbis[id], null)
        );
    }

    public Task<Written<string>> EditBodyAsync(PbiRef pbi, string body, CancellationToken ct)
    {
        Bodies[pbi.Id] = body;
        return Task.FromResult(new Written<string>(body, null));
    }

    public override Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct)
    {
        Searched.Add((query.Text!, query.State));
        return Task.FromResult<IReadOnlyList<Pbi>>([_pbis["1"]]);
    }

    public override Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct)
    {
        Commented.Add((pbi.Id, body));
        return Task.FromResult(new Written<PbiComment>(new PbiComment("501", "aio-bot", DateTimeOffset.UtcNow, body), null));
    }

    // What `aio issue` never asks.
}
