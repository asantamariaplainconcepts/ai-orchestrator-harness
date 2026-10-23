using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using AiOrchestrator.Modules.Loop.Settings;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// Turns the engine on or off for one folder, without a restart (#562). The Loop strip (#564) and
/// the folder page (#565) both send it.
/// <para>
/// <b>Off stops nothing.</b> The scheduled passes leave the folder out: its PBIs are not read, so no
/// script or run starts there, and its live runs go on in Runs; what they end with is applied by the
/// first pass after it is on again. A pass by hand still walks it.
/// </para>
/// <para>
/// <b>On needs a loop.</b> A folder whose <c>.aio/loop.yaml</c> is missing or breaks its rules is
/// refused with the reason (#564): the passes would skip it, so a switch that reads on would say
/// something the engine does not do. Off is always taken.
/// </para>
/// <para>
/// A change is a line of the journal, kind <c>switched</c>, so the Loop screen says who stopped the
/// engine and when. Setting what is already set writes no line.
/// </para>
/// </summary>
sealed class SwitchEngine : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/loop/engine",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.Repository, request.On), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SwitchEngine))
            .WithTags("Loop");

    /// <param name="Repository">The folder's repository at the vendor, such as <c>owner/name</c>.</param>
    /// <param name="On">True to turn the engine on for it, false to turn it off.</param>
    internal sealed record Request(string Repository, bool On);

    /// <param name="Written">The journal line the change wrote, or null when nothing changed.</param>
    internal sealed record Response(string Repository, bool On, ReadJournal.Line? Written);

    // Nothing to scope to: the engine spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, bool On) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Repository).NotEmpty().MaximumLength(200);
    }

    internal sealed class Handler(
        ControlSettings settings,
        LoopDbContext database,
        IFolderDirectory folders,
        Passes passes,
        TimeProvider clock
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var folder = (await folders.ListAsync(cancellationToken)).FirstOrDefault(candidate =>
                string.Equals(candidate.Repository, command.Repository, StringComparison.OrdinalIgnoreCase)
            );
            if (folder is null)
            {
                return Error.NotFound("Loop.NoFolder", $"No folder is a checkout of {command.Repository}. Add one from the rail.");
            }

            if (command.On && (await LoopReader.ReadAsync(folder, cancellationToken)).Problem is { } problem)
            {
                return Error.Conflict("Loop.NoLoop", $"The engine cannot be turned on for {folder.Repository}: {problem}");
            }

            var was = (await settings.ReadAsync(cancellationToken)).IsOn(folder.Repository);
            await settings.SetOnAsync(folder.Repository, command.On, cancellationToken);

            JournalEntry? line = null;
            if (was != command.On)
            {
                line = new JournalEntry(
                    clock.GetUtcNow(),
                    folder.Repository,
                    string.Empty,
                    Word(was),
                    JournalKind.Switched,
                    Word(command.On),
                    command.On
                        ? "A person turned the engine on for this folder: the next pass works its PBIs."
                        : "A person turned the engine off for this folder: its live runs finish, and nothing new starts until it is on again."
                );
                database.Journal.Add(line);
            }

            await database.SaveChangesAsync(cancellationToken);
            passes.Wake();

            return new Response(folder.Repository, command.On, line is null ? null : ReadJournal.Line.Of(line));
        }

        static string Word(bool on) => on ? "on" : "off";
    }
}
