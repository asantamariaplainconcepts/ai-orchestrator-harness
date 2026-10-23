using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.SharedFunctionalTests;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// A vendor held in memory: the PBIs of one repository, and every write the engine made on them.
/// Writes read back as written, as <c>GitHubPbiProvider</c>'s do when the vendor keeps them.
/// </summary>
sealed class FakeVendor : VendorStub, IPbiProvider
{
    public const string Me = "aio-bot";

    public List<Pbi> Pbis { get; } = [];

    public List<string> Writes { get; } = [];

    public Dictionary<string, PullRequestInfo> Pulls { get; } = [];

    /// <summary>Each repository whose list fails, with what the CLI said, as the adapter throws it.</summary>
    public Dictionary<string, string> Unreachable { get; } = [];

    public override Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct) =>
        Unreachable.TryGetValue(repository, out var said)
            ? throw new PbiProviderException(said)
            : Task.FromResult<IReadOnlyList<Pbi>>(
                [.. Pbis.Where(pbi => pbi.Ref.Repository == repository && (query.State == PbiSearchState.All || pbi.Closed == (query.State == PbiSearchState.Closed))).Take(query.Limit)]
            );

    public override Task<CliProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(new CliProbe("gh", "/usr/local/bin/gh", "2.99.0", Me));

    /// <summary>Each part of the move as its own write, in the order the engine used to make them.</summary>
    public override Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct)
    {
        Written<Pbi> written = new(Pbis.FirstOrDefault(row => row.Ref == pbi), null);
        if (move.Held is { } held)
        {
            written = Write(pbi, $"{(held ? "hold" : "release")} #{pbi.Id}", row => row with { Held = held });
        }

        if (move.To is { } state)
        {
            written = Write(pbi, $"move #{pbi.Id} {state}", row => row with { State = state });
        }

        if (move.AssignToSelf)
        {
            written = Write(pbi, $"assign #{pbi.Id}", row => row with { Assignee = Me });
        }

        if (move.Close)
        {
            written = Write(pbi, $"close #{pbi.Id}", row => row with { Closed = true });
        }

        return Task.FromResult(written);
    }

    public override Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct)
    {
        var id = draft.Closes!.Id;
        if (draft.Draft)
        {
            Writes.Add($"open draft #{id} from {draft.Branch}");
            Pulls[id] = new PullRequestInfo($"1{id}", new Uri($"https://github.com/{repository}/pull/1{id}"), true, false, false);
        }
        else
        {
            Writes.Add($"ready #{id}");
            Pulls[id] = Pulls[id] with { IsDraft = false };
        }

        return Task.FromResult(new Written<PullRequestInfo>(Pulls[id], null));
    }

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(Pulls.GetValueOrDefault(pbi.Id));

    // #513. The Review tab's members: a review approves the pull request as the vendor would, and
    // the threads are whatever the test puts on it.

    public Dictionary<string, List<ReviewThread>> Threads { get; } = [];

    /// <summary>The body of every review written, in order.</summary>
    public List<string> Reviews { get; } = [];

    /// <summary>Set to refuse every review, in the vendor's words.</summary>
    public string? RefusesReviews { get; set; }

    /// <summary>The person reviewing is the pull request's author, so an approval or a request for changes goes on as a comment.</summary>
    public bool ReviewsAsAuthor { get; set; }

    public override Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct)
    {
        if (RefusesReviews is { } refusal)
        {
            return Task.FromResult(new Written<SubmittedReview>(null, refusal));
        }

        // What GitHubPbiProvider does when the person reviewing wrote the pull request (#544).
        string? asComment = null;
        if (ReviewsAsAuthor && verdict != ReviewVerdict.Comment)
        {
            asComment = $"Review Can not {(verdict == ReviewVerdict.Approve ? "approve" : "request changes on")} your own pull request";
            verdict = ReviewVerdict.Comment;
        }

        Writes.Add($"{verdict switch { ReviewVerdict.Approve => "approve", ReviewVerdict.RequestChanges => "request changes on", _ => "review" }} #{pbi.Id}");
        Reviews.Add(body);
        if (verdict == ReviewVerdict.Approve && Pulls.TryGetValue(pbi.Id, out var pull))
        {
            Pulls[pbi.Id] = pull with { IsApproved = true };
        }
        return Task.FromResult(new Written<SubmittedReview>(new SubmittedReview($"{Writes.Count}", verdict, Me, body, asComment), null));
    }

    public override Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(
            Pulls.GetValueOrDefault(pbi.Id) is { } pull
                ? new PullRequestChange(pull, "master", "base", $"change/{pbi.Id}", "head", 1, 0, 0, [], 0, Threads.GetValueOrDefault(pbi.Id) ?? [])
                : null
        );

    // #636. What closing an epic reads: each PBI's parent, a parent's children and its body. A PBI
    // named in no test's Parents has none, so every other test's close stops at the first read.

    /// <summary>Each child's id, and the id of the PBI it is part of.</summary>
    public Dictionary<string, string> Parents { get; } = [];

    /// <summary>A PBI's body, empty unless a test sets it.</summary>
    public Dictionary<string, string> Bodies { get; } = [];

    public static readonly DateTimeOffset Opened = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct)
    {
        if (Pbis.FirstOrDefault(row => row.Ref == pbi) is not { } found)
        {
            return Task.FromResult<PbiThread?>(null);
        }

        var parent = Parents.TryGetValue(pbi.Id, out var id) ? Pbis.Single(row => row.Ref.Id == id) : null;
        var url = new Uri($"https://github.com/{pbi.Repository}/issues/{pbi.Id}");
        return Task.FromResult<PbiThread?>(
            new PbiThread(
                found,
                url,
                Me,
                Opened,
                Bodies.GetValueOrDefault(pbi.Id) ?? "",
                [],
                [],
                parent is null ? null : new LinkedPbi(parent.Ref, parent.Title, new Uri($"https://github.com/{pbi.Repository}/issues/{parent.Ref.Id}"), parent.Closed),
                [],
                Comments.GetValueOrDefault(pbi.Id) ?? []
            )
        );
    }

    /// <summary>#734. What the Metrics screen reads; null answers as a vendor that cannot, with that sentence.</summary>
    public VendorHistory? History { get; set; }

    public Task<VendorHistory> ReadHistoryAsync(string repository, CancellationToken ct) =>
        History is { } history ? Task.FromResult(history) : throw new PbiProviderException("gh: no history in this test.");

    public Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Pbi>>([.. Pbis.Where(row => Parents.GetValueOrDefault(row.Ref.Id) == pbi.Id)]);

    // #512. The Issue and Proposal tabs' members, which the loop never asks.

    // #696. The journey comment the engine keeps on every PBI it moves is held apart from Writes, so
    // the tests that list what a transition wrote at the vendor keep reading only that.

    /// <summary>Each PBI's comments as the vendor keeps them, the journey's among them.</summary>
    public Dictionary<string, List<PbiComment>> Comments { get; } = [];

    /// <summary>Set to refuse every comment, in the vendor's words.</summary>
    public string? RefusesComments { get; set; }

    int _commentIds;

    public override Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct)
    {
        if (RefusesComments is { } refusal)
        {
            return Task.FromResult(new Written<PbiComment>(null, refusal));
        }

        if (!body.StartsWith("<!-- aio:journey", StringComparison.Ordinal))
        {
            Writes.Add($"comment #{pbi.Id}: {body}");
        }

        var comment = new PbiComment($"{900 + ++_commentIds}", Me, DateTimeOffset.UtcNow, body);
        (Comments.TryGetValue(pbi.Id, out var kept) ? kept : Comments[pbi.Id] = []).Add(comment);
        return Task.FromResult(new Written<PbiComment>(comment, null));
    }

    public Task<Written<PbiComment>> EditCommentAsync(PbiRef pbi, string commentId, string body, CancellationToken ct)
    {
        if (RefusesComments is { } refusal)
        {
            return Task.FromResult(new Written<PbiComment>(null, refusal));
        }

        var kept = Comments.GetValueOrDefault(pbi.Id) ?? [];
        var index = kept.FindIndex(comment => comment.Id == commentId);
        if (index < 0)
        {
            return Task.FromResult(new Written<PbiComment>(null, $"#{pbi.Id} has no comment {commentId}."));
        }

        kept[index] = kept[index] with { Body = body };
        return Task.FromResult(new Written<PbiComment>(kept[index], null));
    }

    Written<Pbi> Write(PbiRef pbi, string what, Func<Pbi, Pbi> change)
    {
        Writes.Add(what);
        var index = Pbis.FindIndex(row => row.Ref == pbi);
        Pbis[index] = change(Pbis[index]);
        return new Written<Pbi>(Pbis[index], null);
    }
}

/// <summary>Scripts that answer the lines they are told to, and remember what ran and on which branch.</summary>
sealed class FakeScripts
{
    /// <summary>
    /// What each script answers. <c>quality</c> reports, as it always does (#746), so a test of
    /// <c>verifying</c> need not say so.
    /// </summary>
    public Dictionary<string, (int Exit, string Line)> Answers { get; } = new()
    {
        ["quality"] = (0, """{"outcome": "reported", "published": false, "comment": null, "why": "Nothing published: gh could not comment."}"""),
    };

    /// <summary>What a script prints before its line, for a test that reads the output the engine kept (#638).</summary>
    public Dictionary<string, string> Printed { get; } = [];

    public List<ScriptStep> Ran { get; } = [];

    /// <summary>
    /// How many places a script ran in, and how many of those were retired (#751): a place is
    /// prepared by its first script, as Runs prepares it, and retired when the state is done with it.
    /// </summary>
    public int Prepared { get; private set; }

    public int Retired { get; private set; }

    public IScriptPlace Open(PbiRef pbi, string folderPath, string branch) => new Place(this, pbi, folderPath, branch);

    sealed class Place(FakeScripts scripts, PbiRef pbi, string folderPath, string branch) : IScriptPlace
    {
        bool _prepared;

        public Task<ScriptResult> RunAsync(string script, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            if (!_prepared)
            {
                _prepared = true;
                scripts.Prepared++;
            }

            return scripts.RunAsync(new ScriptStep(pbi, folderPath, branch, script, arguments));
        }

        public ValueTask DisposeAsync()
        {
            if (_prepared)
            {
                scripts.Retired++;
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A script named here answers only once its task completes, so a test sees it running.</summary>
    public Dictionary<string, Task> Until { get; } = [];

    public async Task<ScriptResult> RunAsync(ScriptStep step)
    {
        Ran.Add(step);
        if (Until.TryGetValue(step.Script, out var until))
        {
            await until;
        }

        var (exit, line) = Answers[step.Script];
        var output = Printed.TryGetValue(step.Script, out var printed) ? $"{printed}\n{line}" : line;
        return new ScriptResult(exit, JsonDocument.Parse(line).RootElement.Clone(), output);
    }
}

/// <summary>
/// Runs that start when asked and end when the test says so, and scripts answered by
/// <see cref="FakeScripts"/>. <see cref="Elsewhere"/> live runs fill the WIP.
/// </summary>
sealed class FakeRuns(FakeScripts scripts) : IRunRequests
{
    public List<(RunRequest Request, RunStatus Status)> Started { get; } = [];

    public int Elsewhere { get; set; }

    /// <summary>What Runs says about placing the next run (#614); null, as for a worktree, unless a test sets it.</summary>
    public RunPlacement? Placement { get; set; }

    public Task<RunPlacement?> PlacementAsync(RunRequest request, CancellationToken ct) => Task.FromResult(Placement);

    public Task<Guid> StartAsync(RunRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        // The scheduled engine adds from its own thread while a test reads the live list (#668).
        lock (Started)
        {
            Started.Add((request, new RunStatus(id, request.Pbi, request.State, null) { Provider = request.Provider, Model = request.Model, Command = request.Command, StartedAt = DateTimeOffset.UtcNow }));
        }

        return Task.FromResult(id);
    }

    public void End(int index, string outcome, string reason) =>
        Started[index] = Started[index] with { Status = Started[index].Status with { End = new RunEnd(outcome, reason, null, []) { EndedAt = DateTimeOffset.UtcNow } } };

    public void Fail(int index, RunFailure failure, string reason) =>
        Started[index] = Started[index] with { Status = Started[index].Status with { End = new RunEnd(null, reason, failure, []) { EndedAt = DateTimeOffset.UtcNow } } };

    public IScriptPlace OpenScriptPlace(PbiRef pbi, string folderPath, string branch) => scripts.Open(pbi, folderPath, branch);

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);

    public Task<TimeSpan> StaleAfterAsync(CancellationToken ct) => Task.FromResult(StaleAfter);

    public Task<RunStatus?> GetAsync(Guid runId, CancellationToken ct) =>
        Task.FromResult<RunStatus?>(Started.Select(run => run.Status).FirstOrDefault(status => status.RunId == runId));

    /// <summary>Runs a person opened (#697), which <see cref="GetAsync"/> never answers for.</summary>
    public List<RunStatus> Manual { get; } = [];

    public Task<IReadOnlyList<RunStatus>> ListEndedAsync(PbiRef pbi, CancellationToken ct)
    {
        lock (Started)
        {
            return Task.FromResult<IReadOnlyList<RunStatus>>(
                [.. Started.Select(run => run.Status).Concat(Manual).Where(status => status.Pbi == pbi && status.End is not null)]
            );
        }
    }

    /// <summary>#734. Every run of the repository, as a test lays them out for Metrics.</summary>
    public List<RunStatus> Repository { get; } = [];

    public Task<IReadOnlyList<RunStatus>> ListRepositoryAsync(string repository, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RunStatus>>([.. Repository.Where(run => run.Pbi.Repository == repository)]);

    public Task<IReadOnlyList<RunStatus>> ListLiveAsync(CancellationToken ct)
    {
        lock (Started)
        {
            return Task.FromResult<IReadOnlyList<RunStatus>>(
                [
                    .. Started.Select(run => run.Status).Where(status => status.End is null),
                    .. Enumerable.Range(0, Elsewhere).Select(index => new RunStatus(Guid.NewGuid(), new PbiRef("other/repo", $"{index}"), "in-progress", null)),
                ]
            );
        }
    }
}

/// <summary>One folder, a scratch directory holding this repository's own loop file.</summary>
sealed class FakeFolders : IFolderDirectory
{
    public const string Repository = "owner/repo";

    public string Path { get; } = Directory.CreateTempSubdirectory("aio-loop-").FullName;

    public FakeFolders()
    {
        Directory.CreateDirectory(System.IO.Path.Combine(Path, ".aio"));
        File.Copy(
            System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "loop.yaml"),
            System.IO.Path.Combine(Path, ".aio", "loop.yaml")
        );
        // #697. The harness is installed, so the engine writes the journey comment.
        File.WriteAllText(System.IO.Path.Combine(Path, ".aio", "version"), "16\n");
    }

    public Task<IReadOnlyList<LoopFolder>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoopFolder>>([new LoopFolder(Path, "github", Repository)]);
}

/// <summary>
/// The claude-code adapter as far as the engine asks it before a run: whether it trusts the folder
/// (#606). Trusted unless a test says otherwise, so the user's own <c>~/.claude.json</c> is never read.
/// </summary>
sealed class FakeAgent : IAgentProvider
{
    public bool Trusted { get; set; } = true;

    public string Name => "claude-code";

    public AgentTrust Trust(string folder) =>
        Trusted
            ? new(true, $"Claude Code trusts {folder}.", null)
            : new(false, $"Claude Code does not trust {folder}. Run claude there once and accept it.", $"cd '{folder}' && claude");

    public SessionOptions Launch(AgentRequest request) => throw new InvalidOperationException("The engine asks Runs to launch, never the adapter.");

    public Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) => throw new InvalidOperationException("Runs reads the end.");

    public Task<CliProbe> ProbeAsync(CancellationToken ct) => throw new InvalidOperationException("Nothing here probes.");
}

/// <summary>One script a test's place ran: the PBI, the folder, the branch and what was asked.</summary>
sealed record ScriptStep(PbiRef Pbi, string FolderPath, string Branch, string Script, IReadOnlyList<string> Arguments);
