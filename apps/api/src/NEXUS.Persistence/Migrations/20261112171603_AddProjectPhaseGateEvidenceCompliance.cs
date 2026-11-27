using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhaseGateEvidenceCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ComparedAt",
                table: "ProjectPhaseGateEvidence",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceFindingsJson",
                table: "ProjectPhaseGateEvidence",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceStatus",
                table: "ProjectPhaseGateEvidence",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PendingValidation");

            migrationBuilder.AddColumn<string>(
                name: "DetectedFileType",
                table: "ProjectPhaseGateEvidence",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedFileType",
                table: "ProjectPhaseGateEvidence",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidatorVersion",
                table: "ProjectPhaseGateEvidence",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComparedAt",
                table: "ProjectPhaseGateEvidence");

            migrationBuilder.DropColumn(
                name: "ComplianceFindingsJson",
                table: "ProjectPhaseGateEvidence");

            migrationBuilder.DropColumn(
                name: "ComplianceStatus",
                table: "ProjectPhaseGateEvidence");

            migrationBuilder.DropColumn(
                name: "DetectedFileType",
                table: "ProjectPhaseGateEvidence");

            migrationBuilder.DropColumn(
                name: "ExpectedFileType",
                table: "ProjectPhaseGateEvidence");

            migrationBuilder.DropColumn(
                name: "ValidatorVersion",
                table: "ProjectPhaseGateEvidence");

        }
    }
}
