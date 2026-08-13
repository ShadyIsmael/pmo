using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain.Entities;

namespace NEXUS.Persistence.Configurations;

public class ProjectPlanDraftConfiguration : IEntityTypeConfiguration<ProjectPlanDraft>
{
    public void Configure(EntityTypeBuilder<ProjectPlanDraft> builder)
    {
        builder.ToTable("ProjectPlanDrafts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProjectId)
            .IsRequired();

        builder.Property(e => e.SourceBaselineVersionId)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.SubmittedChangeRequestId)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ProjectId })
            .HasDatabaseName("IX_ProjectPlanDrafts_Tenant_Project_Active")
            .IsUnique()
            .HasFilter("\"IsActive\" = 1");

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SourceBaselineVersion)
            .WithMany()
            .HasForeignKey(e => e.SourceBaselineVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.SubmittedChangeRequest)
            .WithMany()
            .HasForeignKey(e => e.SubmittedChangeRequestId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}