using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => m.TeamId);
        builder.HasIndex(m => m.ResourceId);

        // Serves the "which Teams is this Resource currently in" lookup used by
        // My Work and by the Resource form.
        builder.HasIndex(m => new { m.TenantId, m.ResourceId, m.IsActive });

        // At most one current membership row per (Team, Resource) pair. The filter
        // leaves closed historical rows alone so history is never rewritten.
        builder.HasIndex(m => new { m.TenantId, m.TeamId, m.ResourceId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [EffectiveTo] IS NULL");

        builder.Ignore(m => m.IsCurrent);    }
}
