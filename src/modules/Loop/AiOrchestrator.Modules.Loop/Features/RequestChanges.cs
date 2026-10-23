using System.Globalization;
using System.Text;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// <i>Request changes</i> in the Review tab (DESIGN.md, Review; #513): writes the
/// review at the vendor, copies it and the open threads to the issue, and sends a held PBI to its
/// state's <c>back</c> through <see cref="Planner.ChangesRequested"/>.
/// <para>
/// <b>Copied to the issue</b> because DESIGN.md says so and the issue is what the coder's next
/// run reads first; the threads stay on the pull request too, anchored to their lines.
/// </para>
/// </summary>
sealed class RequestChanges : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/review/request-changes",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewVerdicts.ReviewSubmitted>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RequestChanges))
            .WithTags("Loop");

    /// <param name="Repository">The PBI's repository, such as <c>owner/name</c>.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="Body">What has to change. The vendor refuses a request for changes without one.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Body) : ICommand<ErrorOr<ReviewVerdicts.ReviewSubmitted>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Body)
                .NotEmpty()
                .WithMessage("Say what has to change: it is written on the pull request and copied to the issue.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers, PersonDecisions decisions)
        : IAppCommandHandler<Command, ErrorOr<ReviewVerdicts.ReviewSubmitted>>
    {
        public Task<ErrorOr<ReviewVerdicts.ReviewSubmitted>> Handle(Command command, CancellationToken cancellationToken) =>
            ReviewVerdicts.SubmitAsync(
                folders,
                providers,
                decisions,
                command.Repository,
                command.Id,
                ReviewVerdict.RequestChanges,
                command.Body,
                async (provider, pbi, ct) =>
                {
                    var change = await provider.GetPullRequestChangeAsync(pbi, ct);
                    var copied = await provider.CommentAsync(pbi, Copy(change?.PullRequest.Id, command.Body, change?.Threads ?? []), [], ct);
                    return copied.Matches ? null : $"The copy to the issue: {copied.Mismatch}";
                },
                (loop, pbi, _) => Planner.ChangesRequested(loop, pbi),
                cancellationToken
            );
    }

    /// <summary>The review and every open thread with its line, as the issue comment that carries them.</summary>
    internal static string Copy(string? pull, string body, IReadOnlyList<ReviewThread> threads)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Changes requested on pull request #{pull ?? "?"}:\n\n{body.Trim()}\n");

        var open = threads.Where(thread => !thread.IsResolved && thread.Comments.Count > 0).ToArray();
        if (open.Length > 0)
        {
            text.Append("\nOpen threads:\n\n");
            foreach (var thread in open)
            {
                var at = thread.Line is { } line ? $"{thread.Path}:{line.ToString(CultureInfo.InvariantCulture)}" : thread.Path;
                foreach (var comment in thread.Comments)
                {
                    text.Append(
                        CultureInfo.InvariantCulture,
                        $"- `{at}` {comment.Author ?? "someone"}: {comment.Body.ReplaceLineEndings(" ").Trim()}\n"
                    );
                }
            }
        }

        return text.ToString();
    }
}
