using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationManagementAndTenantScopedEntityCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration must also work with databases that already received the
            // organization/code tables from the earlier migration chain.  The source
            // migration was later consolidated, so the table creation operations are
            // intentionally idempotent and never replace existing tenant data.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkflowDefinitions') AND name = N'IX_WorkflowDefinitions_TenantId_EntityType_IsActive')
                    DROP INDEX [IX_WorkflowDefinitions_TenantId_EntityType_IsActive] ON [dbo].[WorkflowDefinitions];

                IF COL_LENGTH(N'dbo.WorkflowDefinitions', N'Domain') IS NULL
                    ALTER TABLE [dbo].[WorkflowDefinitions]
                    ADD [Domain] nvarchar(30) NOT NULL CONSTRAINT [DF_WorkflowDefinitions_Domain] DEFAULT N'Task' WITH VALUES;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.EntityCodeSequences', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[EntityCodeSequences]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [EntityType] nvarchar(100) NOT NULL,
                        [NextValue] int NOT NULL,
                        CONSTRAINT [PK_EntityCodeSequences] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'dbo.OrganizationalUnits', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OrganizationalUnits]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [Code] nvarchar(50) NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [TypeCode] nvarchar(100) NOT NULL,
                        [Description] nvarchar(2000) NULL,
                        [ParentOrganizationalUnitId] uniqueidentifier NULL,
                        [IsActive] bit NOT NULL,
                        [SortOrder] int NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [IsDeleted] bit NOT NULL,
                        [RowVersion] rowversion NOT NULL,
                        CONSTRAINT [PK_OrganizationalUnits] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_OrganizationalUnits_OrganizationalUnits_ParentOrganizationalUnitId]
                            FOREIGN KEY ([ParentOrganizationalUnitId]) REFERENCES [dbo].[OrganizationalUnits] ([Id])
                    );
                END;

                IF OBJECT_ID(N'dbo.ResourceReportingLines', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ResourceReportingLines]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [ResourceId] uniqueidentifier NOT NULL,
                        [ManagerResourceId] uniqueidentifier NOT NULL,
                        [ReportingType] nvarchar(50) NOT NULL,
                        [IsPrimary] bit NOT NULL,
                        [EffectiveFrom] datetime2 NOT NULL,
                        [EffectiveTo] datetime2 NULL,
                        [IsActive] bit NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [IsDeleted] bit NOT NULL,
                        [RowVersion] rowversion NOT NULL,
                        CONSTRAINT [PK_ResourceReportingLines] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'dbo.ResourceOrganizationalUnits', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ResourceOrganizationalUnits]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [ResourceId] uniqueidentifier NOT NULL,
                        [OrganizationalUnitId] uniqueidentifier NOT NULL,
                        [IsPrimary] bit NOT NULL,
                        [EffectiveFrom] datetime2 NOT NULL,
                        [EffectiveTo] datetime2 NULL,
                        [IsActive] bit NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [IsDeleted] bit NOT NULL,
                        [RowVersion] rowversion NOT NULL,
                        CONSTRAINT [PK_ResourceOrganizationalUnits] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ResourceOrganizationalUnits_OrganizationalUnits_OrganizationalUnitId]
                            FOREIGN KEY ([OrganizationalUnitId]) REFERENCES [dbo].[OrganizationalUnits] ([Id])
                    );
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ProjectWorkflowConfigurations', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ProjectWorkflowConfigurations]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [ProjectId] uniqueidentifier NOT NULL,
                        [Domain] nvarchar(30) NOT NULL,
                        [Mode] nvarchar(20) NOT NULL,
                        [WorkflowDefinitionId] uniqueidentifier NULL,
                        [EffectiveWorkflowVersionId] uniqueidentifier NULL,
                        [ConfiguredBy] nvarchar(200) NOT NULL,
                        [ConfiguredAt] datetime2 NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [IsDeleted] bit NOT NULL,
                        [RowVersion] rowversion NOT NULL,
                        CONSTRAINT [PK_ProjectWorkflowConfigurations] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ProjectWorkflowConfigurations_WorkflowDefinitionVersions_EffectiveWorkflowVersionId]
                            FOREIGN KEY ([EffectiveWorkflowVersionId]) REFERENCES [dbo].[WorkflowDefinitionVersions] ([Id]),
                        CONSTRAINT [FK_ProjectWorkflowConfigurations_WorkflowDefinitions_WorkflowDefinitionId]
                            FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [dbo].[WorkflowDefinitions] ([Id])
                    );
                END;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkflowDefinitions') AND name = N'IX_WorkflowDefinitions_TenantId_Domain_IsActive')
                    CREATE INDEX [IX_WorkflowDefinitions_TenantId_Domain_IsActive]
                    ON [dbo].[WorkflowDefinitions] ([TenantId], [Domain], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EntityCodeSequences') AND name = N'IX_EntityCodeSequences_TenantId_EntityType')
                    CREATE UNIQUE INDEX [IX_EntityCodeSequences_TenantId_EntityType]
                    ON [dbo].[EntityCodeSequences] ([TenantId], [EntityType]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrganizationalUnits') AND name = N'IX_OrganizationalUnits_ParentOrganizationalUnitId')
                    CREATE INDEX [IX_OrganizationalUnits_ParentOrganizationalUnitId]
                    ON [dbo].[OrganizationalUnits] ([ParentOrganizationalUnitId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrganizationalUnits') AND name = N'IX_OrganizationalUnits_TenantId_Code')
                    CREATE UNIQUE INDEX [IX_OrganizationalUnits_TenantId_Code]
                    ON [dbo].[OrganizationalUnits] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrganizationalUnits') AND name = N'IX_OrganizationalUnits_TenantId_ParentOrganizationalUnitId_IsActive')
                    CREATE INDEX [IX_OrganizationalUnits_TenantId_ParentOrganizationalUnitId_IsActive]
                    ON [dbo].[OrganizationalUnits] ([TenantId], [ParentOrganizationalUnitId], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrganizationalUnits') AND name = N'IX_OrganizationalUnits_TenantId_TypeCode_IsActive')
                    CREATE INDEX [IX_OrganizationalUnits_TenantId_TypeCode_IsActive]
                    ON [dbo].[OrganizationalUnits] ([TenantId], [TypeCode], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProjectWorkflowConfigurations') AND name = N'IX_ProjectWorkflowConfigurations_EffectiveWorkflowVersionId')
                    CREATE INDEX [IX_ProjectWorkflowConfigurations_EffectiveWorkflowVersionId]
                    ON [dbo].[ProjectWorkflowConfigurations] ([EffectiveWorkflowVersionId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProjectWorkflowConfigurations') AND name = N'IX_ProjectWorkflowConfigurations_TenantId_ProjectId_Domain')
                    CREATE UNIQUE INDEX [IX_ProjectWorkflowConfigurations_TenantId_ProjectId_Domain]
                    ON [dbo].[ProjectWorkflowConfigurations] ([TenantId], [ProjectId], [Domain]) WHERE [IsDeleted] = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProjectWorkflowConfigurations') AND name = N'IX_ProjectWorkflowConfigurations_TenantId_WorkflowDefinitionId')
                    CREATE INDEX [IX_ProjectWorkflowConfigurations_TenantId_WorkflowDefinitionId]
                    ON [dbo].[ProjectWorkflowConfigurations] ([TenantId], [WorkflowDefinitionId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProjectWorkflowConfigurations') AND name = N'IX_ProjectWorkflowConfigurations_WorkflowDefinitionId')
                    CREATE INDEX [IX_ProjectWorkflowConfigurations_WorkflowDefinitionId]
                    ON [dbo].[ProjectWorkflowConfigurations] ([WorkflowDefinitionId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceOrganizationalUnits') AND name = N'IX_ResourceOrganizationalUnits_OrganizationalUnitId')
                    CREATE INDEX [IX_ResourceOrganizationalUnits_OrganizationalUnitId]
                    ON [dbo].[ResourceOrganizationalUnits] ([OrganizationalUnitId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceOrganizationalUnits') AND name = N'IX_ResourceOrganizationalUnits_TenantId_OrganizationalUnitId_IsActive')
                    CREATE INDEX [IX_ResourceOrganizationalUnits_TenantId_OrganizationalUnitId_IsActive]
                    ON [dbo].[ResourceOrganizationalUnits] ([TenantId], [OrganizationalUnitId], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceOrganizationalUnits') AND name = N'IX_ResourceOrganizationalUnits_TenantId_ResourceId_EffectiveFrom')
                    CREATE INDEX [IX_ResourceOrganizationalUnits_TenantId_ResourceId_EffectiveFrom]
                    ON [dbo].[ResourceOrganizationalUnits] ([TenantId], [ResourceId], [EffectiveFrom]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceOrganizationalUnits') AND name = N'IX_ResourceOrganizationalUnits_TenantId_ResourceId_IsPrimary_IsActive')
                    CREATE UNIQUE INDEX [IX_ResourceOrganizationalUnits_TenantId_ResourceId_IsPrimary_IsActive]
                    ON [dbo].[ResourceOrganizationalUnits] ([TenantId], [ResourceId], [IsPrimary], [IsActive])
                    WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceReportingLines') AND name = N'IX_ResourceReportingLines_TenantId_ManagerResourceId_IsActive')
                    CREATE INDEX [IX_ResourceReportingLines_TenantId_ManagerResourceId_IsActive]
                    ON [dbo].[ResourceReportingLines] ([TenantId], [ManagerResourceId], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceReportingLines') AND name = N'IX_ResourceReportingLines_TenantId_ResourceId_EffectiveFrom')
                    CREATE INDEX [IX_ResourceReportingLines_TenantId_ResourceId_EffectiveFrom]
                    ON [dbo].[ResourceReportingLines] ([TenantId], [ResourceId], [EffectiveFrom]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ResourceReportingLines') AND name = N'IX_ResourceReportingLines_TenantId_ResourceId_IsPrimary_IsActive')
                    CREATE UNIQUE INDEX [IX_ResourceReportingLines_TenantId_ResourceId_IsPrimary_IsActive]
                    ON [dbo].[ResourceReportingLines] ([TenantId], [ResourceId], [IsPrimary], [IsActive])
                    WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These organization/code tables may have been created by an earlier
            // migration id in existing databases.  Never remove them (or their
            // tenant data) when rolling back this consolidated compatibility migration.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ProjectWorkflowConfigurations', N'U') IS NOT NULL
                    DROP TABLE [dbo].[ProjectWorkflowConfigurations];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkflowDefinitions') AND name = N'IX_WorkflowDefinitions_TenantId_Domain_IsActive')
                    DROP INDEX [IX_WorkflowDefinitions_TenantId_Domain_IsActive] ON [dbo].[WorkflowDefinitions];

                IF COL_LENGTH(N'dbo.WorkflowDefinitions', N'Domain') IS NOT NULL
                    ALTER TABLE [dbo].[WorkflowDefinitions] DROP COLUMN [Domain];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkflowDefinitions') AND name = N'IX_WorkflowDefinitions_TenantId_EntityType_IsActive')
                    CREATE INDEX [IX_WorkflowDefinitions_TenantId_EntityType_IsActive]
                    ON [dbo].[WorkflowDefinitions] ([TenantId], [EntityType], [IsActive]);
                """);
        }
    }
}
