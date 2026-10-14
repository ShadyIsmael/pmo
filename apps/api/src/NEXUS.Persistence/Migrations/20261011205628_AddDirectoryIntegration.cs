using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    DirectoryTenantId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CredentialReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: true),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    BaseDn = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BindUser = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BindCredentialReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
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
                    table.PrimaryKey("PK_DirectoryConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryFieldMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetField = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceAttribute = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatePolicy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Transform = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_DirectoryFieldMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryGroupMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalGroupId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalGroupName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_DirectoryGroupMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDirectoryId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_DirectoryGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryOrganizationMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrganizationalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_DirectoryOrganizationMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryProvisioningPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MissingUserAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MissingGroupAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisabledUserAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OutOfScopeUserAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedGroupAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MissingGroupReferenceAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreateResource = table.Column<bool>(type: "bit", nullable: false),
                    DefaultOrganizationalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultBaseCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultEmploymentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultSeniority = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_DirectoryProvisioningPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryRoleGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalGroupId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_DirectoryRoleGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectorySyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UsersFetched = table.Column<int>(type: "int", nullable: false),
                    UsersCreated = table.Column<int>(type: "int", nullable: false),
                    UsersUpdated = table.Column<int>(type: "int", nullable: false),
                    UsersDeactivated = table.Column<int>(type: "int", nullable: false),
                    UsersSkipped = table.Column<int>(type: "int", nullable: false),
                    GroupsCreated = table.Column<int>(type: "int", nullable: false),
                    GroupsUpdated = table.Column<int>(type: "int", nullable: false),
                    GroupsDeactivated = table.Column<int>(type: "int", nullable: false),
                    Conflicts = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DirectorySyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectorySyncScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SelectedGroupIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedUserIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrganizationalUnitsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserFilter = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    GroupFilter = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    MaxItems = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DirectorySyncScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryUserLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDirectoryId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmployeeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManagerExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ManagedOrganizationalMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagedReportingLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDirectoryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastImportedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DirectoryUserLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectorySyncItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectorySyncRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExternalDirectoryId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProposedAction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MatchedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_DirectorySyncItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectorySyncItems_DirectorySyncRuns_DirectorySyncRunId",
                        column: x => x.DirectorySyncRunId,
                        principalTable: "DirectorySyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryConnections_TenantId_IsDefault",
                table: "DirectoryConnections",
                columns: new[] { "TenantId", "IsDefault" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryConnections_TenantId_Name",
                table: "DirectoryConnections",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryFieldMappings_TenantId_DirectoryConnectionId_TargetField",
                table: "DirectoryFieldMappings",
                columns: new[] { "TenantId", "DirectoryConnectionId", "TargetField" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroupMappings_TenantId_DirectoryConnectionId_ExternalGroupId",
                table: "DirectoryGroupMappings",
                columns: new[] { "TenantId", "DirectoryConnectionId", "ExternalGroupId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroupMappings_TenantId_DirectoryConnectionId_RoleId",
                table: "DirectoryGroupMappings",
                columns: new[] { "TenantId", "DirectoryConnectionId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_TenantId_DirectoryConnectionId_ExternalDirectoryId",
                table: "DirectoryGroups",
                columns: new[] { "TenantId", "DirectoryConnectionId", "ExternalDirectoryId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryOrganizationMappings_TenantId_DirectoryConnectionId_DirectoryValue",
                table: "DirectoryOrganizationMappings",
                columns: new[] { "TenantId", "DirectoryConnectionId", "DirectoryValue" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryProvisioningPolicies_TenantId_DirectoryConnectionId",
                table: "DirectoryProvisioningPolicies",
                columns: new[] { "TenantId", "DirectoryConnectionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryRoleGrants_TenantId_DirectoryConnectionId_ExternalGroupId_UserId_RoleId",
                table: "DirectoryRoleGrants",
                columns: new[] { "TenantId", "DirectoryConnectionId", "ExternalGroupId", "UserId", "RoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncItems_DirectorySyncRunId",
                table: "DirectorySyncItems",
                column: "DirectorySyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncItems_TenantId_DirectorySyncRunId",
                table: "DirectorySyncItems",
                columns: new[] { "TenantId", "DirectorySyncRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncRuns_TenantId_DirectoryConnectionId_StartedAt",
                table: "DirectorySyncRuns",
                columns: new[] { "TenantId", "DirectoryConnectionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncScopes_TenantId_DirectoryConnectionId",
                table: "DirectorySyncScopes",
                columns: new[] { "TenantId", "DirectoryConnectionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryUserLinks_TenantId_DirectoryConnectionId_ExternalDirectoryId",
                table: "DirectoryUserLinks",
                columns: new[] { "TenantId", "DirectoryConnectionId", "ExternalDirectoryId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryUserLinks_TenantId_DirectoryConnectionId_UserId",
                table: "DirectoryUserLinks",
                columns: new[] { "TenantId", "DirectoryConnectionId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryUserLinks_TenantId_UserPrincipalName",
                table: "DirectoryUserLinks",
                columns: new[] { "TenantId", "UserPrincipalName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryConnections");

            migrationBuilder.DropTable(
                name: "DirectoryFieldMappings");

            migrationBuilder.DropTable(
                name: "DirectoryGroupMappings");

            migrationBuilder.DropTable(
                name: "DirectoryGroups");

            migrationBuilder.DropTable(
                name: "DirectoryOrganizationMappings");

            migrationBuilder.DropTable(
                name: "DirectoryProvisioningPolicies");

            migrationBuilder.DropTable(
                name: "DirectoryRoleGrants");

            migrationBuilder.DropTable(
                name: "DirectorySyncItems");

            migrationBuilder.DropTable(
                name: "DirectorySyncScopes");

            migrationBuilder.DropTable(
                name: "DirectoryUserLinks");

            migrationBuilder.DropTable(
                name: "DirectorySyncRuns");
        }
    }
}
