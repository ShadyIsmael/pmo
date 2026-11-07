using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalEmailNotificationProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "NotificationDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailNotificationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    SecurityMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AuthenticationMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProtectedPassword = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReplyToAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotificationProfiles", x => x.Id);
                    table.CheckConstraint("CK_EmailNotificationProfiles_AuthenticationMode", "[AuthenticationMode] IN (N'UsernamePassword', N'TrustedRelay')");
                    table.CheckConstraint("CK_EmailNotificationProfiles_EnabledProfile", "[Enabled] = 0 OR (LEN(LTRIM(RTRIM([Host]))) > 0 AND LEN(LTRIM(RTRIM([FromAddress]))) > 0 AND (([AuthenticationMode] = N'UsernamePassword' AND LEN(LTRIM(RTRIM([UserName]))) > 0 AND [ProtectedPassword] IS NOT NULL) OR ([AuthenticationMode] = N'TrustedRelay' AND [UserName] IS NULL AND [ProtectedPassword] IS NULL)))");
                    table.CheckConstraint("CK_EmailNotificationProfiles_Port", "[Port] BETWEEN 1 AND 65535");
                    table.CheckConstraint("CK_EmailNotificationProfiles_ScopeOwner", "(([Scope] = N'Platform' AND [TenantId] IS NULL AND [OwnerId] IS NULL AND [OrganizationId] IS NULL) OR ([Scope] = N'Tenant' AND [TenantId] IS NOT NULL AND [OwnerId] = [TenantId] AND [OrganizationId] IS NULL) OR ([Scope] = N'Organization' AND [TenantId] IS NOT NULL AND [OwnerId] = [OrganizationId] AND [OrganizationId] IS NOT NULL))");
                    table.CheckConstraint("CK_EmailNotificationProfiles_SecurityMode", "[SecurityMode] IN (N'StartTlsRequired', N'ImplicitTls')");
                    table.ForeignKey(
                        name: "FK_EmailNotificationProfiles_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailNotificationProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_OrganizationId",
                table: "Notifications",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationProfiles_ActiveOwner",
                table: "EmailNotificationProfiles",
                columns: new[] { "Scope", "TenantId", "OwnerId" },
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationProfiles_TenantId_OrganizationId",
                table: "EmailNotificationProfiles",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "UX_EmailNotificationProfiles_Organization",
                table: "EmailNotificationProfiles",
                columns: new[] { "TenantId", "OwnerId" },
                unique: true,
                filter: "[Scope] = N'Organization' AND [DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_EmailNotificationProfiles_Platform",
                table: "EmailNotificationProfiles",
                column: "Scope",
                unique: true,
                filter: "[Scope] = N'Platform' AND [DeletedAt] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Organizations_TenantId_OrganizationId",
                table: "Notifications",
                columns: new[] { "TenantId", "OrganizationId" },
                principalTable: "Organizations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Organizations_TenantId_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "EmailNotificationProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "NotificationDeliveries");
        }
    }
}
