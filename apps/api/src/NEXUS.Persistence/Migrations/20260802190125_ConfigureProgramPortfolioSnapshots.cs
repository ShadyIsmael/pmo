using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureProgramPortfolioSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramSnapshots_Programs_ProgramId",
                table: "ProgramSnapshots");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProgramSnapshots");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgramSnapshots",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ProgramSnapshots",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "HandoverReason",
                table: "ProgramSnapshots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "HandoverNotes",
                table: "ProgramSnapshots",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_TenantId",
                table: "ProgramSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_TenantId_ProgramId_SnapshotNumber",
                table: "ProgramSnapshots",
                columns: new[] { "TenantId", "ProgramId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramSnapshots_Programs_ProgramId",
                table: "ProgramSnapshots",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramSnapshots_Programs_ProgramId",
                table: "ProgramSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProgramSnapshots_TenantId",
                table: "ProgramSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProgramSnapshots_TenantId_ProgramId_SnapshotNumber",
                table: "ProgramSnapshots");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProgramSnapshots");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgramSnapshots",
                type: "varbinary(max)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ProgramSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "HandoverReason",
                table: "ProgramSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "HandoverNotes",
                table: "ProgramSnapshots",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramSnapshots_Programs_ProgramId",
                table: "ProgramSnapshots",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
