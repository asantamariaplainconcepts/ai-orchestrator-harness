using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Settings;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Loop.Persistence;

/// <summary>The engine's own tables: its journal, the runs it asked for and its settings.</summary>
sealed class LoopDbContext(DbContextOptions<LoopDbContext> options) : DbContext(options)
{
    public const string Schema = "loop";

    public DbSet<JournalEntry> Journal => Set<JournalEntry>();

    public DbSet<AskedRun> AskedRuns => Set<AskedRun>();

    /// <summary>What a person changed of the engine from the console (#562).</summary>
    public DbSet<LoopSetting> Settings => Set<LoopSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.StoreDateTimeOffsetsAsBinary();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<JournalEntry>(entry =>
        {
            entry.ToTable("loop_journal");
            entry.HasKey(row => row.Id);
            entry.Property(row => row.Repository).HasMaxLength(200).IsRequired();
            entry.Property(row => row.Pbi).HasMaxLength(50).IsRequired();
            entry.Property(row => row.From).HasMaxLength(100).IsRequired();
            entry.Property(row => row.To).HasMaxLength(100);
            entry.Property(row => row.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
            entry.Property(row => row.Why).HasMaxLength(4000).IsRequired();
            entry.Property(row => row.Output).HasMaxLength(1000);

            // Every pass reads one PBI's lines; the screen reads the newest first.
            entry.HasIndex(row => new { row.Repository, row.Pbi });
        });

        modelBuilder.Entity<AskedRun>(run =>
        {
            run.ToTable("loop_asked_runs");
            run.HasKey(row => row.RunId);
            run.Property(row => row.RunId).ValueGeneratedNever();
            run.Property(row => row.Repository).HasMaxLength(200).IsRequired();
            run.Property(row => row.Pbi).HasMaxLength(50).IsRequired();
            run.Property(row => row.State).HasMaxLength(100).IsRequired();
            run.HasIndex(row => new { row.Repository, row.Pbi, row.Consumed });
        });

        modelBuilder.Entity<LoopSetting>(setting =>
        {
            setting.ToTable("loop_settings");
            setting.HasKey(row => row.Name);
            setting.Property(row => row.Name).HasMaxLength(250);
            setting.Property(row => row.Value).HasMaxLength(50).IsRequired();
        });
    }
}
