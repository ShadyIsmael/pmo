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

        // A project can contain multiple plans, but only one of each PlanType
        // (Execution/Recovery/Tender/...) per tenant/project.
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.PlanType })
            .IsUnique()
            .HasDatabaseName("IX_Plans_Tenant_Project_Type");

        builder.HasMany(e => e.Versions)
            .WithOne(v => v.Plan)
            .HasForeignKey(v => v.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}