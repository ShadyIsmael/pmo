using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignResourceProjectTeamsAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [Resources]
                    WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0
                    GROUP BY [TenantId], [UserId]
                    HAVING COUNT_BIG(*) > 1)
                BEGIN
                    ;THROW 51000, 'Resource-to-User alignment is blocked by duplicate non-deleted links.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM [Resources] AS r
                    WHERE r.[UserId] IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM [Users] AS u
                          WHERE u.[TenantId] = r.[TenantId] AND u.[Id] = r.[UserId]))
                BEGIN
                    ;THROW 51000, 'Resource-to-User alignment is blocked by a missing or cross-tenant User link.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM [Resources] AS r
                    WHERE r.[TeamId] IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM [Teams] AS t
                          WHERE t.[TenantId] = r.[TenantId] AND t.[Id] = r.[TeamId]))
                BEGIN
                    ;THROW 51000, 'Resource-to-Team backfill is blocked by a missing or cross-tenant Team link.', 1;
                END;
                """);

            migrationBuilder.AddColumn<string>(
                name: "ConsentVersion",
                table: "StakeholderInvitations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentedAt",
                table: "StakeholderInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeclinedAt",
                table: "StakeholderInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailDeliveryStatus",
                table: "StakeholderInvitations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [StakeholderInvitations]
                SET [EmailDeliveryStatus] = N'Untracked'
                WHERE [EmailDeliveryStatus] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "EmailDeliveryStatus",
                table: "StakeholderInvitations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "StakeholderInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsConsent",
                table: "StakeholderInvitations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReportsConsent",
                table: "StakeholderInvitations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [ProjectMembers]
                    WHERE [ResourceId] IS NULL AND [TeamId] IS NULL)
                BEGIN
                    ;THROW 51000, 'Project Team alignment is blocked by ProjectMembers with no assignment target.', 1;
                END;

                DECLARE @TeamMembershipMigrationTime datetime2 = SYSUTCDATETIME();

                INSERT INTO [TeamMembers]
                    ([Id], [TeamId], [ResourceId], [EffectiveFrom], [EffectiveTo], [IsActive],
                     [TenantId], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted], [RowVersion])
                SELECT
                    NEWID(), r.[TeamId], r.[Id], @TeamMembershipMigrationTime, NULL,
                    CASE WHEN r.[IsActive] = 1 AND t.[IsActive] = 1
                                   AND r.[IsDeleted] = 0 AND t.[IsDeleted] = 0 THEN 1 ELSE 0 END,
                    r.[TenantId], @TeamMembershipMigrationTime, NULL,
                    N'Migration:20261031142036_AlignResourceProjectTeamsAndInvitations', NULL,
                    CASE WHEN r.[IsDeleted] = 1 OR t.[IsDeleted] = 1 THEN 1 ELSE 0 END,
                    CAST(0x AS varbinary(max))
                FROM [Resources] AS r
                INNER JOIN [Teams] AS t
                    ON t.[Id] = r.[TeamId] AND t.[TenantId] = r.[TenantId]
                WHERE r.[TeamId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [TeamMembers] AS tm
                      WHERE tm.[TenantId] = r.[TenantId]
                        AND tm.[TeamId] = r.[TeamId]
                        AND tm.[ResourceId] = r.[Id]
                        AND tm.[IsDeleted] = 0
                        AND tm.[IsActive] = 1
                        AND (tm.[EffectiveTo] IS NULL OR tm.[EffectiveTo] > @TeamMembershipMigrationTime));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_TenantId_UserId",
                table: "Resources",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_TeamId",
                table: "ProjectMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_TenantId_ProjectId_TeamId_Status",
                table: "ProjectMembers",
                columns: new[] { "TenantId", "ProjectId", "TeamId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectMembers_ExactlyOneAssignmentTarget",
                table: "ProjectMembers",
                sql: "(([ResourceId] IS NOT NULL AND [TeamId] IS NULL) OR ([ResourceId] IS NULL AND [TeamId] IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_Teams_TeamId",
                table: "ProjectMembers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Users_TenantId_UserId",
                table: "Resources",
                columns: new[] { "TenantId", "UserId" },
                principalTable: "Users",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ;THROW 51000, 'This migration is intentionally irreversible because rollback would discard Team membership history and invitation consent records.', 1;
                """);
        }
    }
}
