using System.Net;
using AiOrchestrator.Server.Installed;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// #679. The update check against a fake nuget.org: the flat-container index of <c>aio</c>, answered
/// the way the real one answers, a list of versions or a 404 before the first publish.
/// </summary>
public sealed class UpdatesTests
{
    [Fact]
    public async Task The_published_version_it_runs_is_up_to_date()
    {
        var answer = await Check("0.3.0+515d5c24", NuGet("""{"versions":["0.2.18","0.3.0"]}"""));

        answer.State.ShouldBe("up-to-date");
        answer.Installed.ShouldBe("0.3.0");
        answer.Latest.ShouldBe("0.3.0");
    }

    [Fact]
    public async Task A_newer_stable_version_is_available_and_a_preview_is_not_it()
    {
        var answer = await Check("0.3.0", NuGet("""{"versions":["0.3.0","0.3.1","0.4.0-preview.1"]}"""));

        answer.State.ShouldBe("available");
        answer.Latest.ShouldBe("0.3.1");
        answer.How.ShouldBe("tool");
    }

    [Fact]
    public async Task A_404_is_a_package_nobody_has_published_yet()
    {
        var answer = await Check("0.3.0", NuGet(status: HttpStatusCode.NotFound));

        answer.State.ShouldBe("unpublished");
        answer.Latest.ShouldBeNull();
    }

    [Fact]
    public async Task A_feed_that_fails_is_an_error_with_its_reason()
    {
        var answer = await Check("0.3.0", NuGet(status: HttpStatusCode.ServiceUnavailable));

        answer.State.ShouldBe("error");
        answer.Detail.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reading_waits_for_the_last_check_and_asks_nothing_new()
    {
        var feed = NuGet("""{"versions":["0.3.1"]}""");
        var updates = new Updates(new HttpClient(feed), "0.3.0", "dnx", NullLogger<Updates>.Instance);

        (await updates.Read(CancellationToken.None)).State.ShouldBe("unchecked");
        feed.Requests.ShouldBeEmpty();

        await updates.Check(CancellationToken.None);
        var read = await updates.Read(CancellationToken.None);

        read.State.ShouldBe("available");
        read.How.ShouldBe("dnx");
        feed.Requests.ShouldBe([Updates.Index]);
    }

    static Task<UpdateAnswer> Check(string version, FakeNuGet feed) =>
        new Updates(new HttpClient(feed), version, "tool", NullLogger<Updates>.Instance).Check(CancellationToken.None);

    static FakeNuGet NuGet(string body = "", HttpStatusCode status = HttpStatusCode.OK) => new(status, body);

    sealed class FakeNuGet(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
