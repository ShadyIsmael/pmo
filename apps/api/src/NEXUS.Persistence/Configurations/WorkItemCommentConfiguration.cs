using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class WorkItemCommentConfiguration : IEntityTypeConfiguration<WorkItemComment>
{
    public void Configure(EntityTypeBuilder<WorkItemComment> builder)
    {
        builder.ToTable("WorkItemComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.CommentText).HasMaxLength(4000).IsRequired();
        builder.Property(comment => comment.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(comment => comment.RowVersion).IsRowVersion();
        builder.HasIndex(comment => new { comment.WorkItemId, comment.CreatedAt });
        builder.HasIndex(comment => comment.ParentCommentId);
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(comment => comment.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItemComment>()
            .WithMany()
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
