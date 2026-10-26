using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NEXUS.Persistence;

#nullable disable

namespace NEXUS.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261021160000_AddGovernanceRecoveryPublicIds")]
public sealed class AddGovernanceRecoveryPublicIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PublicId", table: "Programs", type: "uniqueidentifier", nullable: false,
            defaultValueSql: "NEWID()");
        migrationBuilder.AddColumn<Guid>(
            name: "PublicId", table: "Portfolios", type: "uniqueidentifier", nullable: false,
            defaultValueSql: "NEWID()");
        migrationBuilder.CreateIndex(name: "IX_Programs_PublicId", table: "Programs", column: "PublicId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Portfolios_PublicId", table: "Portfolios", column: "PublicId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Programs_PublicId", table: "Programs");
        migrationBuilder.DropIndex(name: "IX_Portfolios_PublicId", table: "Portfolios");
        migrationBuilder.DropColumn(name: "PublicId", table: "Programs");
        migrationBuilder.DropColumn(name: "PublicId", table: "Portfolios");
    }
}
