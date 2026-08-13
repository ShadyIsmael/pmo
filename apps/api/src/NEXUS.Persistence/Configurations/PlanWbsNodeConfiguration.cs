using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanWbsNodeConfiguration : IEntityTypeConfiguration<PlanWbsNode>
{
    public void Configure(EntityTypeBuilder<PlanWbsNode> builder)
    {
        builder.ToTable("PlanWbsNodes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanVersionId)
            .IsRequired();

        builder.Property(e => e.ParentWbsId)
            .IsRequired(false);

        builder.Property(e => e.SortOrder)
            .IsRequired();

        builder.Property(e => e.Code)
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.ActivityId)
            .IsRequired(false);

        builder.Property(e => e.DeliverableId)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.ParentWbsId, e.Code })
            .IsUnique()
            .HasDatabaseName("IX_PlanWbsNodes_Tenant_Version_Parent_Code");

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.WbsNodes)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Parent)
            .WithMany()
            .HasForeignKey(e => e.ParentWbsId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}