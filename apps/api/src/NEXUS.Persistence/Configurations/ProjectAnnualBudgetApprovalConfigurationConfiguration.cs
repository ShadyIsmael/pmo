using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectAnnualBudgetApprovalConfigurationConfiguration
    : IEntityTypeConfiguration<ProjectAnnualBudgetApprovalConfiguration>
{
    public void Configure(EntityTypeBuilder<ProjectAnnualBudgetApprovalConfiguration> builder)
    {
        builder.ToTable("ProjectAnnualBudgetApprovalConfigurations", table =>
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgetApprovalConfigurations_Mode",
                "[ApprovalMode] IN (N'Sequential', N'Parallel')"));

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project_Id");
        builder.Property(item => item.ApprovalMode).HasMaxLength(24).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project");
    }
}

public sealed class ProjectAnnualBudgetApprovalApproverConfiguration
    : IEntityTypeConfiguration<ProjectAnnualBudgetApprovalApprover>
{
    public void Configure(EntityTypeBuilder<ProjectAnnualBudgetApprovalApprover> builder)
    {
        builder.ToTable("ProjectAnnualBudgetApprovalApprovers", table =>
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgetApprovalApprovers_Position",
                "[Position] IS NULL OR [Position] > 0"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(item => item.Configuration)
            .WithMany(item => item.Approvers)
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ConfigurationId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ApproverUserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ConfigurationId, item.ApproverUserId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectAnnualBudgetApprovalApprovers_Configuration_User");
        builder.HasIndex(item => new { item.TenantId, item.ConfigurationId, item.Position })
            .HasFilter("[IsDeleted] = 0 AND [Position] IS NOT NULL")
            .HasDatabaseName("IX_ProjectAnnualBudgetApprovalApprovers_Configuration_Position");
    }
}
