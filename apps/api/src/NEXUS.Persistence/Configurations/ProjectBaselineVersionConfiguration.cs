using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain.Entities;

namespace NEXUS.Persistence.Configurations;

public class ProjectBaselineVersionConfiguration : IEntityTypeConfiguration<ProjectBaselineVersion>
{
    public void Configure(EntityTypeBuilder<ProjectBaselineVersion> builder)
    {
        builder.ToTable("ProjectBaselineVersions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.ApprovedBy)
            .HasMaxLength(100);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Configure Value Object conversions explicitly
        builder.Property(e => e.ProjectId)
            .HasConversion(
                v => v.Value,
                v => new ProjectId(v))
            .HasColumnName("ProjectId")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.VersionNumber)
            .HasConversion(
                v => v.Value,
                v => new VersionNumber(v))
            .HasColumnName("VersionNumber")
            .IsRequired();

        // Index for unique version number per tenant/project
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_Tenant_Project_Version");

        // Index for only one current approved baseline per tenant/project
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.IsCurrent, e.Status })
            .HasDatabaseName("IX_Tenant_Project_CurrentApproved")
            .HasFilter("\"IsCurrent\" = 1 AND \"Status\" = 2"); // Status.Approved == 2

        // Relationship for ApprovedBaselineMetadata
        builder.HasOne(e => e.ApprovedBaselineMetadata)
            .WithMany()
            .HasForeignKey(e => e.ApprovedBaselineMetadataId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}