using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence.Configurations;

public sealed class EntityCodeSequenceConfiguration : IEntityTypeConfiguration<EntityCodeSequence>
{
    public void Configure(EntityTypeBuilder<EntityCodeSequence> builder)
    {
        builder.ToTable("EntityCodeSequences");
        builder.HasKey(sequence => sequence.Id);
        builder.Property(sequence => sequence.EntityType)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(sequence => sequence.NextValue)
            .IsRequired();
        builder.HasIndex(sequence => new { sequence.TenantId, sequence.EntityType })
            .IsUnique();
    }
}
