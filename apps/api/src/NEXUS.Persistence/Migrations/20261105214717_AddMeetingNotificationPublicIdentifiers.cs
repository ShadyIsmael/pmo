using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingNotificationPublicIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferencePublicId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Meetings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "MeetingMinutesComments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_PublicId",
                table: "Meetings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesComments_PublicId",
                table: "MeetingMinutesComments",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_PublicId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_MeetingMinutesComments_PublicId",
                table: "MeetingMinutesComments");

            migrationBuilder.DropColumn(
                name: "ReferencePublicId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "MeetingMinutesComments");
        }
    }
}
