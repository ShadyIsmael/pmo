using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeHandoverSnapshotPreviousManagerNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid?>(
                name: "PreviousPortfolioManagerId",
                table: "PortfolioSnapshots",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid?>(
                name: "PreviousProgramManagerId",
                table: "ProgramSnapshots",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [PortfolioSnapshots] WHERE [PreviousPortfolioManagerId] IS NULL) OR EXISTS (SELECT 1 FROM [ProgramSnapshots] WHERE [PreviousProgramManagerId] IS NULL) BEGIN ;THROW 51000, 'Cannot downgrade while initial-assignment handover snapshots have no previous manager.', 1; END;");

            migrationBuilder.AlterColumn<Guid>(
                name: "PreviousPortfolioManagerId",
                table: "PortfolioSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid?),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PreviousProgramManagerId",
                table: "ProgramSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid?),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
