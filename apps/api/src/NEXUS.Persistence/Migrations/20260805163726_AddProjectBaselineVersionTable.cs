using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBaselineVersionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBaselineVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ApprovedBaselineMetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBaselineVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBaselineVersions_ProjectBaselineVersions_ApprovedBaselineMetadataId",
                        column: x => x.ApprovedBaselineMetadataId,
                        principalTable: "ProjectBaselineVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselineVersions_ApprovedBaselineMetadataId",
                table: "ProjectBaselineVersions",
                column: "ApprovedBaselineMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Project_CurrentApproved",
                table: "ProjectBaselineVersions",
                columns: new[] { "TenantId", "ProjectId", "IsCurrent", "Status" },
                filter: "\"IsCurrent\" = 1 AND \"Status\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Project_Version",
                table: "ProjectBaselineVersions",
                columns: new[] { "TenantId", "ProjectId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectBaselineVersions");
        }
    }
}
