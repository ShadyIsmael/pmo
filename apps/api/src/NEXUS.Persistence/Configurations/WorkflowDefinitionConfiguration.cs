using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Domain).HasMaxLength(30).IsRequired().HasDefaultValue(WorkflowDomains.Task);
        builder.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.Domain, item.IsActive });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.ActivePublishedVersion)
            .WithMany()
            .HasForeignKey(item => item.ActivePublishedVersionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowDefinitionVersionConfiguration : IEntityTypeConfiguration<WorkflowDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionVersion> builder)
    {
        builder.ToTable("WorkflowDefinitionVersions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ChangeLog).HasMaxLength(4000);
        builder.Property(item => item.PublishedBy).HasMaxLength(200);
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId, item.VersionNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId, item.LifecycleStatus });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowDefinition)
            .WithMany(item => item.Versions)
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
{
    public void Configure(EntityTypeBuilder<WorkflowState> builder)
    {
        builder.ToTable("WorkflowStates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(100).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionVersionId, item.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasAlternateKey(item => new { item.WorkflowDefinitionVersionId, item.Id });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowDefinitionVersion)
            .WithMany(item => item.States)
            .HasForeignKey(item => item.WorkflowDefinitionVersionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(100).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionVersionId, item.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionVersionId, item.FromStateId, item.ToStateId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowDefinitionVersion)
            .WithMany(item => item.Transitions)
            .HasForeignKey(item => item.WorkflowDefinitionVersionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.FromState)
            .WithMany(item => item.OutgoingTransitions)
            .HasForeignKey(item => new { item.WorkflowDefinitionVersionId, item.FromStateId })
            .HasPrincipalKey(item => new { item.WorkflowDefinitionVersionId, item.Id })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.ToState)
            .WithMany(item => item.IncomingTransitions)
            .HasForeignKey(item => new { item.WorkflowDefinitionVersionId, item.ToStateId })
            .HasPrincipalKey(item => new { item.WorkflowDefinitionVersionId, item.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowTransitionGuardConfiguration : IEntityTypeConfiguration<WorkflowTransitionGuard>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionGuard> builder)
    {
        builder.ToTable("WorkflowTransitionGuards");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.GuardType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ConfigurationJson).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowTransitionId, item.ExecutionOrder });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowTransition).WithMany(item => item.Guards).HasForeignKey(item => item.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowTransitionActionConfiguration : IEntityTypeConfiguration<WorkflowTransitionAction>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionAction> builder)
    {
        builder.ToTable("WorkflowTransitionActions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ConfigurationJson).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.WorkflowTransitionId, item.ExecutionOrder });
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowTransition).WithMany(item => item.Actions).HasForeignKey(item => item.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.StartedBy).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.EntityType, item.EntityId, item.WorkflowDefinitionId, item.IsActive }).HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.EntityType, item.EntityId, item.WorkflowDefinitionId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.WorkflowDefinition)
            .WithMany(item => item.Instances)
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.WorkflowDefinitionVersion)
            .WithMany(item => item.Instances)
            .HasForeignKey(item => item.WorkflowDefinitionVersionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.CurrentState)
            .WithMany()
            .HasForeignKey(item => new { item.WorkflowDefinitionVersionId, item.CurrentStateId })
            .HasPrincipalKey(item => new { item.WorkflowDefinitionVersionId, item.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkflowTransitionHistoryConfiguration : IEntityTypeConfiguration<WorkflowTransitionHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionHistory> builder)
    {
        builder.ToTable("WorkflowTransitionHistory");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ExecutedBy).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ExecutedByNameSnapshot).HasMaxLength(200);
        builder.Property(item => item.Comment).HasMaxLength(4000);
        builder.Property(item => item.MetadataJson);
        builder.Property(item => item.CorrelationId).HasMaxLength(100);
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId, item.ExecutedAt });
        builder.HasOne(item => item.WorkflowInstance).WithMany(item => item.History).HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.WorkflowTransition).WithMany().HasForeignKey(item => item.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.FromState).WithMany().HasForeignKey(item => item.FromStateId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.ToState).WithMany().HasForeignKey(item => item.ToStateId).OnDelete(DeleteBehavior.NoAction);
    }
}
