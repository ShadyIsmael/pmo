using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProgramManagerAssignmentConfiguration : IEntityTypeConfiguration<ProgramManagerAssignment>
{
    public void Configure(EntityTypeBuilder<ProgramManagerAssignment> builder)
    {
        builder.ToTable("ProgramManagerAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HandoverReason).HasMaxLength(500);
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ProgramId);
        builder.HasIndex(x => x.ProgramManagerId);
        builder.HasIndex(x => x.ProgramId).IsUnique().HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0");
        builder.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
