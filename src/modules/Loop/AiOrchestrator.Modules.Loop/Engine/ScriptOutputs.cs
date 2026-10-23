using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// Keeps what each loop script printed, one file per run of a script (#638): on 11 October a
/// dogfood PBI held in <c>verifying</c> on "aspire do ci failed", the same command passed by hand a
/// minute later, and the output that would have said why was in nobody's file. The path goes on
/// the script's <c>ran</c> line, and the Journey links to it through <c>ReadScriptOutput</c>.
/// </summary>
sealed class ScriptOutputs(IConfiguration configuration, IOptions<LoopSettings> settings)
{
    /// <summary>
    /// <c>&lt;directory&gt;/&lt;owner&gt;/&lt;name&gt;/&lt;pbi&gt;/&lt;script&gt;-&lt;utc time&gt;.log</c>, laid out
    /// as Runs lays out <c>runs/&lt;run id&gt;/</c> beside it: the PBI's files together, oldest first by name.
    /// </summary>
    public async Task<string> WriteAsync(PbiRef pbi, string script, DateTimeOffset at, string output, CancellationToken ct)
    {
        var directory = Path.Combine(Directory(), pbi.Repository.Replace('/', Path.DirectorySeparatorChar), pbi.Id);
        System.IO.Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{script}-{at.UtcDateTime:yyyyMMdd-HHmmss-fff}.log");
        await File.WriteAllTextAsync(path, output, ct);
        return path;
    }

    string Directory()
    {
        if (settings.Value.ScriptsDirectory is { Length: > 0 } configured)
        {
            return configured;
        }

        var connection = configuration.GetConnectionString(LoopModule.ConnectionStringName);
        var database = connection is null ? null : new SqliteConnectionStringBuilder(connection).DataSource;

        return database is { Length: > 0 } && Path.GetDirectoryName(Path.GetFullPath(database)) is { } beside
            ? Path.Combine(beside, "scripts")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiOrchestrator", "scripts");
    }
}
