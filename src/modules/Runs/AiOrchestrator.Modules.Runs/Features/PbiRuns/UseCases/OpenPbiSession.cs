using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// <i>Terminal</i> and <i>Explore with agent</i> in the PBI's header (DESIGN.md#the-screens,
/// "Las sesiones a mano", #516): a session on the PBI's branch, in its environment, stored as a
/// manual run of the state the PBI is in now. It shows in the Journey with its recording and never
/// moves the loop (<see cref="RunBook"/>).
/// <para>
/// The state is read from the vendor here rather than taken from the browser, so the run is filed
/// under the state the PBI is in, not the one a stale screen showed.
/// </para>
/// </summary>
sealed class OpenPbiSession : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/sessions",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new Command(request.Repository, request.Id, Blank(request.Provider), Blank(request.Model), Blank(request.Command)),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(OpenPbiSession))
            .WithTags("Runs");

    /// <param name="Repository">The PBI's repository, as the vendor names it.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="Provider">The agent CLI, such as <c>claude-code</c>; null or empty for a terminal.</param>
    /// <param name="Model">A model of that agent, or null for its default.</param>
    /// <param name="Command">What the agent is asked first, or null to open it waiting.</param>
    internal sealed record Request(string Repository, string Id, string? Provider, string? Model, string? Command);

    /// <param name="RunId">The manual run; its terminal is <c>/api/sessions/{runId}/attach</c>.</param>
    internal sealed record Response(Guid RunId);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string? Provider, string? Model, string? Prompt)
        : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
        }
    }

    internal sealed class Handler(
        IFolderDirectory folders,
        IEnumerable<IPbiProvider> vendors,
        IEnumerable<IAgentProvider> agents,
        RunBook book
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (command.Provider is { } provider && agents.All(agent => agent.Name != provider))
            {
                return Error.NotFound("Session.NoAgent", $"No agent provider is called '{provider}'.");
            }

            var folder = (await folders.ListAsync(cancellationToken)).FirstOrDefault(candidate =>
                string.Equals(candidate.Repository, command.Repository, StringComparison.OrdinalIgnoreCase)
            );
            if (folder is null)
            {
                return Error.NotFound("Pbi.NoFolder", $"No folder is a checkout of {command.Repository}. Add one from the rail.");
            }

            if (vendors.FirstOrDefault(vendor => vendor.Name == folder.Vendor) is not { } vendor)
            {
                return Error.NotFound("Pbi.NoAdapter", $"No adapter reads {folder.Vendor}.");
            }

            var reference = new PbiRef(folder.Repository, command.Id);
            Pbi? pbi;
            try
            {
                pbi = (await vendor.ReadThreadAsync(reference, cancellationToken))?.AsPbi;
            }
            catch (PbiProviderException failure)
            {
                return Error.Conflict("Pbi.VendorUnread", failure.Message);
            }

            if (pbi is null)
            {
                return Error.NotFound("Pbi.NotFound", $"{folder.Repository} has no PBI {command.Id}.");
            }

            var runId = await book.OpenAsync(
                reference,
                pbi.State ?? string.Empty,
                folder.Path,
                Branch(reference),
                command.Provider,
                command.Model,
                command.Prompt,
                cancellationToken
            );

            return new Response(runId);
        }

        /// <summary>The PBI's branch, as the loop names it (<c>LoopEngine.Branch</c>).</summary>
        static string Branch(PbiRef pbi) => $"change/{pbi.Id}";
    }

    static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
