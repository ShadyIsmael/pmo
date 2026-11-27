using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectPhaseCatalogEntryConfiguration : IEntityTypeConfiguration<ProjectPhaseCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseCatalogEntry> builder)
    {
        builder.ToTable("ProjectPhaseCatalogEntries", table => table.HasCheckConstraint(
            "CK_ProjectPhaseCatalogEntries_DisplayOrder", "[DisplayOrder] >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(64).IsRequired();
        builder.Property(item => item.NormalizedCode).HasMaxLength(64).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.DescriptionEn).HasMaxLength(4000);
        builder.Property(item => item.DescriptionAr).HasMaxLength(4000);
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.Property(item => item.IsDefault).HasDefaultValue(false);
        builder.Property(item => item.IsContinuousActivity).HasDefaultValue(false);
        builder.Property(item => item.IsDeleted).HasDefaultValue(false);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id })
            .HasName("AK_ProjectPhaseCatalogEntries_Tenant_Id");
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentCategory>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.DocumentCategoryId })
            .HasPrincipalKey(category => new { category.TenantId, category.Id })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.NormalizedCode })
            .IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectPhaseCatalogEntries_Scope_Code");
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [IsDefault] = 1")
            .HasDatabaseName("UX_ProjectPhaseCatalogEntries_Scope_Default");
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.IsActive, item.DisplayOrder })
            .HasDatabaseName("IX_ProjectPhaseCatalogEntries_Scope_Order");
        ConfigureAuditFields(builder);
    }

    internal static void ConfigureAuditFields<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2");
        builder.Property(item => item.UpdatedBy).HasMaxLength(100);
    }
}

public sealed class ProjectStatusCatalogEntryConfiguration : IEntityTypeConfiguration<ProjectStatusCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ProjectStatusCatalogEntry> builder)
    {
        builder.ToTable("ProjectStatusCatalogEntries", table =>
        {
            table.HasCheckConstraint("CK_ProjectStatusCatalogEntries_DisplayOrder", "[DisplayOrder] >= 0");
            table.HasCheckConstraint("CK_ProjectStatusCatalogEntries_ReportingCategory", "[ReportingCategory] IN ('InProgress', 'Completed', 'Excluded')");
            table.HasCheckConstraint("CK_ProjectStatusCatalogEntries_AttentionLevel", "[AttentionLevel] IN ('None', 'NeedsAttention', 'Critical')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(64).IsRequired();
        builder.Property(item => item.NormalizedCode).HasMaxLength(64).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.DescriptionEn).HasMaxLength(4000);
        builder.Property(item => item.DescriptionAr).HasMaxLength(4000);
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.Property(item => item.IsDefault).HasDefaultValue(false);
        builder.Property(item => item.ReportingCategory).HasMaxLength(24).HasDefaultValue(ProjectStatusReportingCategories.InProgress).IsRequired();
        builder.Property(item => item.AttentionLevel).HasMaxLength(24).HasDefaultValue(ProjectStatusAttentionLevels.None).IsRequired();
        builder.Property(item => item.IsDeleted).HasDefaultValue(false);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id })
            .HasName("AK_ProjectStatusCatalogEntries_Tenant_Id");
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.NormalizedCode })
            .IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectStatusCatalogEntries_Scope_Code");
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [IsDefault] = 1")
            .HasDatabaseName("UX_ProjectStatusCatalogEntries_Scope_Default");
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.IsActive, item.DisplayOrder })
            .HasDatabaseName("IX_ProjectStatusCatalogEntries_Scope_Order");
        ProjectPhaseCatalogEntryConfiguration.ConfigureAuditFields(builder);
    }
}

public sealed class ProjectPhaseTransitionRuleConfiguration : IEntityTypeConfiguration<ProjectPhaseTransitionRule>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseTransitionRule> builder)
    {
        builder.ToTable("ProjectPhaseTransitionRules");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.Property(item => item.IsDeleted).HasDefaultValue(false);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SourcePhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.TargetPhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.SourcePhaseId, item.TargetPhaseId })
            .IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectPhaseTransitionRules_Scope_Transition");
        ProjectPhaseCatalogEntryConfiguration.ConfigureAuditFields(builder);
    }
}

public sealed class ProjectStatusTransitionRuleConfiguration : IEntityTypeConfiguration<ProjectStatusTransitionRule>
{
    public void Configure(EntityTypeBuilder<ProjectStatusTransitionRule> builder)
    {
        builder.ToTable("ProjectStatusTransitionRules");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.Property(item => item.IsDeleted).HasDefaultValue(false);
        builder.Property(item => item.RequiresReason).HasDefaultValue(false);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<ProjectStatusCatalogEntry>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SourceStatusId })
            .HasPrincipalKey(status => new { status.TenantId, status.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectStatusCatalogEntry>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.TargetStatusId })
            .HasPrincipalKey(status => new { status.TenantId, status.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.SourceStatusId, item.TargetStatusId })
            .IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ProjectStatusTransitionRules_Scope_Transition");
        ProjectPhaseCatalogEntryConfiguration.ConfigureAuditFields(builder);
    }
}
