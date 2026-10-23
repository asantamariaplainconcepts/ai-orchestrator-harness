using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// What the instance's agents may do without asking (#578), one mode for every run and every session
/// by hand, read each time one starts so a change applies to the next. Stored in <c>runs_settings</c>
/// beside the stale threshold; nothing is stored until somebody changes it, and then it is
/// <c>auto</c> (#706). It was <c>allow-list</c>, the mode every run had before there was a choice, until
/// the dogfood of 13 October stopped for an hour at a <c>python3 -c</c> outside the allow list; the epic
/// before it ran in <c>auto</c>. A person who stored <c>allow-list</c> keeps it.
/// </summary>
sealed class PermissionModeSetting(IServiceScopeFactory scopes)
{
    const string Name = "permission-mode";

    /// <summary>The modes as the API and the database spell them, in the order Settings lists them.</summary>
    public static readonly IReadOnlyList<string> Modes = ["allow-list", "auto", "yolo"];

    public static string Spell(AgentPermissions mode) =>
        mode switch
        {
            AgentPermissions.Auto => "auto",
            AgentPermissions.Yolo => "yolo",
            _ => "allow-list",
        };

    /// <summary>The mode <paramref name="spelled"/> names, or null when it names none.</summary>
    public static AgentPermissions? Parse(string? spelled) =>
        spelled switch
        {
            "allow-list" => AgentPermissions.AllowList,
            "auto" => AgentPermissions.Auto,
            "yolo" => AgentPermissions.Yolo,
            _ => null,
        };

    public async Task<AgentPermissions> ReadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await ReadAsync(scope.ServiceProvider.GetRequiredService<RunsDbContext>(), ct);
    }

    public static async Task<AgentPermissions> ReadAsync(RunsDbContext database, CancellationToken ct)
    {
        var stored = await database.Settings.AsNoTracking().Where(row => row.Name == Name).Select(row => row.Value).FirstOrDefaultAsync(ct);
        return Parse(stored) ?? AgentPermissions.Auto;
    }

    /// <summary>Stores the mode and saves.</summary>
    public static async Task SetAsync(RunsDbContext database, AgentPermissions mode, CancellationToken ct)
    {
        var value = Spell(mode);
        var existing = await database.Settings.FirstOrDefaultAsync(row => row.Name == Name, ct);
        if (existing is null)
        {
            database.Settings.Add(new RunsSetting(Name, value));
        }
        else
        {
            existing.Set(value);
        }

        await database.SaveChangesAsync(ct);
    }
}
