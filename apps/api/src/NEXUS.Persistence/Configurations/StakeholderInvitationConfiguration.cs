using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class StakeholderInvitationConfiguration : IEntityTypeConfiguration<StakeholderInvitation>
{
    public void Configure(EntityTypeBuilder<StakeholderInvitation> builder)
    {
        builder.ToTable("StakeholderInvitations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Email).HasMaxLength(256).IsRequired();
        builder.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.RowVersion).IsConcurrencyToken().ValueGeneratedNever();

        builder.HasOne(item => item.Project)
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ProjectStakeholder)
            .WithMany()
            .HasForeignKey(item => item.ProjectStakeholderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.TokenHash).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ResourceId, item.ProjectId, item.Status });
    }
}
