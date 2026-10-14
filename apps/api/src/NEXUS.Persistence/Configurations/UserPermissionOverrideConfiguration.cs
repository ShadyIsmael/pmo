using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("UserPermissionOverrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Effect).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.PermissionId }).IsUnique();
        builder.HasOne(item => item.User)
            .WithMany(user => user.PermissionOverrides)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Permission)
            .WithMany(permission => permission.UserOverrides)
            .HasForeignKey(item => item.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
