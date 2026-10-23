using AiOrchestrator.BuildingBlocks.Folders;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Persistence;

/// <summary>
/// Where the database file is when nobody said. One SQLite file in the owner's home directory
/// (ADR-0003), supplied as configuration of last resort so that a host started by nothing still
/// has somewhere to read and write.
/// <para>
/// This lives in ServiceDefaults rather than in each host for the reason
/// <c>AddSecretResolution</c> gives for sitting here: the Server and the MigrationService must not
/// be able to drift apart on where the database is, because the one that migrates and the one that
/// queries disagreeing is a schema-less database reported as a broken product.
/// </para>
/// <para>
/// <b>It cannot be an appsettings file.</b> SQLite does not expand <c>~</c>, and the
/// MigrationService has no appsettings file at all — the default has to be computed by something
/// both hosts call.
/// </para>
/// </summary>
public static class LocalDatabase
{
    /// <summary>
    /// The connection-string name every module reads. Duplicated as a constant per module today;
    /// this is the composition side of the same name, and the one place a habitat without an
    /// orchestrator fills it in.
    /// </summary>
    public const string ConnectionStringName = "aiorchestratordb";

    /// <summary>
    /// Where an installed app keeps its data. Not the checkout — <c>AppHost.cs</c> derives that
    /// path for the dev loop and an installed app has no checkout above it, which
    /// <see cref="SelfCheckout.Root"/> answers null for by design.
    /// <para>
    /// <c>~/.aiorchestrator</c> rather than <c>~/Library/Application Support</c>: the packaged
    /// launcher already creates this directory at mode 0700 to hold state, the product already
    /// reads <c>~/.harness/workflows</c> from home, and one spelling works on the three platforms
    /// #35 is about instead of each of them needing its own.
    /// </para>
    /// </summary>
    public const string DefaultPath = "~/.aiorchestrator/aiorchestrator.db";

    /// <summary>
    /// Fills in the connection string when nothing else did, and makes sure the directory holding
    /// it exists.
    /// <para>
    /// <b>Read first, then fill — never append unconditionally.</b> Environment variables are
    /// registered before this runs, and in <c>ConfigurationManager</c> the later source wins, so a
    /// default appended without looking would beat the <c>ConnectionStrings__aiorchestratordb</c>
    /// the AppHost sets on every resource and point every worktree at one shared file in
    /// <c>$HOME</c>. That failure has the shape of #61's stale <c>wwwroot</c>: it works, and it
    /// works against the wrong data. Filling only an absent value cannot have it, and it degrades
    /// in the safe direction — anything registered after this still wins.
    /// </para>
    /// <para>
    /// The directory is created here because <c>Microsoft.Data.Sqlite</c> creates a missing file
    /// but not a missing directory — the same reason <c>AppHost.cs</c> creates the dev database's
    /// directory at composition time. At 0700, because the launcher that used to be the only thing
    /// making this directory made it 0700, and a habitat with no launcher must not be the loose
    /// one.
    /// </para>
    /// </summary>
    public static TBuilder AddLocalDatabase<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var declared = builder.Configuration.GetConnectionString(ConnectionStringName);

        if (!string.IsNullOrWhiteSpace(declared))
        {
            return builder;
        }

        var file = LocalPath.Expand(DefaultPath);

        CreatePrivateDirectoryFor(file);

        builder.Configuration.AddInMemoryCollection(
            [new($"ConnectionStrings:{ConnectionStringName}", $"Data Source={file}")]
        );

        return builder;
    }

    static void CreatePrivateDirectoryFor(string file)
    {
        var directory = Path.GetDirectoryName(file);

        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        // The mode is set on creation rather than after, so there is no window in which the
        // directory that is about to hold the database is readable by anybody else. Windows has no
        // mode to set and the overload throws there rather than ignoring it.
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(
            directory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
    }
}
