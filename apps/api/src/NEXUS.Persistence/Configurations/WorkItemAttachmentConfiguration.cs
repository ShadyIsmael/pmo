using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class WorkItemAttachmentConfiguration : IEntityTypeConfiguration<WorkItemAttachment>
{
    public void Configure(EntityTypeBuilder<WorkItemAttachment> builder)
    {
        builder.ToTable("WorkItemAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.StoragePath).HasMaxLength(2000).IsRequired();
        builder.Property(attachment => attachment.FileSize).IsRequired();
        builder.Property(attachment => attachment.RowVersion).IsRowVersion();
        builder.HasIndex(attachment => new { attachment.WorkItemId, attachment.CreatedAt });
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(attachment => attachment.WorkItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
