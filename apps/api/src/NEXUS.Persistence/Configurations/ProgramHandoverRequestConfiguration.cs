using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProgramHandoverRequestConfiguration : IEntityTypeConfiguration<ProgramHandoverRequest>
{
    public void Configure(EntityTypeBuilder<ProgramHandoverRequest> builder)
    {
        builder.ToTable("ProgramHandoverRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.InitiationType).HasMaxLength(40).IsRequired().HasDefaultValue("ManagerInitiated");
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProgramId, x.Status }).IsUnique().HasFilter("[Status] = 'Pending' AND [IsDeleted] = 0");
        builder.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
