using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain.Entities;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectPhaseGateSnapshotConfiguration : IEntityTypeConfiguration<ProjectPhaseGateSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseGateSnapshot> builder)
    {
        builder.ToTable("ProjectPhaseGateSnapshots", table =>
        {
            table.HasCheckConstraint(
                "CK_ProjectPhaseGateSnapshots_DistinctPhases",
                "[SourcePhaseId] <> [TargetPhaseId]");
            table.HasCheckConstraint(
                "CK_ProjectPhaseGateSnapshots_ConfigurationVersionPositive",
                "[ConfigurationVersionNumber] >= 1");
        });

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.TenantId).IsRequired();
        builder.Property(snapshot => snapshot.ProjectId).IsRequired();
        builder.Property(snapshot => snapshot.OrganizationId).IsRequired();
        builder.Property(snapshot => snapshot.SourcePhaseId).IsRequired();
        builder.Property(snapshot => snapshot.TargetPhaseId).IsRequired();
        builder.Property(snapshot => snapshot.SourcePhase).HasMaxLength(64).IsRequired();
        builder.Property(snapshot => snapshot.TargetPhase).HasMaxLength(64).IsRequired();
        builder.Property(snapshot => snapshot.ConfigurationId).IsRequired();
        builder.Property(snapshot => snapshot.ConfigurationVersionId).IsRequired();
        builder.Property(snapshot => snapshot.ConfigurationVersionNumber).IsRequired();
        ConfigureAuditFields(builder);

        builder.HasAlternateKey(snapshot => new { snapshot.TenantId, snapshot.Id })
            .HasName("AK_ProjectPhaseGateSnapshots_Tenant_Id");
        builder.HasOne<Project>().WithMany()
            .HasForeignKey(snapshot => new { snapshot.TenantId, snapshot.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(snapshot => new { snapshot.TenantId, snapshot.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(snapshot => new { snapshot.TenantId, snapshot.SourcePhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(snapshot => new { snapshot.TenantId, snapshot.TargetPhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationPhaseGateConfiguration>().WithMany()
            .HasForeignKey(snapshot => new { snapshot.TenantId, snapshot.ConfigurationId })
            .HasPrincipalKey(configuration => new { configuration.TenantId, configuration.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationPhaseGateConfigurationVersion>().WithMany()
            .HasForeignKey(snapshot => new
            {
                snapshot.TenantId,
                snapshot.ConfigurationId,
                snapshot.ConfigurationVersionId
            })
            .HasPrincipalKey(version => new { version.TenantId, version.ConfigurationId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(snapshot => new
            {
                snapshot.TenantId,
                snapshot.ProjectId,
                snapshot.OrganizationId,
                snapshot.SourcePhaseId,
                snapshot.TargetPhaseId
            })
            .IsUnique()
            .HasDatabaseName("UX_ProjectPhaseGateSnapshots_Project_Transition");
        builder.Property(snapshot => snapshot.RowVersion).IsRowVersion().IsConcurrencyToken();
    }

    private static void ConfigureAuditFields<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnType("datetime2");
        builder.Property(entity => entity.UpdatedBy).HasMaxLength(100);
        builder.Property(entity => entity.IsDeleted).HasDefaultValue(false);
    }
}

public sealed class ProjectPhaseGateRequirementSnapshotConfiguration
    : IEntityTypeConfiguration<ProjectPhaseGateRequirementSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseGateRequirementSnapshot> builder)
    {
        builder.ToTable("ProjectPhaseGateRequirementSnapshots", table =>
        {
            table.HasCheckConstraint(
                "CK_ProjectPhaseGateRequirementSnapshots_RequirementType",
                "[RequirementType] IN (N'Mandatory', N'Optional')");
        });

        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).ValueGeneratedNever();
        builder.Property(requirement => requirement.TenantId).IsRequired();
        builder.Property(requirement => requirement.SnapshotId).IsRequired();
        builder.Property(requirement => requirement.SourceRequirementId).IsRequired();
        builder.Property(requirement => requirement.TemplateId).IsRequired();
        builder.Property(requirement => requirement.TemplateVersionId).IsRequired();
        builder.Property(requirement => requirement.TemplateName).HasMaxLength(250).IsRequired();
        builder.Property(requirement => requirement.Category).HasMaxLength(200);
        builder.Property(requirement => requirement.TemplateVersionLabel).HasMaxLength(32).IsRequired();
        builder.Property(requirement => requirement.RequirementType).HasMaxLength(20).IsRequired();
        builder.Property(requirement => requirement.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(requirement => requirement.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(requirement => requirement.UpdatedAt).HasColumnType("datetime2");
        builder.Property(requirement => requirement.UpdatedBy).HasMaxLength(100);
        builder.Property(requirement => requirement.IsDeleted).HasDefaultValue(false);
        builder.Property(requirement => requirement.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasAlternateKey(requirement => new { requirement.TenantId, requirement.Id })
            .HasName("AK_ProjectPhaseGateRequirementSnapshots_Tenant_Id");
        builder.HasOne<ProjectPhaseGateSnapshot>().WithMany()
            .HasForeignKey(requirement => new { requirement.TenantId, requirement.SnapshotId })
            .HasPrincipalKey(snapshot => new { snapshot.TenantId, snapshot.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationPhaseGateRequirement>().WithMany()
            .HasForeignKey(requirement => new { requirement.TenantId, requirement.SourceRequirementId })
            .HasPrincipalKey(source => new { source.TenantId, source.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>().WithMany()
            .HasForeignKey(requirement => new
            {
                requirement.TenantId,
                requirement.TemplateId,
                requirement.TemplateVersionId
            })
            .HasPrincipalKey(version => new { version.TenantId, version.DocumentId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(requirement => new
            {
                requirement.TenantId,
                requirement.SnapshotId,
                requirement.TemplateId
            })
            .IsUnique()
            .HasDatabaseName("UX_ProjectPhaseGateRequirementSnapshots_Snapshot_Template");
    }
}

public sealed class ProjectPhaseGateEvidenceConfiguration : IEntityTypeConfiguration<ProjectPhaseGateEvidence>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseGateEvidence> builder)
    {
        builder.ToTable("ProjectPhaseGateEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Id).ValueGeneratedNever();
        builder.Property(evidence => evidence.TenantId).IsRequired();
        builder.Property(evidence => evidence.ProjectId).IsRequired();
        builder.Property(evidence => evidence.OrganizationId).IsRequired();
        builder.Property(evidence => evidence.RequirementSnapshotId).IsRequired();
        builder.Property(evidence => evidence.DocumentId).IsRequired();
        builder.Property(evidence => evidence.DocumentVersionId).IsRequired();
        builder.Property(evidence => evidence.ComplianceStatus).HasMaxLength(32).HasDefaultValue("PendingValidation").IsRequired();
        builder.Property(evidence => evidence.ExpectedFileType).HasMaxLength(16);
        builder.Property(evidence => evidence.DetectedFileType).HasMaxLength(16);
        builder.Property(evidence => evidence.ComplianceFindingsJson).HasColumnType("nvarchar(max)");
        builder.Property(evidence => evidence.ComparedAt).HasColumnType("datetime2");
        builder.Property(evidence => evidence.ValidatorVersion).HasMaxLength(64);
        builder.Property(evidence => evidence.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(evidence => evidence.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(evidence => evidence.UpdatedAt).HasColumnType("datetime2");
        builder.Property(evidence => evidence.UpdatedBy).HasMaxLength(100);
        builder.Property(evidence => evidence.IsDeleted).HasDefaultValue(false);
        builder.Property(evidence => evidence.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>().WithMany()
            .HasForeignKey(evidence => new { evidence.TenantId, evidence.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(evidence => new { evidence.TenantId, evidence.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseGateRequirementSnapshot>().WithMany()
            .HasForeignKey(evidence => new { evidence.TenantId, evidence.RequirementSnapshotId })
            .HasPrincipalKey(requirement => new { requirement.TenantId, requirement.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Document>().WithMany()
            .HasForeignKey(evidence => new { evidence.TenantId, evidence.DocumentId })
            .HasPrincipalKey(document => new { document.TenantId, document.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>().WithMany()
            .HasForeignKey(evidence => new
            {
                evidence.TenantId,
                evidence.DocumentId,
                evidence.DocumentVersionId
            })
            .HasPrincipalKey(version => new { version.TenantId, version.DocumentId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(evidence => new { evidence.TenantId, evidence.RequirementSnapshotId })
            .IsUnique()
            .HasDatabaseName("UX_ProjectPhaseGateEvidence_ActiveRequirement")
            .HasFilter("[IsDeleted] = 0");
    }
}
