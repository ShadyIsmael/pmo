using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectAnnualBudgetLineConfiguration : IEntityTypeConfiguration<ProjectAnnualBudgetLine>
{
    public void Configure(EntityTypeBuilder<ProjectAnnualBudgetLine> builder)
    {
        builder.ToTable("ProjectAnnualBudgetLines", table =>
        {
            table.HasCheckConstraint("CK_ProjectAnnualBudgetLines_Description", "LEN(LTRIM(RTRIM([Description]))) > 0");
            table.HasCheckConstraint("CK_ProjectAnnualBudgetLines_AuthorizedLimit", "[AuthorizedLimit] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId, item.Id })
            .HasName("AK_ProjectAnnualBudgetLines_Tenant_Project_Budget_Id");
        builder.Property(item => item.Reference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(200).IsRequired();
        builder.Property(item => item.AuthorizedLimit).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudget>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId })
            .HasPrincipalKey(budget => new { budget.TenantId, budget.ProjectId, budget.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId, item.IsDeleted })
            .HasDatabaseName("IX_ProjectAnnualBudgetLines_Tenant_Project_Budget_Active");
        builder.HasIndex(item => new { item.TenantId, item.Reference })
            .IsUnique().HasDatabaseName("UX_ProjectAnnualBudgetLines_Tenant_Reference");
    }
}

