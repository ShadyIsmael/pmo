using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Planing.Domain.Entities;

namespace Pmo.Planing.Infrastructure.Contexts;

public class ProjectBaselineVersionConfiguration : IEntityTypeConfiguration<ProjectBaselineVersion>
{
    public void Configure(EntityTypeBuilder<ProjectBaselineVersion> builder)
    {
        builder.ToTable("ProjectBaselineVersions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ApprovedBy)
            .HasMaxLength(100);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        // Configure Value Object conversions
        builder.OwnsOne(e => e.ProjectId, b =>
        {
            b.Property(p => p.Value)
                .HasColumnName("ProjectId")
                .IsRequired()
                .HasMaxLength(100);
        });

        builder.OwnsOne(e => e.VersionNumber, b =>
        {
            b.Property(v => v.Value)
                .HasColumnName("VersionNumber")
                .IsRequired();
        });

        // Index for unique version number per tenant/project
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_Tenant_Project_Version");

        // Index for only one current approved baseline per project
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.IsCurrent, e.Status })
            .HasDatabaseName("IX_Tenant_Project_CurrentApproved")
            .HasFilter("\"IsCurrent\" = 1 AND \"Status\" = 2"); // Assuming 2 is the enum value for 'Approved'

        // Relationship for ApprovedBaselineMetadata
        builder.HasOne(e => e.ApprovedBaselineMetadata)
            .WithMany()
            .HasForeignKey(e => e.ApprovedBaselineMetadataId)
            .OnDelete(DeleteBehavior.NoAction); // Or Restrict depending on DB policy
    }
}