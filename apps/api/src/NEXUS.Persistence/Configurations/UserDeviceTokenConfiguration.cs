using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("UserDeviceTokens");
        builder.HasKey(device => device.Id);
        // SQL Server caps nvarchar at 4000 characters, and (TenantId, Token) is a unique
        // index so Token cannot be nvarchar(max) either.
        builder.Property(device => device.Token).HasMaxLength(4000).IsRequired();
        builder.Property(device => device.Platform).HasMaxLength(32).IsRequired();
        builder.Property(device => device.DeviceName).HasMaxLength(200);
        builder.HasIndex(device => new { device.TenantId, device.Token }).IsUnique();
        builder.HasIndex(device => new { device.TenantId, device.UserId, device.IsActive });
        builder.Property(device => device.RowVersion).IsRowVersion();
    }
}
