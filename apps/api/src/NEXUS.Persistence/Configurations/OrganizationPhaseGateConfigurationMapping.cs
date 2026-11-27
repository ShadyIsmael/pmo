using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class OrganizationPhaseGateConfigurationMapping
    : IEntityTypeConfiguration<OrganizationPhaseGateConfiguration>
{
    public void Configure(EntityTypeBuilder<OrganizationPhaseGateConfiguration> builder)
    {
        builder.ToTable("OrganizationPhaseGateConfigurations", table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationPhaseGateConfigurations_DistinctPhases",
                "[SourcePhase] <> [TargetPhase]");
            table.HasCheckConstraint(
                "CK_OrganizationPhaseGateConfigurations_DistinctPhaseIds",
                "[SourcePhaseId] <> [TargetPhaseId]");
            table.HasCheckConstraint(
                "CK_OrganizationPhaseGateConfigurations_CurrentVersionPositive",
                "[CurrentVersionNumber] >= 1");
        });

        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.Id).ValueGeneratedNever();
        builder.Property(configuration => configuration.TenantId).IsRequired();
        builder.Property(configuration => configuration.OrganizationId).IsRequired();
        builder.Property(configuration => configuration.SourcePhaseId).IsRequired();
        builder.Property(configuration => configuration.TargetPhaseId).IsRequired();
        builder.Property(configuration => configuration.SourcePhase).HasMaxLength(64).IsRequired();
        builder.Property(configuration => configuration.TargetPhase).HasMaxLength(64).IsRequired();
        builder.Property(configuration => configuration.CurrentVersionNumber).IsRequired();
        builder.Property(configuration => configuration.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(configuration => configuration.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(configuration => configuration.UpdatedAt).HasColumnType("datetime2");
        builder.Property(configuration => configuration.UpdatedBy).HasMaxLength(100);
        builder.Property(configuration => configuration.IsDeleted).HasDefaultValue(false);
        builder.Property(configuration => configuration.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasAlternateKey(configuration => new { configuration.TenantId, configuration.Id })
            .HasName("AK_OrganizationPhaseGateConfigurations_Tenant_Id");
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(configuration => new { configuration.TenantId, configuration.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(configuration => new { configuration.TenantId, configuration.SourcePhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPhaseCatalogEntry>().WithMany()
            .HasForeignKey(configuration => new { configuration.TenantId, configuration.TargetPhaseId })
            .HasPrincipalKey(phase => new { phase.TenantId, phase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(configuration => configuration.Versions)
            .WithOne(version => version.Configuration)
            .HasForeignKey(version => new { version.TenantId, version.ConfigurationId })
            .HasPrincipalKey(configuration => new { configuration.TenantId, configuration.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(configuration => new
            {
                configuration.TenantId,
                configuration.OrganizationId,
                configuration.SourcePhaseId,
                configuration.TargetPhaseId
            })
            .IsUnique()
            .HasDatabaseName("UX_OrganizationPhaseGateConfigurations_ActiveTransition")
            .HasFilter("[IsDeleted] = 0");
    }
}

public sealed class OrganizationPhaseGateConfigurationVersionMapping
    : IEntityTypeConfiguration<OrganizationPhaseGateConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<OrganizationPhaseGateConfigurationVersion> builder)
    {
        builder.ToTable("OrganizationPhaseGateConfigurationVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationPhaseGateConfigurationVersions_VersionPositive",
                "[VersionNumber] >= 1");
        });

        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.TenantId).IsRequired();
        builder.Property(version => version.ConfigurationId).IsRequired();
        builder.Property(version => version.VersionNumber).IsRequired();
        builder.Property(version => version.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(version => version.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(version => version.UpdatedAt).HasColumnType("datetime2");
        builder.Property(version => version.UpdatedBy).HasMaxLength(100);
        builder.Property(version => version.IsDeleted).HasDefaultValue(false);
        builder.Property(version => version.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasAlternateKey(version => new { version.TenantId, version.Id })
            .HasName("AK_OrganizationPhaseGateConfigurationVersions_Tenant_Id");
        builder.HasAlternateKey(version => new { version.TenantId, version.ConfigurationId, version.Id })
            .HasName("AK_OrganizationPhaseGateConfigurationVersions_Tenant_Configuration_Id");
        builder.HasMany(version => version.Requirements)
            .WithOne(requirement => requirement.ConfigurationVersion)
            .HasForeignKey(requirement => new { requirement.TenantId, requirement.ConfigurationVersionId })
            .HasPrincipalKey(version => new { version.TenantId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(version => new { version.TenantId, version.ConfigurationId, version.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_OrganizationPhaseGateConfigurationVersions_Configuration_Version");
    }
}

public sealed class OrganizationPhaseGateRequirementMapping
    : IEntityTypeConfiguration<OrganizationPhaseGateRequirement>
{
    public void Configure(EntityTypeBuilder<OrganizationPhaseGateRequirement> builder)
    {
        builder.ToTable("OrganizationPhaseGateRequirements", table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationPhaseGateRequirements_RequirementType",
                "[RequirementType] IN (N'Mandatory', N'Optional')");
        });

        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).ValueGeneratedNever();
        builder.Property(requirement => requirement.TenantId).IsRequired();
        builder.Property(requirement => requirement.ConfigurationVersionId).IsRequired();
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
            .HasName("AK_OrganizationPhaseGateRequirements_Tenant_Id");

        // The composite key guarantees that a pinned version belongs to the same
        // template and tenant recorded by this immutable requirement snapshot.
        builder.HasOne<DocumentVersion>()
            .WithMany()
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
                requirement.ConfigurationVersionId,
                requirement.TemplateId
            })
            .IsUnique()
            .HasDatabaseName("UX_OrganizationPhaseGateRequirements_Version_Template")
            .HasFilter("[IsDeleted] = 0");
    }
}
