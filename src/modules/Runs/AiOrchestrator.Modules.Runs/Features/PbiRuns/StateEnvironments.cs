using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// Where each agent state of a folder runs (#612), read as each run starts so a change applies to the
/// next. <c>loop.yaml</c> says what a step does and this says where, because where depends on the
/// machine and the credentials on it, not on the repository.
/// <para>
/// One row of <c>runs_settings</c> per repository and state that somebody changed; a state with none
/// runs in <see cref="RunsOptions.Environment"/>. Kept per repository, like the engine's switch,
/// because the engine walks one checkout per repository.
/// </para>
/// </summary>
sealed class StateEnvironments(IServiceScopeFactory scopes)
{
    const string Prefix = "environment:";

    /// <summary>The environment stored for <paramref name="state"/> of <paramref name="repository"/>, or null.</summary>
    public async Task<string?> ReadAsync(string repository, string state, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var name = Key(repository, state);
        return await database.Settings.AsNoTracking().Where(row => row.Name == name).Select(row => row.Value).FirstOrDefaultAsync(ct);
    }

    /// <summary>Every state of <paramref name="repository"/> with an environment stored, by state.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ListAsync(RunsDbContext database, string repository, CancellationToken ct)
    {
        var prefix = Key(repository, string.Empty);
        var rows = await database.Settings.AsNoTracking().Where(row => row.Name.StartsWith(prefix)).ToListAsync(ct);
        return rows.ToDictionary(row => row.Name[prefix.Length..], row => row.Value, StringComparer.Ordinal);
    }

    /// <summary>Stores the environment of one state and saves.</summary>
    public static async Task SetAsync(RunsDbContext database, string repository, string state, string environment, CancellationToken ct)
    {
        var name = Key(repository, state);
        var existing = await database.Settings.FirstOrDefaultAsync(row => row.Name == name, ct);
        if (existing is null)
        {
            database.Settings.Add(new RunsSetting(name, environment));
        }
        else
        {
            existing.Set(environment);
        }

        await database.SaveChangesAsync(ct);
    }

    /// <summary>Lower case, because the vendor reads <c>Owner/Name</c> and <c>owner/name</c> as one repository.</summary>
    static string Key(string repository, string state) => $"{Prefix}{repository.ToLowerInvariant()}:{state}";
}
