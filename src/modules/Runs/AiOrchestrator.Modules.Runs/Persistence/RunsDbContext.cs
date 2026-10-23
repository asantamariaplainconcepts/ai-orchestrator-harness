using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.Modules.Runs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Persistence;

sealed class RunsDbContext(DbContextOptions<RunsDbContext> options) : DbContext(options)
{
    public const string Schema = "runs";

    /// <summary>The runs: the loop's, and the manual ones on a PBI (#506).</summary>
    public DbSet<PbiRun> PbiRuns => Set<PbiRun>();

    /// <summary>What a person changed of Runs from the console (#562).</summary>
    public DbSet<Features.PbiRuns.RunsSetting> Settings => Set<Features.PbiRuns.RunsSetting>();

    // SQLite cannot order a TEXT DateTimeOffset; the guard keeps the Postgres side of the
    // one-off import on its native mapping. See BinaryDateTimeOffsets for the full story.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (Database.IsSqlite())
        {
            configurationBuilder.StoreDateTimeOffsetsAsBinary();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<PbiRun>(run =>
        {
            run.ToTable("pbi_runs");
            run.HasKey(entity => entity.Id);
            run.Property(entity => entity.Repository).HasMaxLength(240).IsRequired();
            run.Property(entity => entity.PbiId).HasMaxLength(100).IsRequired();
            run.Property(entity => entity.State).HasMaxLength(100).IsRequired();
            run.Property(entity => entity.FolderPath).HasMaxLength(1000).IsRequired();
            run.Property(entity => entity.Branch).HasMaxLength(250).IsRequired();
            run.Property(entity => entity.Provider).HasMaxLength(50);
            run.Property(entity => entity.Model).HasMaxLength(100);
            run.Property(entity => entity.Command).HasMaxLength(4000).IsRequired();
            run.Property(entity => entity.OutcomesJson).IsRequired();
            run.Property(entity => entity.EndRecordPath).HasMaxLength(1000).IsRequired();
            run.Property(entity => entity.RecordingPath).HasMaxLength(1000).IsRequired();
            run.Property(entity => entity.Environment).HasMaxLength(50);
            run.Property(entity => entity.WorkingDirectory).HasMaxLength(1000);
            run.Property(entity => entity.Outcome).HasMaxLength(100);
            run.Property(entity => entity.Failure).HasConversion<string>().HasMaxLength(50);
            run.Property(entity => entity.ProblemsJson).IsRequired();

            // Money as a number, never a float, and four places because a short run rounds to zero at two.
            run.Property(entity => entity.CostUsd).HasPrecision(12, 4);
            run.Ignore(entity => entity.Ended);
            run.Ignore(entity => entity.Pbi);
            run.Ignore(entity => entity.Outcomes);
            run.Ignore(entity => entity.Problems);

            // The PBI's Journey reads its runs, and the engine reads the ones still live.
            run.HasIndex(entity => new { entity.Repository, entity.PbiId });
            run.HasIndex(entity => entity.EndedAt);
        });

        modelBuilder.Entity<Features.PbiRuns.RunsSetting>(setting =>
        {
            setting.ToTable("runs_settings");
            setting.HasKey(row => row.Name);
            // Room for an environment's key: its prefix, a repository of 200 and a state of 100 (#612).
            setting.Property(row => row.Name).HasMaxLength(320);
            setting.Property(row => row.Value).HasMaxLength(50).IsRequired();
        });

        // Ids come from the domain, never from Postgres. Without this, a child appended to an
        // owned collection of an already-tracked parent is UPDATEd instead of INSERTed.
        modelBuilder.IdsAreDomainGenerated();
    }
}
