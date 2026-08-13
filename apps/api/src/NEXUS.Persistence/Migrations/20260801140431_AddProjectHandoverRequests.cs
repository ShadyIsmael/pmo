using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectHandoverRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverRequests_ProjectSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "ProjectSnapshots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectHandoverRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_NewProjectManagerId",
                table: "ProjectHandoverRequests",
                column: "NewProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_ProjectId",
                table: "ProjectHandoverRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_SnapshotId",
                table: "ProjectHandoverRequests",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_Status",
                table: "ProjectHandoverRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_TenantId",
                table: "ProjectHandoverRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectHandoverRequests");
        }
    }
}
