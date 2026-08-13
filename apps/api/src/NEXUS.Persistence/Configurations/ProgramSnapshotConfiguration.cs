using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProgramSnapshotConfiguration : IEntityTypeConfiguration<ProgramSnapshot>
{
    public void Configure(EntityTypeBuilder<ProgramSnapshot> builder)
    {
        builder.ToTable("ProgramSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HandoverReason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.Property(x => x.Data);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProgramId, x.SnapshotNumber }).IsUnique();
        builder.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
