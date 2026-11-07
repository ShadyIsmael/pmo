using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantScopedEmailProfileUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_EmailNotificationProfiles_Tenant",
                table: "EmailNotificationProfiles",
                column: "TenantId",
                unique: true,
                filter: "[Scope] = N'Tenant' AND [DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_EmailNotificationProfiles_Tenant",
                table: "EmailNotificationProfiles");
        }
    }
}
