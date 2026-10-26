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
    }
}
