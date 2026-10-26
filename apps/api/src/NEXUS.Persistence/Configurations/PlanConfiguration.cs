using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProjectId)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.PlanType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // A project may have parallel plans of the same type. Keep names unique
        // per plan type without constraining names used by a different type.
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.PlanType, e.Name })
            .IsUnique()
            .HasDatabaseName("IX_Plans_Tenant_Project_Type_Name");

        builder.HasMany(e => e.Versions)
            .WithOne(v => v.Plan)
            .HasForeignKey(v => v.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
