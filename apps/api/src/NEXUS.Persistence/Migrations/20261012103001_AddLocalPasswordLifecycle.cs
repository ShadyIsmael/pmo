using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPasswordLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AuthenticationSource",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [AuthenticationSource] = N'EntraId'
                WHERE EXISTS (
                    SELECT 1
                    FROM [DirectoryUserLinks] AS [link]
                    INNER JOIN [DirectoryConnections] AS [connection]
                        ON [connection].[Id] = [link].[DirectoryConnectionId]
                       AND [connection].[TenantId] = [link].[TenantId]
                    WHERE [link].[UserId] = [Users].[Id]
                      AND [link].[TenantId] = [Users].[TenantId]
                      AND [link].[IsDeleted] = 0
                      AND [connection].[IsDeleted] = 0
                      AND [connection].[ProviderType] = N'Entra'
                );

                UPDATE [Users]
                SET [AuthenticationSource] = N'ActiveDirectoryLdap'
                WHERE EXISTS (
                    SELECT 1
                    FROM [DirectoryUserLinks] AS [link]
                    INNER JOIN [DirectoryConnections] AS [connection]
                        ON [connection].[Id] = [link].[DirectoryConnectionId]
                       AND [connection].[TenantId] = [link].[TenantId]
                    WHERE [link].[UserId] = [Users].[Id]
                      AND [link].[TenantId] = [Users].[TenantId]
                      AND [link].[IsDeleted] = 0
                      AND [connection].[IsDeleted] = 0
                      AND [connection].[ProviderType] = N'Ldap'
                )
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [DirectoryUserLinks] AS [link]
                    INNER JOIN [DirectoryConnections] AS [connection]
                        ON [connection].[Id] = [link].[DirectoryConnectionId]
                       AND [connection].[TenantId] = [link].[TenantId]
                    WHERE [link].[UserId] = [Users].[Id]
                      AND [link].[TenantId] = [Users].[TenantId]
                      AND [link].[IsDeleted] = 0
                      AND [connection].[IsDeleted] = 0
                      AND [connection].[ProviderType] = N'Entra'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticationSource",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAt",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
