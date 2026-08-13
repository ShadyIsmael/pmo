using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Category).HasMaxLength(100);
        builder.HasIndex(r => r.Name).IsUnique();
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
