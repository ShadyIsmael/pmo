using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationOwnedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Roles_Tenant_Name",
                table: "Roles");

            migrationBuilder.AddColumn<bool>(
                name: "IsLegacyOrganizationOwnershipUnresolved",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: true);

            // Reconcile only the two Product Owner-approved Role IDs using the
            // live, same-Tenant Organization mapping. Keep the five other active
            // operational Roles unresolved; do not infer any additional owners.
            // This SQL runs inside the EF migration transaction, so the approved
            // Roles do not have a temporary unresolved/privilege-loss interval.
            migrationBuilder.Sql(
                """
                DECLARE @OrganizationId uniqueidentifier = '90000001-0000-0000-0000-000000000001';
                DECLARE @OrganizationTenantId uniqueidentifier;

                SELECT @OrganizationTenantId = [TenantId]
                FROM [dbo].[Organizations]
                WHERE [Id] = @OrganizationId
                  AND [Name] = N'NEXUS Group'
                  AND [IsActive] = 1
                  AND [IsDeleted] = 0;

                IF @OrganizationTenantId IS NULL
                BEGIN
                    THROW 51001, 'Approved NEXUS Group Organization is missing, inactive, deleted, or mismatched.', 1;
                END;

                DECLARE @ApprovedRoles TABLE
                (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [Name] nvarchar(100) NOT NULL,
                    [RoleKey] nvarchar(64) NOT NULL
                );
                INSERT INTO @ApprovedRoles ([Id], [Name], [RoleKey])
                VALUES
                    ('A0000003-0000-0000-0000-000000000003', N'portfolio.manager', N'tenant.portfolio-manager'),
                    ('A0000004-0000-0000-0000-000000000004', N'project.manager', N'tenant.project-manager');

                DECLARE @UnresolvedRoles TABLE
                (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [Name] nvarchar(100) NOT NULL,
                    [RoleKey] nvarchar(64) NOT NULL
                );
                INSERT INTO @UnresolvedRoles ([Id], [Name], [RoleKey])
                VALUES
                    ('A0000002-0000-0000-0000-000000000002', N'executive', N'tenant.executive'),
                    ('A0000005-0000-0000-0000-000000000005', N'team.member', N'tenant.team-member'),
                    ('A0000006-0000-0000-0000-000000000006', N'pmo.admin', N'tenant.pmo-admin'),
                    ('A0000007-0000-0000-0000-000000000007', N'finance.controller', N'tenant.finance-controller'),
                    ('A0000008-0000-0000-0000-000000000008', N'resource.manager', N'tenant.resource-manager');

                IF EXISTS
                (
                    SELECT 1
                    FROM @ApprovedRoles expected
                    LEFT JOIN [dbo].[Roles] actual ON actual.[Id] = expected.[Id]
                    WHERE actual.[Id] IS NULL
                       OR actual.[TenantId] <> @OrganizationTenantId
                       OR actual.[Name] IS NULL
                       OR actual.[Name] COLLATE Latin1_General_100_BIN2 <> expected.[Name] COLLATE Latin1_General_100_BIN2
                       OR actual.[RoleKey] IS NULL
                       OR actual.[RoleKey] COLLATE Latin1_General_100_BIN2 <> expected.[RoleKey] COLLATE Latin1_General_100_BIN2
                       OR actual.[IsDeleted] <> 0
                       OR actual.[IsSystem] <> 0
                       OR actual.[SystemRoleKey] IS NOT NULL
                       OR actual.[OrganizationId] IS NOT NULL
                       OR actual.[IsLegacyOrganizationOwnershipUnresolved] <> 0
                )
                BEGIN
                    THROW 51002, 'Approved Role identity or Tenant mapping differs from the reviewed Development baseline.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM @UnresolvedRoles expected
                    LEFT JOIN [dbo].[Roles] actual ON actual.[Id] = expected.[Id]
                    WHERE actual.[Id] IS NULL
                       OR actual.[TenantId] <> @OrganizationTenantId
                       OR actual.[Name] IS NULL
                       OR actual.[Name] COLLATE Latin1_General_100_BIN2 <> expected.[Name] COLLATE Latin1_General_100_BIN2
                       OR actual.[RoleKey] IS NULL
                       OR actual.[RoleKey] COLLATE Latin1_General_100_BIN2 <> expected.[RoleKey] COLLATE Latin1_General_100_BIN2
                       OR actual.[IsDeleted] <> 0
                       OR actual.[IsSystem] <> 0
                       OR actual.[SystemRoleKey] IS NOT NULL
                       OR actual.[OrganizationId] IS NOT NULL
                       OR actual.[IsLegacyOrganizationOwnershipUnresolved] <> 0
                )
                BEGIN
                    THROW 51003, 'The five unresolved legacy Role identities differ from the reviewed Development baseline.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM [dbo].[UserRoles] assignment
                    INNER JOIN @UnresolvedRoles expected ON expected.[Id] = assignment.[RoleId]
                    WHERE assignment.[IsDeleted] = 0
                )
                BEGIN
                    THROW 51004, 'A reviewed-unassigned legacy Role now has an active UserRole assignment.', 1;
                END;

                IF
                (
                    SELECT COUNT(*)
                    FROM [dbo].[Roles]
                    WHERE [TenantId] = @OrganizationTenantId
                      AND [IsDeleted] = 0
                      AND [IsSystem] = 0
                      AND [SystemRoleKey] IS NULL
                ) <> 7
                OR EXISTS
                (
                    SELECT 1
                    FROM [dbo].[Roles] actual
                    WHERE actual.[IsDeleted] = 0
                      AND actual.[IsSystem] = 0
                      AND actual.[SystemRoleKey] IS NULL
                      AND NOT EXISTS (SELECT 1 FROM @ApprovedRoles expected WHERE expected.[Id] = actual.[Id])
                      AND NOT EXISTS (SELECT 1 FROM @UnresolvedRoles expected WHERE expected.[Id] = actual.[Id])
                )
                BEGIN
                    THROW 51005, 'The active operational Role inventory differs from the reviewed two-approved/five-unresolved baseline.', 1;
                END;

                DECLARE @ProtectedRoles TABLE
                (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SystemRoleKey] nvarchar(64) NOT NULL
                );
                INSERT INTO @ProtectedRoles ([Id], [SystemRoleKey])
                VALUES
                    ('A0000001-0000-0000-0000-000000000001', N'tenant.admin'),
                    ('A0000009-0000-0000-0000-000000000009', N'organization.admin');

                IF EXISTS
                (
                    SELECT 1
                    FROM @ProtectedRoles expected
                    LEFT JOIN [dbo].[Roles] actual ON actual.[Id] = expected.[Id]
                    WHERE actual.[Id] IS NULL
                       OR actual.[TenantId] <> @OrganizationTenantId
                       OR actual.[SystemRoleKey] IS NULL
                       OR actual.[SystemRoleKey] COLLATE Latin1_General_100_BIN2 <> expected.[SystemRoleKey] COLLATE Latin1_General_100_BIN2
                       OR actual.[RoleKey] IS NULL
                       OR actual.[RoleKey] COLLATE Latin1_General_100_BIN2 <> expected.[SystemRoleKey] COLLATE Latin1_General_100_BIN2
                       OR actual.[IsDeleted] <> 0
                       OR actual.[IsSystem] <> 1
                       OR actual.[OrganizationId] IS NOT NULL
                       OR actual.[IsLegacyOrganizationOwnershipUnresolved] <> 0
                )
                OR
                (
                    SELECT COUNT(*)
                    FROM [dbo].[Roles]
                    WHERE [TenantId] = @OrganizationTenantId
                      AND [IsDeleted] = 0
                      AND [IsSystem] = 1
                ) <> 2
                BEGIN
                    THROW 51006, 'Protected Tenant Admin or Organization Admin identity differs from the reviewed Development baseline.', 1;
                END;

                UPDATE [dbo].[Roles]
                SET [OrganizationId] = @OrganizationId,
                    [IsLegacyOrganizationOwnershipUnresolved] = 0
                WHERE [TenantId] = @OrganizationTenantId
                  AND [Id] IN (SELECT [Id] FROM @ApprovedRoles)
                  AND [IsDeleted] = 0
                  AND [IsSystem] = 0
                  AND [SystemRoleKey] IS NULL;

                IF @@ROWCOUNT <> 2
                BEGIN
                    THROW 51007, 'The approved Role ownership update did not affect exactly two rows.', 1;
                END;

                UPDATE [dbo].[Roles]
                SET [IsLegacyOrganizationOwnershipUnresolved] = 1
                WHERE [OrganizationId] IS NULL
                  AND [IsSystem] = 0
                  AND [SystemRoleKey] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_Organization_Name",
                table: "Roles",
                columns: new[] { "TenantId", "OrganizationId", "Name" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_Unowned_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[OrganizationId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_OrganizationOwnershipMatchesRoleKind",
                table: "Roles",
                sql: "([IsSystem] = 0 AND [SystemRoleKey] IS NULL AND (([OrganizationId] IS NOT NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 0) OR ([OrganizationId] IS NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 1))) OR ([IsSystem] = 1 AND [OrganizationId] IS NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 0 AND [SystemRoleKey] COLLATE Latin1_General_100_BIN2 IN (N'tenant.admin', N'organization.admin'))");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Organizations_TenantId_OrganizationId",
                table: "Roles",
                columns: new[] { "TenantId", "OrganizationId" },
                principalTable: "Organizations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [dbo].[Roles] "
                + "WHERE [OrganizationId] IS NOT NULL OR [IsLegacyOrganizationOwnershipUnresolved] = 1) "
                + "THROW 51000, 'Cannot roll back Organization-Owned Roles after ownership or unresolved legacy state exists.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Organizations_TenantId_OrganizationId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UX_Roles_Tenant_Organization_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UX_Roles_Tenant_Unowned_Name",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_OrganizationOwnershipMatchesRoleKind",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsLegacyOrganizationOwnershipUnresolved",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
