using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDeviceTokens_TenantId_Token",
                table: "UserDeviceTokens");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "UserDeviceTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [UserDeviceTokens]
                SET [TokenHash] = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), [Token])), 2)
                WHERE [TokenHash] IS NULL;

                ;WITH RankedActiveTokens AS
                (
                    SELECT [Id], ROW_NUMBER() OVER
                    (
                        PARTITION BY [TokenHash]
                        ORDER BY [LastSeenAt] DESC, [CreatedAt] DESC, [Id]
                    ) AS [DuplicateRank]
                    FROM [UserDeviceTokens]
                    WHERE [IsActive] = 1
                )
                UPDATE [UserDeviceTokens]
                SET [IsActive] = 0,
                    [DeactivatedAt] = COALESCE([DeactivatedAt], SYSUTCDATETIME()),
                    [UpdatedAt] = SYSUTCDATETIME(),
                    [UpdatedBy] = N'NotificationDeliveryMigration'
                FROM [UserDeviceTokens] AS devices
                INNER JOIN RankedActiveTokens AS ranked ON ranked.[Id] = devices.[Id]
                WHERE ranked.[DuplicateRank] > 1;

                ;WITH RankedPreferences AS
                (
                    SELECT [Id], ROW_NUMBER() OVER
                    (
                        PARTITION BY [TenantId], [UserId], [Type]
                        ORDER BY [UpdatedAt] DESC, [CreatedAt] DESC, [Id]
                    ) AS [DuplicateRank]
                    FROM [NotificationPreferences]
                    WHERE [IsDeleted] = 0
                )
                UPDATE [NotificationPreferences]
                SET [IsDeleted] = 1,
                    [UpdatedAt] = SYSUTCDATETIME(),
                    [UpdatedBy] = N'NotificationDeliveryMigration'
                FROM [NotificationPreferences] AS preferences
                INNER JOIN RankedPreferences AS ranked ON ranked.[Id] = preferences.[Id]
                WHERE ranked.[DuplicateRank] > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "UserDeviceTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInAppVisible",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "NotificationPreferences",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_NotificationDeliveries_NotificationDeliveryId",
                        column: x => x.NotificationDeliveryId,
                        principalTable: "NotificationDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_UserDeviceTokens_DeviceTokenId",
                        column: x => x.DeviceTokenId,
                        principalTable: "UserDeviceTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_TokenHash",
                table: "UserDeviceTokens",
                column: "TokenHash",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_Type",
                table: "NotificationPreferences",
                columns: new[] { "TenantId", "UserId", "Type" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId",
                table: "NotificationDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_NextAttemptAt",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_ProcessingStartedAt",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "ProcessingStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_NotificationId_Channel",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "NotificationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_DeviceTokenId",
                table: "NotificationDeliveryAttempts",
                column: "DeviceTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_NotificationDeliveryId",
                table: "NotificationDeliveryAttempts",
                column: "NotificationDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_TenantId_DeviceTokenId_Status",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "TenantId", "DeviceTokenId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_TenantId_NotificationDeliveryId_DeviceTokenId",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "TenantId", "NotificationDeliveryId", "DeviceTokenId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_UserDeviceTokens_TokenHash",
                table: "UserDeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_Type",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "UserDeviceTokens");

            migrationBuilder.DropColumn(
                name: "IsInAppVisible",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "NotificationPreferences",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_TenantId_Token",
                table: "UserDeviceTokens",
                columns: new[] { "TenantId", "Token" },
                unique: true);
        }
    }
}
