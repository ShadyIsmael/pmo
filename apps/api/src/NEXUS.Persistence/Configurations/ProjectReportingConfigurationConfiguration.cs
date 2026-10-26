using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectReportingConfigurationConfiguration : IEntityTypeConfiguration<ProjectReportingConfiguration>
{
    public void Configure(EntityTypeBuilder<ProjectReportingConfiguration> builder)
    {
        builder.ToTable("ProjectReportingConfigurations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReportingFrequency).HasMaxLength(20).IsRequired();
        builder.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.CurrencyCode).HasMaxLength(10).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.IncludedSectionsJson).IsRequired();
        builder.Property(item => item.ConfiguredBy).HasMaxLength(300).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.IsCurrent })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("IX_ProjectReportingConfigurations_Tenant_Project_Current");
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Plans)
            .WithOne(item => item.Configuration)
            .HasForeignKey(item => item.ProjectReportingConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectReportingPlanSelectionConfiguration : IEntityTypeConfiguration<ProjectReportingPlanSelection>
{
    public void Configure(EntityTypeBuilder<ProjectReportingPlanSelection> builder)
    {
        builder.ToTable("ProjectReportingPlanSelections");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PlanNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(item => item.PlanTypeSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.ProjectReportingConfigurationId, item.PlanId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PlanId });
    }
}
