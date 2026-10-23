using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Domain;
using AiOrchestrator.Modules.Workspace.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Workspace.Features.Preferences.UseCases;

/// <summary>
/// The editor <i>Open in editor</i> starts, as Settings shows it (#515), and whether the
/// requirements screen still opens on start (#680).
/// <para>
/// <b>Typed fields rather than a dictionary</b>, so a preference that does not exist is a compile
/// error at the one screen that wanted it rather than an empty string at runtime.
/// </para>
/// <para>
/// <b>The fallback is the behaviour that was already there.</b> <c>Desktop:Editor</c> is what
/// <c>DesktopIntegration</c> has always read, and <c>code</c> is what it falls back to, so a machine
/// that has never opened Settings behaves exactly as it did before the table existed.
/// </para>
/// </summary>
sealed class GetPreferences : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/preferences",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetPreferences))
            .WithTags("Workspace");

    /// <param name="Editor">A command line name, never a path this app invented.</param>
    /// <param name="RequirementsSeen">Whether the requirements screen was shown with every required CLI present (#680).</param>
    internal sealed record Response(string Editor, bool RequirementsSeen);

    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Response>;

    internal sealed class Handler(WorkspaceDbContext database, IConfiguration configuration)
        : IAppQueryHandler<Query, Response>
    {
        /// <summary>What <c>DesktopIntegration</c> falls back to, kept identical on purpose.</summary>
        internal const string DefaultEditor = "code";

        public async Task<Response> Handle(Query query, CancellationToken cancellationToken) =>
            Answer(await Read(database, cancellationToken), configuration);

        /// <summary>What the GET answers and the PUT answers back, from the stored rows.</summary>
        internal static Response Answer(IReadOnlyDictionary<string, string> stored, IConfiguration configuration) =>
            new(
                Editor(stored, configuration),
                stored.TryGetValue(PreferenceNames.RequirementsSeen, out var seen) && seen == bool.TrueString
            );

        /// <summary>
        /// Chosen, then configured, then the fallback. The order is the point: an operator's
        /// <c>appsettings</c> value keeps working right up until somebody picks something on the
        /// screen, and after that the person wins.
        /// </summary>
        internal static string Editor(
            IReadOnlyDictionary<string, string> stored,
            IConfiguration configuration
        )
        {
            if (stored.TryGetValue(PreferenceNames.Editor, out var chosen)
                && !string.IsNullOrWhiteSpace(chosen))
            {
                return chosen;
            }

            var configured = configuration["Desktop:Editor"]?.Trim();

            return string.IsNullOrWhiteSpace(configured) ? DefaultEditor : configured;
        }

        internal static async Task<IReadOnlyDictionary<string, string>> Read(
            WorkspaceDbContext database,
            CancellationToken cancellationToken
        ) =>
            await database
                .Preferences.AsNoTracking()
                .ToDictionaryAsync(row => row.Name, row => row.Value, cancellationToken);
    }
}
