using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PlanImportSessionConfiguration : IEntityTypeConfiguration<PlanImportSession>
{
    public void Configure(EntityTypeBuilder<PlanImportSession> builder)
    {
        builder.ToTable("PlanImportSessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.FileName).HasMaxLength(260).IsRequired();
        builder.Property(item => item.Format).HasMaxLength(50).IsRequired();
        builder.Property(item => item.SuggestedPlanName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.ParsedDocumentJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.ResolutionJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.CommitResultJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        builder.HasMany(item => item.Issues)
            .WithOne(issue => issue.PlanImportSession)
            .HasForeignKey(issue => issue.PlanImportSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlanImportIssueConfiguration : IEntityTypeConfiguration<PlanImportIssue>
{
    public void Configure(EntityTypeBuilder<PlanImportIssue> builder)
    {
        builder.ToTable("PlanImportIssues");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Severity).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Category).HasMaxLength(80).IsRequired();
        builder.Property(item => item.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SourceId).HasMaxLength(200);
        builder.Property(item => item.Code).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.PlanImportSessionId, item.IsBlocking });
    }
}
