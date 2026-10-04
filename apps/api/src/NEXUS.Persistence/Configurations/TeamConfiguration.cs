using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(50);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.ExternalDirectoryId).HasMaxLength(256);
        builder.Property(t => t.EmailAlias).HasMaxLength(256);
        builder.HasIndex(t => t.Name);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.IsActive);

        // Backstops the TEAM-000001 code generator against concurrent creates,
        // mirroring the Vendor code convention. Mirrored on Resource.TeamId so the
        // legacy single-team pointer keeps a referential relationship.
        builder.HasIndex(t => new { t.TenantId, t.Code }).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.HasOne(t => t.Department)
            .WithMany(d => d.Teams)
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);    }
}