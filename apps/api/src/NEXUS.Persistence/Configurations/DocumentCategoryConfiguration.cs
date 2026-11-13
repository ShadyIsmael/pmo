using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class DocumentCategoryConfiguration : IEntityTypeConfiguration<DocumentCategory>
{
    public void Configure(EntityTypeBuilder<DocumentCategory> builder)
    {
        builder.ToTable("DocumentCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.NameAr).HasMaxLength(200);
        builder.Property(category => category.NormalizedNameEn).HasMaxLength(400).IsRequired();
        builder.Property(category => category.NormalizedNameAr).HasMaxLength(400);
        builder.Property(category => category.IsActive).HasDefaultValue(true).IsRequired();

        // Composite ownership prevents a category from pointing at an Organization
        // in another tenant. Legacy rows retain OrganizationId = null.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(category => new { category.TenantId, category.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Inactive names remain reserved so reactivation cannot create ambiguous
        // duplicate categories. Legacy tenant-wide rows are intentionally excluded.
        builder.HasIndex(category => new { category.TenantId, category.OrganizationId, category.NormalizedNameEn })
            .IsUnique()
            .HasDatabaseName("UX_DocumentCategories_Organization_NameEn")
            .HasFilter("[OrganizationId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(category => new { category.TenantId, category.OrganizationId, category.NormalizedNameAr })
            .IsUnique()
            .HasDatabaseName("UX_DocumentCategories_Organization_NameAr")
            .HasFilter("[OrganizationId] IS NOT NULL AND [IsDeleted] = 0 AND [NormalizedNameAr] IS NOT NULL");
    }
}
