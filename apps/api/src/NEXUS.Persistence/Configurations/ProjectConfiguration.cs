using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(50);
        builder.Property(p => p.Status).HasMaxLength(64);
        builder.Property(p => p.Priority).HasMaxLength(50);
        builder.Property(p => p.Health).HasMaxLength(50);
        builder.Property(p => p.ConfidenceLevel).HasMaxLength(50);
        builder.Property(p => p.FinanceApprovalMode).HasMaxLength(24);
        builder.Property(p => p.Phase).HasMaxLength(100);
        builder.Property(p => p.RiskRating).HasMaxLength(50);
        builder.HasIndex(p => p.TenantId);
        builder.Property(p => p.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ProjectManagerId);
        builder.Property(p => p.WorkingCalendarId).HasColumnType("uniqueidentifier");
        builder.HasIndex(p => p.WorkingCalendarId);
        builder.HasIndex(p => new { p.TenantId, p.ProjectPhaseId });
        builder.HasIndex(p => new { p.TenantId, p.ProjectStatusId });
        builder.HasOne<NEXUS.Modules.ProjectManagement.Domain.ProjectPhaseCatalogEntry>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.ProjectPhaseId })
            .HasPrincipalKey(entry => new { entry.TenantId, entry.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NEXUS.Modules.ProjectManagement.Domain.ProjectStatusCatalogEntry>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.ProjectStatusId })
            .HasPrincipalKey(entry => new { entry.TenantId, entry.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
