using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskActionPublicIdForFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "RiskActions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_Tenant_PublicId",
                table: "RiskActions",
                columns: new[] { "TenantId", "PublicId" },
                unique: true,
                filter: "[IsDeleted] = 0");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskActions_Tenant_PublicId",
                table: "RiskActions");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "RiskActions");
        }
    }
}
