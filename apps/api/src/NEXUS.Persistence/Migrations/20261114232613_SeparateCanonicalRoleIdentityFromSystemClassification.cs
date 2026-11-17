using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateCanonicalRoleIdentityFromSystemClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_SystemRoleKeyRequiresSystemRole",
                table: "Roles");

            migrationBuilder.AddColumn<string>(
                name: "RoleKey",
                table: "Roles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Guard before changing any Tenant-owned Role rows. The old filtered
            // SystemRoleKey index should already make the collision impossible,
            // but an explicit check keeps this migration fail-closed if its source
            // database has drifted. No role name or description is used to infer
            // canonical identity.
            migrationBuilder.Sql(
                "IF EXISTS (\n"
                + "    SELECT [TenantId], [SystemRoleKey]\n"
                + "    FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL AND [SystemRoleKey] IS NOT NULL AND [IsDeleted] = 0\n"
                + "    GROUP BY [TenantId], [SystemRoleKey]\n"
                + "    HAVING COUNT_BIG(*) > 1\n"
                + ")\n"
                + "    THROW 51031, 'Role identity migration stopped: duplicate active Tenant SystemRoleKeys exist.', 1;\n"
                + "IF EXISTS (\n"
                + "    SELECT 1 FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL AND [IsSystem] = 0 AND [SystemRoleKey] IS NOT NULL\n"
                + ")\n"
                + "    THROW 51039, 'Role identity migration stopped: a non-System Tenant Role carries a SystemRoleKey.', 1;\n"
                + "IF EXISTS (\n"
                + "    SELECT 1 FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL AND [SystemRoleKey] IS NOT NULL AND [RoleKey] IS NOT NULL\n"
                + "      AND [RoleKey] COLLATE Latin1_General_100_BIN2 <> [SystemRoleKey] COLLATE Latin1_General_100_BIN2\n"
                + ")\n"
                + "    THROW 51032, 'Role identity migration stopped: an existing RoleKey conflicts with SystemRoleKey.', 1;\n"
                + "IF EXISTS (\n"
                + "    SELECT [TenantId], [RoleKey]\n"
                + "    FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL AND [RoleKey] IS NOT NULL AND [IsDeleted] = 0\n"
                + "    GROUP BY [TenantId], [RoleKey]\n"
                + "    HAVING COUNT_BIG(*) > 1\n"
                + ")\n"
                + "    THROW 51033, 'Role identity migration stopped: duplicate active Tenant RoleKeys exist.', 1;\n"
                + "IF EXISTS (\n"
                + "    SELECT [TenantId], [CanonicalRoleKey]\n"
                + "    FROM (\n"
                + "        SELECT [TenantId], COALESCE([RoleKey], [SystemRoleKey]) AS [CanonicalRoleKey]\n"
                + "        FROM [dbo].[Roles]\n"
                + "        WHERE [TenantId] IS NOT NULL AND [IsDeleted] = 0\n"
                + "          AND ([RoleKey] IS NOT NULL OR [SystemRoleKey] IS NOT NULL)\n"
                + "    ) AS [CandidateRoleKeys]\n"
                + "    GROUP BY [TenantId], [CanonicalRoleKey]\n"
                + "    HAVING COUNT_BIG(*) > 1\n"
                + ")\n"
                + "    THROW 51038, 'Role identity migration stopped: projected canonical Tenant RoleKeys collide.', 1;\n"
                + "IF EXISTS (\n"
                + "    SELECT 1 FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NULL AND ([IsSystem] = 1 OR [SystemRoleKey] IS NOT NULL)\n"
                + ")\n"
                + "    THROW 51034, 'Role identity migration stopped: a non-Tenant System Role cannot be reconciled by this Tenant migration.', 1;\n"
                + "UPDATE [dbo].[Roles]\n"
                + "SET [RoleKey] = [SystemRoleKey]\n"
                + "WHERE [TenantId] IS NOT NULL AND [RoleKey] IS NULL AND [SystemRoleKey] IS NOT NULL;\n"
                + "UPDATE [dbo].[Roles]\n"
                + "SET [IsSystem] = 0, [SystemRoleKey] = NULL\n"
                + "WHERE [TenantId] IS NOT NULL AND [IsSystem] = 1\n"
                + "  AND ([SystemRoleKey] IS NULL\n"
                + "       OR [SystemRoleKey] COLLATE Latin1_General_100_BIN2 NOT IN (N'tenant.admin', N'organization.admin'));\n"
                + "IF EXISTS (\n"
                + "    SELECT 1 FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL AND [IsSystem] = 1\n"
                + "      AND ([SystemRoleKey] IS NULL\n"
                + "           OR [SystemRoleKey] COLLATE Latin1_General_100_BIN2 NOT IN (N'tenant.admin', N'organization.admin')\n"
                + "           OR [RoleKey] COLLATE Latin1_General_100_BIN2 <> [SystemRoleKey] COLLATE Latin1_General_100_BIN2)\n"
                + ")\n"
                + "    THROW 51035, 'Role identity migration stopped: an unapproved or inconsistent Tenant System Role remains.', 1;");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_RoleKey",
                table: "Roles",
                columns: new[] { "TenantId", "RoleKey" },
                unique: true,
                filter: "[RoleKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_SystemRoleKeyRequiresSystemRole",
                table: "Roles",
                sql: "([IsSystem] = 0 AND [SystemRoleKey] IS NULL AND ([RoleKey] IS NULL OR [RoleKey] COLLATE Latin1_General_100_BIN2 NOT IN (N'tenant.admin', N'organization.admin'))) OR ([IsSystem] = 1 AND [SystemRoleKey] IS NOT NULL AND [RoleKey] IS NOT NULL AND [SystemRoleKey] COLLATE Latin1_General_100_BIN2 IN (N'tenant.admin', N'organization.admin') AND [RoleKey] COLLATE Latin1_General_100_BIN2 = [SystemRoleKey] COLLATE Latin1_General_100_BIN2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration is intentionally forward-only. Restoring the legacy
            // classification would turn the seven operational roles back into
            // System Roles and violate DEC-RBAC-ORG-001. Fail before any DDL or
            // data mutation; recovery requires a separately approved forward fix.
            migrationBuilder.Sql(
                "THROW 51041, 'Role identity migration is forward-only: rollback would violate the approved two-System-Role policy. Use a separately approved forward repair.', 1;");
        }
    }
}
