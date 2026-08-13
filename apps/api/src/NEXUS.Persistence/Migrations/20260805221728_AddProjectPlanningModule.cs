using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPlanningModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceBaselineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanVersions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    EstimateHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    WbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanActivities_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    HoursPerWeek = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PlanAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanAssignments_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanDeliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDeliverables_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Lag = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PlanDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDependencies_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanWbsNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentWbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanWbsNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanWbsNodes_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanWbsNodes_PlanWbsNodes_ParentWbsId",
                        column: x => x.ParentWbsId,
                        principalTable: "PlanWbsNodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_PlanVersionId",
                table: "PlanActivities",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_Tenant_Version_Code",
                table: "PlanActivities",
                columns: new[] { "TenantId", "PlanVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_PlanVersionId",
                table: "PlanAssignments",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanDeliverables_PlanVersionId",
                table: "PlanDeliverables",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDeliverables_Tenant_Version_Name",
                table: "PlanDeliverables",
                columns: new[] { "TenantId", "PlanVersionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanDependencies_PlanVersionId",
                table: "PlanDependencies",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDependencies_Tenant_Version_Edge",
                table: "PlanDependencies",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "PredecessorActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Tenant_Project",
                table: "Plans",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_PlanId",
                table: "PlanVersions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_ActiveWorking",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "Type" },
                unique: true,
                filter: "\"Type\" = 0 AND \"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_ApprovedBaseline",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "Status" },
                unique: true,
                filter: "\"Type\" = 1 AND \"Status\" = 3");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_Version",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_ParentWbsId",
                table: "PlanWbsNodes",
                column: "ParentWbsId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_PlanVersionId",
                table: "PlanWbsNodes",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_Tenant_Version_Parent_Code",
                table: "PlanWbsNodes",
                columns: new[] { "TenantId", "PlanVersionId", "ParentWbsId", "Code" },
                unique: true,
                filter: "[ParentWbsId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanActivities");

            migrationBuilder.DropTable(
                name: "PlanAssignments");

            migrationBuilder.DropTable(
                name: "PlanDeliverables");

            migrationBuilder.DropTable(
                name: "PlanDependencies");

            migrationBuilder.DropTable(
                name: "PlanWbsNodes");

            migrationBuilder.DropTable(
                name: "PlanVersions");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
