using System.Globalization;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// One setting of Runs a person changed from the console (#562), keyed by name: the
/// <see cref="StaleThreshold"/> and the <see cref="PermissionModeSetting"/> (#578).
/// </summary>
sealed class RunsSetting
{
    RunsSetting() { }

    public RunsSetting(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public void Set(string value) => Value = value;
}

/// <summary>
/// How long a live run may write nothing before it reads as stale, read every time a run's liveness
/// is (#562): the seconds a person set in Settings, stored in <c>runs_settings</c>, or else
/// <see cref="RunsOptions.StaleAfter"/> as configured. Nothing is stored until somebody changes it,
/// so a machine where nobody did keeps <c>Runs:StaleAfter</c>.
/// </summary>
sealed class StaleThreshold(IServiceScopeFactory scopes, IOptions<RunsOptions> options)
{
    const string Name = "stale-after-seconds";

    public async Task<TimeSpan> ReadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await ReadAsync(scope.ServiceProvider.GetRequiredService<RunsDbContext>(), ct);
    }

    public async Task<TimeSpan> ReadAsync(RunsDbContext database, CancellationToken ct)
    {
        var stored = await database.Settings.AsNoTracking().Where(row => row.Name == Name).Select(row => row.Value).FirstOrDefaultAsync(ct);

        return int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : options.Value.StaleAfter;
    }

    /// <summary>Stores the threshold and saves.</summary>
    public async Task SetAsync(RunsDbContext database, int seconds, CancellationToken ct)
    {
        var value = seconds.ToString(CultureInfo.InvariantCulture);
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
