using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanDeliverableConfiguration : IEntityTypeConfiguration<PlanDeliverable>
{
    public void Configure(EntityTypeBuilder<PlanDeliverable> builder)
    {
        builder.ToTable("PlanDeliverables");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanVersionId)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.AcceptanceCriteria)
            .HasMaxLength(4000);

        builder.Property(e => e.DueDate);

        builder.Property(e => e.WbsId)
            .IsRequired(false);

        builder.Property(e => e.ActivityId)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.Name })
            .IsUnique()
            .HasDatabaseName("IX_PlanDeliverables_Tenant_Version_Name");

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.Deliverables)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}