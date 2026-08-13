using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureProgramPortfolioManagerHandoverConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                table: "PortfolioHandoverRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                table: "PortfolioSnapshots");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PortfolioSnapshots");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PortfolioSnapshots",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "PortfolioSnapshots",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "HandoverReason",
                table: "PortfolioSnapshots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "HandoverNotes",
                table: "PortfolioSnapshots",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PortfolioHandoverRequests");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PortfolioHandoverRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_TenantId",
                table: "PortfolioSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_TenantId_PortfolioId_SnapshotNumber",
                table: "PortfolioSnapshots",
                columns: new[] { "TenantId", "PortfolioId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_TenantId",
                table: "PortfolioHandoverRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_TenantId_PortfolioId_Status",
                table: "PortfolioHandoverRequests",
                columns: new[] { "TenantId", "PortfolioId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending' AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                table: "PortfolioHandoverRequests",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                table: "PortfolioSnapshots",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                table: "PortfolioHandoverRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                table: "PortfolioSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioSnapshots_TenantId",
                table: "PortfolioSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioSnapshots_TenantId_PortfolioId_SnapshotNumber",
                table: "PortfolioSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioHandoverRequests_TenantId",
                table: "PortfolioHandoverRequests");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioHandoverRequests_TenantId_PortfolioId_Status",
                table: "PortfolioHandoverRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PortfolioSnapshots");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PortfolioSnapshots",
                type: "varbinary(max)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "PortfolioSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "HandoverReason",
                table: "PortfolioSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "HandoverNotes",
                table: "PortfolioSnapshots",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PortfolioHandoverRequests");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PortfolioHandoverRequests",
                type: "varbinary(max)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                table: "PortfolioHandoverRequests",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                table: "PortfolioSnapshots",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
