using System.Globalization;
using AiOrchestrator.Modules.Loop.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Loop.Settings;

/// <summary>
/// One setting of the engine a person changed from the console (#562), keyed by name. The names are
/// the closed set of <see cref="ControlSettings"/>; nothing else writes a row.
/// </summary>
sealed class LoopSetting
{
    LoopSetting() { }

    public LoopSetting(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public void Set(string value) => Value = value;
}

/// <summary>What the engine follows on one pass: its interval, and which repositories it works.</summary>
/// <param name="IntervalSeconds">Seconds between two scheduled passes; zero or less schedules nothing.</param>
/// <param name="Stored">The switch a person set for each repository, by repository.</param>
/// <param name="OnByDefault"><c>Loop:Enabled</c>, for a repository nobody switched.</param>
sealed record ControlSnapshot(int IntervalSeconds, IReadOnlyDictionary<string, bool> Stored, bool OnByDefault)
{
    public bool IsOn(string repository) => Stored.TryGetValue(repository, out var on) ? on : OnByDefault;
}

/// <summary>
/// The engine's settings as a person left them in the console, read on every pass (#562): the
/// interval between passes and the switch of each folder, stored in <c>loop_settings</c>.
/// <para>
/// <b>Configuration is the default, not a copy.</b> A setting nobody changed is not stored, so it
/// reads <c>Loop:IntervalSeconds</c> and <c>Loop:Enabled</c> as they are today, and a machine where
/// nobody opened Settings behaves exactly as configured: with <c>Loop:Enabled</c> false every
/// folder starts off.
/// </para>
/// <para>
/// A switch is kept per repository, not per checkout, because the engine walks one checkout per
/// repository (<c>IFolderDirectory</c>) and its journal is keyed by repository.
/// </para>
/// </summary>
sealed class ControlSettings(LoopDbContext database, IOptions<LoopSettings> configured)
{
    const string Interval = "interval-seconds";
    const string EnginePrefix = "engine:";
    const string On = "on";
    const string Off = "off";

    public async Task<ControlSnapshot> ReadAsync(CancellationToken ct)
    {
        var rows = await database.Settings.AsNoTracking().ToListAsync(ct);

        var interval =
            rows.FirstOrDefault(row => row.Name == Interval) is { } stored
            && int.TryParse(stored.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : configured.Value.IntervalSeconds;

        var switches = rows.Where(row => row.Name.StartsWith(EnginePrefix, StringComparison.Ordinal))
            .ToDictionary(row => row.Name[EnginePrefix.Length..], row => row.Value == On, StringComparer.OrdinalIgnoreCase);

        return new ControlSnapshot(interval, switches, configured.Value.Enabled);
    }

    /// <summary>Stores the interval. The caller saves.</summary>
    public Task SetIntervalAsync(int seconds, CancellationToken ct) =>
        Upsert(Interval, seconds.ToString(CultureInfo.InvariantCulture), ct);

    /// <summary>Stores a repository's switch. The caller saves.</summary>
    public Task SetOnAsync(string repository, bool on, CancellationToken ct) => Upsert(EnginePrefix + repository, on ? On : Off, ct);

    async Task Upsert(string name, string value, CancellationToken ct)
    {
        var existing = await database.Settings.FirstOrDefaultAsync(row => row.Name == name, ct);
        if (existing is null)
        {
            database.Settings.Add(new LoopSetting(name, value));
            return;
        }

        existing.Set(value);
    }
}
