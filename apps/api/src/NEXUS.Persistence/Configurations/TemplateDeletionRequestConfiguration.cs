using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Documents.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class TemplateDeletionRequestConfiguration : IEntityTypeConfiguration<TemplateDeletionRequest>
{
    public void Configure(EntityTypeBuilder<TemplateDeletionRequest> builder)
    {
        builder.ToTable("TemplateDeletionRequests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.TemplateName).HasMaxLength(256).IsRequired();
        builder.Property(request => request.OwnerScope).HasMaxLength(24).IsRequired();
        builder.Property(request => request.RequesterName).HasMaxLength(256).IsRequired();
        builder.Property(request => request.Reason).HasMaxLength(4000).IsRequired();
        builder.Property(request => request.Status).HasMaxLength(32).IsRequired();
        builder.Property(request => request.TemplateMutationSnapshotHash).HasMaxLength(64).IsRequired();
        builder.Property(request => request.StorageCleanupKeysJson).HasColumnType("nvarchar(max)");
        builder.Property(request => request.RowVersion).IsConcurrencyToken().ValueGeneratedNever();

        // No user/document foreign keys: approved hard deletes and deleted accounts
        // must never cascade away the independent request and its audit history.
        builder.HasIndex(request => new { request.TenantId, request.TemplateId })
            .IsUnique()
            .HasFilter("[Status] = N'PendingApproval'")
            .HasDatabaseName("UX_TemplateDeletionRequests_Tenant_Template_Pending");

        builder.HasIndex(request => new { request.TenantId, request.RequesterId, request.RequestedAt });
        builder.HasIndex(request => new { request.TenantId, request.Status, request.RequestedAt });
    }
}
