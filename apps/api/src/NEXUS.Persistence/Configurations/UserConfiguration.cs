using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired(false);
        builder.Property(u => u.AuthenticationSource).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(u => u.MustChangePassword).HasDefaultValue(false);
        builder.Property(u => u.TemporaryPasswordExpiresAt);
        builder.Property(u => u.PasswordChangedAt);
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        // Legacy columns remain mapped as shadow properties until a separately
        // approved cleanup migration can verify and preserve any unlinked values.
        builder.Property<string?>("JobTitle").HasMaxLength(200);
        builder.Property<string?>("Phone").HasMaxLength(50);
        builder.Property<string?>("AvatarUrl").HasMaxLength(500);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.TenantId);
        builder.HasAlternateKey(u => new { u.TenantId, u.Id });
        builder.Property(u => u.RowVersion).IsRowVersion();
    }
}
