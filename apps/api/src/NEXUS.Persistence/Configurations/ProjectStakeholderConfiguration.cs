using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectStakeholderConfiguration : IEntityTypeConfiguration<ProjectStakeholder>
{
    public void Configure(EntityTypeBuilder<ProjectStakeholder> builder)
    {
        builder.ToTable("ProjectStakeholders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired(false);
        builder.Property(x => x.ResourceId).IsRequired(false);
        builder.Property(x => x.RelationshipType)
            .HasMaxLength(30)
            .HasDefaultValue("Stakeholder")
            .IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        // ProjectStakeholders predates the standardized SQL rowversion columns.
        // Keep its existing varbinary storage and rotate an application-managed
        // token on every mutation so existing data remains intact.
        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Stakeholders)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.UserId })
            .HasFilter("[IsDeleted] = 0 AND [Status] = 'Active' AND [UserId] IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.NormalizedEmail })
            .HasFilter("[IsDeleted] = 0 AND [Status] = 'ContactOnly' AND [NormalizedEmail] IS NOT NULL");
    }
}
