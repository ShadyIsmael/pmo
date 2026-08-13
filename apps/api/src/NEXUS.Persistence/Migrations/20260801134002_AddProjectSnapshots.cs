using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectManagerAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PlannedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    EarnedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CostVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ScheduleVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Health = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RagStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskRating = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessCase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProjectSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_ProjectId",
                table: "ProjectManagerAssignments",
                column: "ProjectId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_ProjectManagerId",
                table: "ProjectManagerAssignments",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_TenantId",
                table: "ProjectManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_ProjectId",
                table: "ProjectSnapshots",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_SnapshotNumber",
                table: "ProjectSnapshots",
                column: "SnapshotNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_TenantId",
                table: "ProjectSnapshots",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectManagerAssignments");

            migrationBuilder.DropTable(
                name: "ProjectSnapshots");
        }
    }
}
