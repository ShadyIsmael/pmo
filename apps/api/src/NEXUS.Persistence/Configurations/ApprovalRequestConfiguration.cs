using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Approvals.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests");
        builder.HasKey(request => request.Id);
        builder.HasAlternateKey(request => new { request.TenantId, request.Id })
            .HasName("AK_ApprovalRequests_TenantId_Id");
        // Keep the established varbinary column shape so legacy approval rows
        // remain compatible. AppDbContext rotates this concurrency token on writes.
        builder.Property(request => request.RowVersion).IsConcurrencyToken();
        builder.Property(request => request.Amount).HasPrecision(19, 4);

        builder.HasMany(request => request.Actions)
            .WithOne(action => action.ApprovalRequest)
            .HasForeignKey(action => action.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
