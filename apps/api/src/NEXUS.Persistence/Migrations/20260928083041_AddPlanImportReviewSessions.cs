using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanImportReviewSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SuggestedPlanName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ParsedDocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolutionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommitResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanImportIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanImportSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanImportIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanImportIssues_PlanImportSessions_PlanImportSessionId",
                        column: x => x.PlanImportSessionId,
                        principalTable: "PlanImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanImportIssues_PlanImportSessionId",
                table: "PlanImportIssues",
                column: "PlanImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanImportIssues_TenantId_PlanImportSessionId_IsBlocking",
                table: "PlanImportIssues",
                columns: new[] { "TenantId", "PlanImportSessionId", "IsBlocking" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanImportSessions_TenantId_ProjectId_Status",
                table: "PlanImportSessions",
                columns: new[] { "TenantId", "ProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanImportIssues");

            migrationBuilder.DropTable(
                name: "PlanImportSessions");
        }
    }
}
