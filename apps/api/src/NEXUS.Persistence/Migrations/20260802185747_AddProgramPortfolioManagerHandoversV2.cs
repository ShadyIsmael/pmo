using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramPortfolioManagerHandoversV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PortfolioHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PortfolioManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioManagerAssignments_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandoverNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProgramHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramHandoverRequests_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProgramManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramManagerAssignments_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandoverNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProgramSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramSnapshots_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_PortfolioId",
                table: "PortfolioHandoverRequests",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_PortfolioId",
                table: "PortfolioManagerAssignments",
                column: "PortfolioId",
                unique: true,
                filter: "[IsCurrent] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_PortfolioManagerId",
                table: "PortfolioManagerAssignments",
                column: "PortfolioManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_TenantId",
                table: "PortfolioManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_PortfolioId",
                table: "PortfolioSnapshots",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_ProgramId",
                table: "ProgramHandoverRequests",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_TenantId",
                table: "ProgramHandoverRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_TenantId_ProgramId_Status",
                table: "ProgramHandoverRequests",
                columns: new[] { "TenantId", "ProgramId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_ProgramId",
                table: "ProgramManagerAssignments",
                column: "ProgramId",
                unique: true,
                filter: "[IsCurrent] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_ProgramManagerId",
                table: "ProgramManagerAssignments",
                column: "ProgramManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_TenantId",
                table: "ProgramManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_ProgramId",
                table: "ProgramSnapshots",
                column: "ProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioHandoverRequests");

            migrationBuilder.DropTable(
                name: "PortfolioManagerAssignments");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "ProgramHandoverRequests");

            migrationBuilder.DropTable(
                name: "ProgramManagerAssignments");

            migrationBuilder.DropTable(
                name: "ProgramSnapshots");
        }
    }
}
