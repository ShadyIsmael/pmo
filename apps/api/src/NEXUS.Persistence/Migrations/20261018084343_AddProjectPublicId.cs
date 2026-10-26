using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            // Backfill each existing row independently before enforcing uniqueness.
            migrationBuilder.Sql("UPDATE [Projects] SET [PublicId] = NEWID();");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PublicId",
                table: "Projects",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_PublicId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Projects");
        }
    }
}
