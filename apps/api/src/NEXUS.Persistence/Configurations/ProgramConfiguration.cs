using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("Programs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
