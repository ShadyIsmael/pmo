using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProjectChangeRequestRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectChangeRequest_TypeAndField",
                table: "ProjectChangeRequests",
                sql: "(Type = 'DataChange' AND Field IS NOT NULL) OR (Type = 'BaselineChange' AND Field IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectChangeRequest_TypeAndField",
                table: "ProjectChangeRequests");
        }
    }
}
