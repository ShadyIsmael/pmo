using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        // Existing persisted notifications remain visible in the Notification Center.
        builder.Property(item => item.IsInAppVisible).HasDefaultValue(true);
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Channel).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(item => item.FailureCategory).HasMaxLength(64);
        builder.Property(item => item.LastError).HasMaxLength(300);
        builder.HasIndex(item => new { item.TenantId, item.NotificationId, item.Channel }).IsUnique();
        builder.HasIndex(item => new { item.Status, item.NextAttemptAt });
        builder.HasIndex(item => new { item.Status, item.ProcessingStartedAt });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Notification>()
            .WithMany()
            .HasForeignKey(item => item.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.ToTable("NotificationDeliveryAttempts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(item => item.ProviderMessageId).HasMaxLength(256);
        builder.Property(item => item.FailureCategory).HasMaxLength(64);
        builder.Property(item => item.LastError).HasMaxLength(300);
        builder.HasIndex(item => new { item.TenantId, item.NotificationDeliveryId, item.DeviceTokenId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DeviceTokenId, item.Status });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<NotificationDelivery>()
            .WithMany()
            .HasForeignKey(item => item.NotificationDeliveryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserDeviceToken>()
            .WithMany()
            .HasForeignKey(item => item.DeviceTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.Property(item => item.Type).HasMaxLength(64).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.Type })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
