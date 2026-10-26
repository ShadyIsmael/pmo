using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectChangeRequestConfiguration : IEntityTypeConfiguration<ProjectChangeRequest>
{
    public void Configure(EntityTypeBuilder<ProjectChangeRequest> builder)
    {
        builder.ToTable("ProjectChangeRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Justification)
            .HasMaxLength(2000);

        builder.Property(x => x.Impact);
        builder.Property(x => x.BudgetBeforeChange).HasPrecision(18, 2);
        builder.Property(x => x.BudgetChangeAmount).HasPrecision(18, 2);

        builder.Property(x => x.Attachments)
            .HasMaxLength(4000);

        builder.Property(x => x.ApprovalStrategy).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SelectedApproverUserIdsJson).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DecisionNotes);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.WorkflowInstanceId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [WorkflowInstanceId] IS NOT NULL");
        builder.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId).OnDelete(DeleteBehavior.NoAction);
    }
}
