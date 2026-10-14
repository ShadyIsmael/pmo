using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class DirectoryConnectionConfiguration : IEntityTypeConfiguration<DirectoryConnection>
{
    public void Configure(EntityTypeBuilder<DirectoryConnection> builder)
    {
        builder.ToTable("DirectoryConnections");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(150).IsRequired();
        builder.Property(entity => entity.ProviderType).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.DirectoryTenantId).HasMaxLength(200);
        builder.Property(entity => entity.ClientId).HasMaxLength(100);
        builder.Property(entity => entity.CredentialReference).HasMaxLength(100);
        builder.Property(entity => entity.Host).HasMaxLength(255);
        builder.Property(entity => entity.BaseDn).HasMaxLength(1024);
        builder.Property(entity => entity.BindUser).HasMaxLength(256);
        builder.Property(entity => entity.BindCredentialReference).HasMaxLength(100);
        builder.Property(entity => entity.Domain).HasMaxLength(255);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.IsDefault }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsDefault] = 1");
    }
}

public sealed class DirectorySyncScopeConfiguration : IEntityTypeConfiguration<DirectorySyncScope>
{
    public void Configure(EntityTypeBuilder<DirectorySyncScope> builder)
    {
        builder.ToTable("DirectorySyncScopes");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ScopeType).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SelectedGroupIdsJson).IsRequired();
        builder.Property(entity => entity.SelectedUserIdsJson).IsRequired();
        builder.Property(entity => entity.OrganizationalUnitsJson).IsRequired();
        builder.Property(entity => entity.UserFilter).HasMaxLength(1024);
        builder.Property(entity => entity.GroupFilter).HasMaxLength(1024);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectoryFieldMappingConfiguration : IEntityTypeConfiguration<DirectoryFieldMapping>
{
    public void Configure(EntityTypeBuilder<DirectoryFieldMapping> builder)
    {
        builder.ToTable("DirectoryFieldMappings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TargetField).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.SourceAttribute).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.UpdatePolicy).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Transform).HasMaxLength(30);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.TargetField }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectoryGroupConfiguration : IEntityTypeConfiguration<DirectoryGroup>
{
    public void Configure(EntityTypeBuilder<DirectoryGroup> builder)
    {
        builder.ToTable("DirectoryGroups");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ExternalDirectoryId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1024);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.ExternalDirectoryId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectoryGroupMappingConfiguration : IEntityTypeConfiguration<DirectoryGroupMapping>
{
    public void Configure(EntityTypeBuilder<DirectoryGroupMapping> builder)
    {
        builder.ToTable("DirectoryGroupMappings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ExternalGroupId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ExternalGroupName).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.ExternalGroupId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.RoleId });
    }
}

public sealed class DirectoryOrganizationMappingConfiguration : IEntityTypeConfiguration<DirectoryOrganizationMapping>
{
    public void Configure(EntityTypeBuilder<DirectoryOrganizationMapping> builder)
    {
        builder.ToTable("DirectoryOrganizationMappings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DirectoryValue).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.DirectoryValue }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectoryProvisioningPolicyConfiguration : IEntityTypeConfiguration<DirectoryProvisioningPolicy>
{
    public void Configure(EntityTypeBuilder<DirectoryProvisioningPolicy> builder)
    {
        builder.ToTable("DirectoryProvisioningPolicies");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MissingUserAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.MissingGroupAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.DisabledUserAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.OutOfScopeUserAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.ChangedGroupAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.MissingGroupReferenceAction).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.DefaultEmploymentType).HasMaxLength(100);
        builder.Property(entity => entity.DefaultSeniority).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectoryUserLinkConfiguration : IEntityTypeConfiguration<DirectoryUserLink>
{
    public void Configure(EntityTypeBuilder<DirectoryUserLink> builder)
    {
        builder.ToTable("DirectoryUserLinks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ExternalDirectoryId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.UserPrincipalName).HasMaxLength(256);
        builder.Property(entity => entity.Username).HasMaxLength(256);
        builder.Property(entity => entity.DisplayName).HasMaxLength(256);
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.Department).HasMaxLength(256);
        builder.Property(entity => entity.JobTitle).HasMaxLength(200);
        builder.Property(entity => entity.EmployeeId).HasMaxLength(100);
        builder.Property(entity => entity.ManagerExternalId).HasMaxLength(256);
        builder.Property(entity => entity.DistinguishedName).HasMaxLength(1024);
        builder.Property(entity => entity.ManagedOrganizationalMembershipId);
        builder.Property(entity => entity.ManagedReportingLineId);
        builder.Property(entity => entity.LastImportedValuesJson);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.ExternalDirectoryId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.UserId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [UserId] IS NOT NULL");
        builder.HasIndex(entity => new { entity.TenantId, entity.UserPrincipalName });
    }
}

public sealed class DirectoryRoleGrantConfiguration : IEntityTypeConfiguration<DirectoryRoleGrant>
{
    public void Configure(EntityTypeBuilder<DirectoryRoleGrant> builder)
    {
        builder.ToTable("DirectoryRoleGrants");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ExternalGroupId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.ExternalGroupId, entity.UserId, entity.RoleId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DirectorySyncRunConfiguration : IEntityTypeConfiguration<DirectorySyncRun>
{
    public void Configure(EntityTypeBuilder<DirectorySyncRun> builder)
    {
        builder.ToTable("DirectorySyncRuns");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Mode).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Scope).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectoryConnectionId, entity.StartedAt });
    }
}

public sealed class DirectorySyncItemConfiguration : IEntityTypeConfiguration<DirectorySyncItem>
{
    public void Configure(EntityTypeBuilder<DirectorySyncItem> builder)
    {
        builder.ToTable("DirectorySyncItems");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ItemType).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.ExternalDirectoryId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Classification).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ProposedAction).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(256);
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ErrorCode).HasMaxLength(64);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.DirectorySyncRunId });
        builder.HasOne<DirectorySyncRun>().WithMany().HasForeignKey(entity => entity.DirectorySyncRunId).OnDelete(DeleteBehavior.Cascade);
    }
}
