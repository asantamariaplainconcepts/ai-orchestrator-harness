using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #611. A Mermaid block in the Issue tab drawn as an SVG in both themes, an epic's graph painted with
// each sub-issue's live state and a dotted edge for a wait on files, a broken diagram shown as its
// code with Mermaid's error, and the graph the tab draws for sub-issues when the body has none. The
// overlay is a pure function of the frontend, imported here from the same Vite that serves the page.
public sealed partial class IssueProposalTests
{
    const string EpicGraph = """
        The graph.

        ```mermaid
        flowchart LR
          e495[The epic] --> i601[#601 Done]
          e495 --> i602[#602 Runs]
          e495 --> i603[#603 Held]
          e495 --> i604[#604 Waits]
          e495 --> i605[#605 Ready]
        ```
        """;

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task An_epic_s_mermaid_graph_is_drawn_with_each_sub_issue_in_its_live_state(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub { IssueBody = EpicGraph };
        Epic(stub);
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        var diagram = page.GetByRole(AriaRole.Figure, new() { Name = "Diagram" });
        await Assertions.Expect(diagram.Locator("svg")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("code.language-mermaid")).ToHaveCountAsync(0);
        await Assertions.Expect(diagram).Not.ToContainTextAsync("flowchart LR");

        await Assertions.Expect(diagram.Locator("g.node.done")).ToContainTextAsync("#601 Done");
        await Assertions.Expect(diagram.Locator("g.node.running")).ToContainTextAsync("#602 Runs");
        await Assertions.Expect(diagram.Locator("g.node.held")).ToContainTextAsync("#603 Held");
        await Assertions.Expect(diagram.Locator("g.node.waiting")).ToContainTextAsync("#604 Waits");
        await Assertions.Expect(diagram.Locator("g.node.state_ready")).ToContainTextAsync("#605 Ready");
        await Assertions.Expect(diagram).ToContainTextAsync("waits for");

        // The done node is painted with this theme's --success token, not a colour of Mermaid's.
        var painted = await diagram.EvaluateAsync<bool>(
            """
            figure => {
              const success = getComputedStyle(figure).getPropertyValue('--success').trim().toLowerCase();
              const shape = figure.querySelector('g.node.done rect, g.node.done path, g.node.done polygon');
              return success !== '' && (shape?.getAttribute('style') ?? '').toLowerCase().includes(success);
            }
            """
        );
        painted.ShouldBeTrue("the done class is filled with the --success token");

        // #636. The counts beside the graph, from the statuses it is painted with.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sub-issues · 5" })).ToBeVisibleAsync();
        await Assertions.Expect(Counts(page)).ToHaveTextAsync(["1 done", "1 running", "1 waiting", "1 held", "1 other"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Figure, new() { Name = "Diagram" })).ToHaveCountAsync(1);

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"epic-graph-{theme}"), FullPage = true });
    }

    [Fact]
    public async Task A_diagram_that_does_not_parse_is_shown_as_code_with_mermaid_s_error_and_the_tab_goes_on()
    {
        await using var context = await frontend.Open("dark");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub { IssueBody = "Before.\n\n```mermaid\nflowchart LR\n  a --> ((\n```\n\nAfter." };
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        var diagram = page.GetByRole(AriaRole.Figure, new() { Name = "Diagram" });
        await Assertions.Expect(diagram.Locator("pre code")).ToContainTextAsync("a --> ((");
        await Assertions.Expect(diagram.GetByRole(AriaRole.Alert)).ToContainTextAsync("This diagram does not parse");
        await Assertions.Expect(diagram.Locator("svg")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("After.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Comments" }).Locator(":scope > li")).ToHaveCountAsync(2);

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("mermaid-broken-dark"), FullPage = true });
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Sub_issues_without_a_graph_in_the_body_get_one_drawn_at_390(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        var stub = new Stub();
        Epic(stub);
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sub-issues · 5" })).ToBeVisibleAsync();
        var diagram = page.GetByRole(AriaRole.Figure, new() { Name = "Diagram" });
        await Assertions.Expect(diagram.Locator("g.node.done")).ToContainTextAsync("#601 Done");
        await Assertions.Expect(diagram.Locator("g.node.waiting")).ToContainTextAsync("#604 Waits");
        await Assertions.Expect(diagram).ToContainTextAsync("#495 Declare the loop as a state machine");
        await Assertions.Expect(Counts(page)).ToHaveTextAsync(["1 done", "1 running", "1 waiting", "1 held", "1 other"]);

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "the graph scrolls inside its card rather than widening the pane at 390px");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"epic-graph-basic-390-{theme}"), FullPage = true });
    }

    [Fact]
    public async Task The_counts_beside_the_graph_put_every_sub_issue_in_one_state()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Stub());
        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        var text = await page.EvaluateAsync<string>(
            """
            async () => {
              const { countChildren } = await import('/shared/ui/mermaidOverlay.ts');
              const of = (...statuses) => statuses.map((status) => ({ status }));
              return JSON.stringify([
                countChildren(of('done', 'done', 'running', 'held', 'waiting', 'spec-ready', null, 'other')),
                countChildren([]),
              ]);
            }
            """
        );

        text.ShouldBe(
            """[{"done":2,"running":1,"waiting":1,"held":1,"other":3},{"done":0,"running":0,"waiting":0,"held":0,"other":0}]"""
        );
    }

    static ILocator Counts(IPage page) =>
        page.GetByRole(AriaRole.List, new() { Name = "Sub-issues by state" }).Locator(":scope > li");

    [Fact]
    public async Task The_overlay_writes_a_class_per_state_and_a_dotted_edge_for_a_wait_on_files()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Stub());
        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        var text = await page.EvaluateAsync<string>(
            """
            async () => {
              const { liveChildren, overlay } = await import('/shared/ui/mermaidOverlay.ts');
              const paint = (name) => ({ fill: `${name}-fill`, stroke: `${name}-stroke`, color: `${name}-text` });
              const palette = { done: paint('ok'), running: paint('blue'), held: paint('amber'), waiting: paint('tint'), plain: paint('grey') };
              const child = (id, state, held = false, closed = false) => ({ repository: 'owner/sandbox', id, title: `#${id}`, state, held, closed });
              const children = [
                child('1', 'in-progress', false, true),
                child('2', 'in-progress'),
                child('3', 'verifying'),
                child('4', 'proposal-review', true),
                child('5', 'spec-ready'),
                child('6', 'spec-ready'),
                child('7', 'ready'),
                child('8', null),
                child('9', 'spec-ready'),
              ];
              const live = {
                liveRuns: [{ repository: 'owner/sandbox', pbi: '1' }, { repository: 'Owner/Sandbox', pbi: '2' }],
                liveScripts: [{ repository: 'owner/sandbox', pbi: '3' }],
              };
              const journal = [
                { repository: 'owner/sandbox', pbi: '5', kind: 'waited', why: '#2 is in flight and declares src/x too.' },
                { repository: 'owner/sandbox', pbi: '6', kind: 'waited', why: '#40 is in flight and declares src/y too.' },
                { repository: 'owner/sandbox', pbi: '5', kind: 'moved', why: 'older than the wait' },
                { repository: 'owner/sandbox', pbi: '7', kind: 'moved', why: 'ready' },
                { repository: 'owner/sandbox', pbi: '7', kind: 'waited', why: '#2 is in flight and declares src/z too.' },
              ];
              const graph = 'flowchart LR\n  i1 --> i2\n  i2 --> i3\n  i3 --> i4\n  i4 --> i5\n  i5 --> i6\n  i6 --> i7\n  i7 --> i8\n';
              return JSON.stringify({
                painted: overlay(graph, liveChildren(children, live, journal), palette, 'waits for'),
                sequence: overlay('sequenceDiagram\n  a->>b: hi\n', liveChildren(children, live, journal), palette, 'waits for'),
                none: overlay(graph, [], palette, 'waits for'),
              });
            }
            """
        );
        using var answers = System.Text.Json.JsonDocument.Parse(text);
        var graph = "flowchart LR\n  i1 --> i2\n  i2 --> i3\n  i3 --> i4\n  i4 --> i5\n  i5 --> i6\n  i6 --> i7\n  i7 --> i8";

        // 1 is closed with a run still live: done wins. 2 runs and 3 runs a script: running. 4 is held.
        // 5 waits on #2, a sibling: waiting, and the dotted edge. 6 waits on #40, no sibling: waiting
        // and no edge, which would draw a node the graph does not have. 7's newest line is a move:
        // its loop state. 8 carries no state: no class. 9 is not in the graph: nothing.
        answers.RootElement.GetProperty("painted").GetString().ShouldBe(
            graph + "\n"
                + "  classDef done fill:ok-fill,stroke:ok-stroke,color:ok-text\n"
                + "  classDef running fill:blue-fill,stroke:blue-stroke,color:blue-text\n"
                + "  classDef held fill:amber-fill,stroke:amber-stroke,color:amber-text\n"
                + "  classDef waiting fill:tint-fill,stroke:tint-stroke,color:tint-text,stroke-dasharray:4 3\n"
                + "  classDef state_ready fill:grey-fill,stroke:grey-stroke,color:grey-text\n"
                + "  class i1 done\n"
                + "  class i2 running\n"
                + "  class i3 running\n"
                + "  class i4 held\n"
                + "  class i5 waiting\n"
                + "  class i6 waiting\n"
                + "  class i7 state_ready\n"
                + "  i5 -.->|waits for| i2\n"
        );
        answers.RootElement.GetProperty("sequence").GetString().ShouldBe("sequenceDiagram\n  a->>b: hi\n", "only a flowchart is painted");
        answers.RootElement.GetProperty("none").GetString().ShouldBe(graph + "\n", "without sub-issues the diagram is left as it was");
    }

    /// <summary>
    /// Five sub-issues, one per live state and one plain: #601 closed, #602 with a live run, #603
    /// held, #604 waiting on #602 for files, #605 at <c>ready</c>.
    /// </summary>
    static void Epic(Stub stub)
    {
        object Child(string id, string title, string? state, bool held = false, bool closed = false) =>
            new { repository = Repository, id, title, state, held, closed };

        stub.Children.AddRange(
        [
            Child("601", "Done", "done", closed: true),
            Child("602", "Runs", "in-progress"),
            Child("603", "Held", "proposal-review", held: true),
            Child("604", "Waits", "spec-ready"),
            Child("605", "Ready", "ready"),
        ]);
        stub.Present = new
        {
            liveRuns = new[] { new { runId = Guid.NewGuid(), repository = Repository, pbi = "602", state = "in-progress", lastOutputAt = (DateTimeOffset?)null, stale = false } },
            liveScripts = Array.Empty<object>(),
        };
        stub.Journal.Insert(0, new
        {
            at = DateTimeOffset.UtcNow.AddMinutes(-2),
            repository = Repository,
            pbi = "604",
            from = "spec-ready",
            kind = "waited",
            to = (string?)null,
            why = "#602 is in flight and declares src/frontend/shared/ui/ too.",
        });
    }
}
