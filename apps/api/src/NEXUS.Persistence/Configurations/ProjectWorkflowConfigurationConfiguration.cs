using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectWorkflowConfigurationConfiguration : IEntityTypeConfiguration<ProjectWorkflowConfiguration>
{
    public void Configure(EntityTypeBuilder<ProjectWorkflowConfiguration> builder)
    {
        builder.ToTable("ProjectWorkflowConfigurations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Domain).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Mode).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ConfiguredBy).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Domain }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowDefinition).WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.EffectiveWorkflowVersion).WithMany().HasForeignKey(item => item.EffectiveWorkflowVersionId).OnDelete(DeleteBehavior.NoAction);
    }
}
