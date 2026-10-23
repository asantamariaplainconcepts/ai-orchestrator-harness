using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #698. An epic's Journey says it has none of its own and links to the Issue tab, where the graph
// is; its header counts the open sub-issues instead of saying "no state". A PBI without sub-issues
// keeps the loop's states.
public sealed partial class IssueProposalTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task An_epic_s_journey_sends_to_the_issue_tab_and_its_header_counts_the_open_sub_issues(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var stub = new Stub { Held = false };
        stub.Children.AddRange(
        [
            new { repository = Repository, id = "601", title = "One", state = (string?)"spec-ready", held = false, closed = false },
            new { repository = Repository, id = "602", title = "Two", state = (string?)null, held = false, closed = false },
        ]);
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");

        await Assertions.Expect(page.GetByText("epic · 2 of 2 open", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("An epic has no journey of its own")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Their states are in the graph on the Issue tab." }))
            .ToHaveAttributeAsync("href", $"/pbis/{FolderId}/495/issue");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("proposal-review", new() { Exact = true })).ToHaveCountAsync(0);

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"epic-journey-{theme}"), FullPage = true });
    }

    [Fact]
    public async Task A_pbi_without_sub_issues_keeps_the_loop_s_states_in_its_journey()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page, new Stub { Held = false });

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");

        var journey = page.GetByRole(AriaRole.List, new() { Name = "Journey" });
        await Assertions.Expect(journey.Locator("li[aria-current=step]")).ToContainTextAsync("proposal-review");
        await Assertions.Expect(page.GetByText("An epic has no journey of its own")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("epic ·")).ToHaveCountAsync(0);
    }
}
