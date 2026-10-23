using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.Modules.Workspace.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Persistence;

sealed class WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : DbContext(options)
{
    public const string Schema = "workspace";

    public DbSet<Folder> Folders => Set<Folder>();

    public DbSet<Preference> Preferences => Set<Preference>();

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

        modelBuilder.Entity<Preference>(preference =>
        {
            preference.ToTable("preferences");
            preference.HasKey(entity => entity.Id);
            preference.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
            preference.Property(entity => entity.Value).HasMaxLength(2000).IsRequired();

            // One row per name. Two would each be half the answer, and which half won would
            // depend on insertion order.
            preference.HasIndex(entity => entity.Name).IsUnique();
        });

        modelBuilder.Entity<Folder>(folder =>
        {
            folder.ToTable("folders");
            folder.HasKey(entity => entity.Id);
            folder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
            folder.Property(entity => entity.Path).HasMaxLength(1000).IsRequired();
            folder.Property(entity => entity.DefaultAgentId).HasMaxLength(50).IsRequired();
            folder.Property(entity => entity.DefaultModel).HasMaxLength(100);
            folder.Property(entity => entity.Branch).HasMaxLength(400);
            folder.Property(entity => entity.RemoteUrl).HasMaxLength(1000);

            // A folder is identified by where it is, not by what it is called. Two rows for one
            // path would each carry half its runs.
            folder.HasIndex(entity => entity.Path).IsUnique();
        });

        // Ids come from the domain, never from the database.
        modelBuilder.IdsAreDomainGenerated();
    }
}
