using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStableSystemRoleIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemRoleKey",
                table: "Roles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // One-time bridge for previously provisioned Tenant Admin roles. The
            // exact binary display-name match is intentionally bounded to active
            // system roles with a Tenant identity. Runtime protection uses the new
            // stable key exclusively after this migration.
            migrationBuilder.Sql(
                "IF EXISTS (\n"
                + "    SELECT [TenantId]\n"
                + "    FROM [dbo].[Roles]\n"
                + "    WHERE [TenantId] IS NOT NULL\n"
                + "      AND [IsDeleted] = 0\n"
                + "      AND [IsSystem] = 1\n"
                + "      AND [Name] COLLATE Latin1_General_100_BIN2 = N'admin'\n"
                + "    GROUP BY [TenantId]\n"
                + "    HAVING COUNT_BIG(*) > 1\n"
                + ")\n"
                + "BEGIN\n"
                + "    THROW 51001, 'Stable Tenant Admin identity migration stopped: duplicate active system admin roles exist for a Tenant.', 1;\n"
                + "END;\n"
                + "UPDATE [dbo].[Roles]\n"
                + "SET [SystemRoleKey] = N'tenant.admin'\n"
                + "WHERE [TenantId] IS NOT NULL\n"
                + "  AND [IsDeleted] = 0\n"
                + "  AND [IsSystem] = 1\n"
                + "  AND [Name] COLLATE Latin1_General_100_BIN2 = N'admin';");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_SystemRoleKey",
                table: "Roles",
                columns: new[] { "TenantId", "SystemRoleKey" },
                unique: true,
                filter: "[SystemRoleKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_SystemRoleKeyRequiresSystemRole",
                table: "Roles",
                sql: "[SystemRoleKey] IS NULL OR [IsSystem] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Roles_Tenant_SystemRoleKey",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_SystemRoleKeyRequiresSystemRole",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "SystemRoleKey",
                table: "Roles");
        }
    }
}
