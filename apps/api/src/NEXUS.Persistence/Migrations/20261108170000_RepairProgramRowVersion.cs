using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations;

public partial class RepairProgramRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "Programs");

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Programs",
            type: "rowversion",
            rowVersion: true,
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "Programs");

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Programs",
            type: "varbinary(max)",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE [Programs] SET [RowVersion] = 0x WHERE [RowVersion] IS NULL;");

        migrationBuilder.AlterColumn<byte[]>(
            name: "RowVersion",
            table: "Programs",
            type: "varbinary(max)",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "varbinary(max)",
            oldNullable: true);
    }
}
