using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectStatusReportConfiguration : IEntityTypeConfiguration<ProjectStatusReport>
{
    public void Configure(EntityTypeBuilder<ProjectStatusReport> builder)
    {
        builder.ToTable("ProjectStatusReports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReportState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.OverallHealth).HasMaxLength(20);
        builder.Property(x => x.ProjectLifecycleStatusAtReport).HasMaxLength(50);
        builder.Property(x => x.ScheduleHealth).HasMaxLength(20);
        builder.Property(x => x.CostHealth).HasMaxLength(20);
        builder.Property(x => x.ScopeHealth).HasMaxLength(20);
        builder.Property(x => x.QualityHealth).HasMaxLength(20);
        builder.Property(x => x.ResourceHealth).HasMaxLength(20);
        builder.Property(x => x.ProjectNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.ProjectCodeSnapshot).HasMaxLength(50);
        builder.Property(x => x.ProjectManagerNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.PreparedByNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.PublishedByNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.CurrencyCodeSnapshot).HasMaxLength(10);
        builder.Property(x => x.BaselineNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.Status).HasMaxLength(50);

        builder.Property(x => x.ExecutiveSummary).HasMaxLength(4000);
        builder.Property(x => x.KeyAchievements).HasMaxLength(4000);
        builder.Property(x => x.ChallengesConcerns).HasMaxLength(4000);
        builder.Property(x => x.NextPeriodPriorities).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.StatusReports)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ReportDate });
        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ReportState });
    }
}

public class ProjectStatusReportRiskSnapshotConfiguration : IEntityTypeConfiguration<ProjectStatusReportRiskSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectStatusReportRiskSnapshot> builder)
    {
        builder.ToTable("ProjectStatusReportRiskSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RiskStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.OwnerNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.RiskResponse).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ProjectStatusReport)
            .WithMany(x => x.RiskSnapshots)
            .HasForeignKey(x => x.ProjectStatusReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.Rank });
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.SourceRiskId }).IsUnique();
    }
}

public class ProjectStatusReportIssueSnapshotConfiguration : IEntityTypeConfiguration<ProjectStatusReportIssueSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectStatusReportIssueSnapshot> builder)
    {
        builder.ToTable("ProjectStatusReportIssueSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IssueStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AssigneeNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Resolution).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ProjectStatusReport)
            .WithMany(x => x.IssueSnapshots)
            .HasForeignKey(x => x.ProjectStatusReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.Rank });
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.SourceIssueId }).IsUnique();
    }
}

public class ProjectStatusReportChangeRequestSnapshotConfiguration : IEntityTypeConfiguration<ProjectStatusReportChangeRequestSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectStatusReportChangeRequestSnapshot> builder)
    {
        builder.ToTable("ProjectStatusReportChangeRequestSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ChangeRequestStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RequestedByNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.Justification).HasMaxLength(4000);
        builder.Property(x => x.Impact).HasMaxLength(4000);
        builder.Property(x => x.DecisionNotes).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ProjectStatusReport)
            .WithMany(x => x.ChangeRequestSnapshots)
            .HasForeignKey(x => x.ProjectStatusReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.Rank });
        builder.HasIndex(x => new { x.TenantId, x.ProjectStatusReportId, x.SourceChangeRequestId }).IsUnique();
    }
}
