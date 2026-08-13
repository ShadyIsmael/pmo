using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceRequestConfiguration : IEntityTypeConfiguration<ResourceRequest>
{
    public void Configure(EntityTypeBuilder<ResourceRequest> builder)
    {
        builder.ToTable("ResourceRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ProjectName).HasMaxLength(300);
        builder.Property(r => r.Role).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Priority).HasMaxLength(50);
        builder.Property(r => r.Status).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Justification).HasMaxLength(2000);
        builder.HasIndex(r => r.TenantId);
    }
}
