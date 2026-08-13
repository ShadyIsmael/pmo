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
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.JobTitle).HasMaxLength(200);
        builder.Property(u => u.Phone).HasMaxLength(50);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.TenantId);
        builder.Property(u => u.RowVersion).IsRowVersion();
    }
}
