using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.ToTable("IntegrationConnections");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BaseUrl).HasMaxLength(2048);
        builder.Property(entity => entity.AuthType).HasMaxLength(100);
        builder.Property(entity => entity.Status).HasMaxLength(100);
        // At-rest secret columns may contain ASP.NET Data Protection ciphertext with the
        // IntegrationSecretProtector prefix. They must never be exposed by API projections.
        builder.Property(entity => entity.ApiKey).HasMaxLength(4096);
        builder.Property(entity => entity.ApiSecret).HasMaxLength(4096);
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).HasFilter("[IsDeleted] = 0");
    }
}

public sealed class IntegrationWebhookConfiguration : IEntityTypeConfiguration<IntegrationWebhook>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhook> builder)
    {
        builder.ToTable("IntegrationWebhooks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Url).HasMaxLength(2048);
        builder.Property(entity => entity.Events).HasMaxLength(2048);
        // Stores protected webhook signing secret ciphertext when configured.
        builder.Property(entity => entity.Secret).HasMaxLength(4096);
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).HasFilter("[IsDeleted] = 0");
    }
}
