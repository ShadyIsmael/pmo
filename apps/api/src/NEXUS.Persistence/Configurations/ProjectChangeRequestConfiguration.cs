using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

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

        builder.Property(x => x.Attachments)
            .HasMaxLength(4000);

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
    }
}