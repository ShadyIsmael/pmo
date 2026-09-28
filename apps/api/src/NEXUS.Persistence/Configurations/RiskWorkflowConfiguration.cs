using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class RiskActionConfiguration : IEntityTypeConfiguration<RiskAction>
{
    public void Configure(EntityTypeBuilder<RiskAction> builder)
    {
        builder.ToTable("RiskActions");
        builder.HasKey(action => action.Id);
        builder.Property(action => action.Title).HasMaxLength(300).IsRequired();
        builder.Property(action => action.Description).HasMaxLength(4000).IsRequired();
        builder.Property(action => action.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(action => action.RowVersion).IsRowVersion();
        builder.HasIndex(action => new { action.TenantId, action.RiskId, action.SortOrder });
        builder.HasOne(action => action.Risk).WithMany(risk => risk.Actions).HasForeignKey(action => action.RiskId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RiskActionUpdateConfiguration : IEntityTypeConfiguration<RiskActionUpdate>
{
    public void Configure(EntityTypeBuilder<RiskActionUpdate> builder)
    {
        builder.ToTable("RiskActionUpdates");
        builder.HasKey(update => update.Id);
        builder.Property(update => update.UpdateText).HasMaxLength(8000).IsRequired();
        builder.Property(update => update.EntryType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(update => update.ChangeDetailsJson).HasMaxLength(32000);
        builder.Property(update => update.RowVersion).IsRowVersion();
        builder.HasIndex(update => new { update.TenantId, update.RiskActionId, update.CreatedAt });
        builder.HasOne(update => update.RiskAction).WithMany(action => action.Updates).HasForeignKey(update => update.RiskActionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RiskImpactAssessmentConfiguration : IEntityTypeConfiguration<RiskImpactAssessment>
{
    public void Configure(EntityTypeBuilder<RiskImpactAssessment> builder)
    {
        builder.ToTable("RiskImpactAssessments");
        builder.HasKey(impact => impact.Id);
        builder.Property(impact => impact.Description).HasMaxLength(4000).IsRequired();
        builder.Property(impact => impact.RowVersion).IsRowVersion();
        builder.HasIndex(impact => new { impact.TenantId, impact.RiskId }).IsUnique();
        builder.HasOne(impact => impact.Risk).WithMany(risk => risk.ImpactAssessments).HasForeignKey(impact => impact.RiskId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RiskImpactAssessmentTypeConfiguration : IEntityTypeConfiguration<RiskImpactAssessmentType>
{
    public void Configure(EntityTypeBuilder<RiskImpactAssessmentType> builder)
    {
        builder.ToTable("RiskImpactAssessmentTypes");
        builder.HasKey(type => type.Id);
        builder.Property(type => type.ImpactType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(type => type.RowVersion).IsRowVersion();
        builder.HasIndex(type => new { type.TenantId, type.RiskImpactAssessmentId, type.ImpactType }).IsUnique();
        builder.HasOne(type => type.RiskImpactAssessment).WithMany(assessment => assessment.ImpactTypes).HasForeignKey(type => type.RiskImpactAssessmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
