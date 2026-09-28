using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NexusCanonicalBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIPlanRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Context = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIPlanRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredBy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CommittedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ForecastAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    FiscalQuarter = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    IsBase = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    SourceTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceTemplateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationWebhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Secret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Events = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationWebhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssueCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lookups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeetingType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MeetingLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReminderOffsetMinutes = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrganizerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Recurrence = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDigests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDigests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DigestEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DigestFrequency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Resource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanningTablePreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisibleColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnOrderJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnWidthsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnFiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CollapsedRowIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_PlanningTablePreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssemblyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryPoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalBudget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualSpend = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Health = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StrategicObjective = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBaselineVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBaselineMetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProjectBaselineVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBaselineVersions_ProjectBaselineVersions_ApprovedBaselineMetadataId",
                        column: x => x.ApprovedBaselineMetadataId,
                        principalTable: "ProjectBaselineVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkingCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PlannedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    EarnedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Health = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RagStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RiskRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BusinessCase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseCommitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    InvoicedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommitmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseCommitments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RateCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Seniority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    MonthlyRate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SkillRequirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredAllocation = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    RequiredFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimerSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_TimerSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContractValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ContractStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkingCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkingDaysPerWeek = table.Column<int>(type: "int", nullable: false),
                    HoursPerDay = table.Column<int>(type: "int", nullable: false),
                    PaidBreakMinutesPerDay = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Labels = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIPlanResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIPlanRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidenceScore = table.Column<int>(type: "int", nullable: true),
                    Suggestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIPlanResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIPlanResponses_AIPlanRequests_AIPlanRequestId",
                        column: x => x.AIPlanRequestId,
                        principalTable: "AIPlanRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalActions_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditActivities_AuditEntries_AuditEntryId",
                        column: x => x.AuditEntryId,
                        principalTable: "AuditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousApprovedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ProposedApprovedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetRevisions_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostApprovals_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    IncurredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncurredBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentReviews_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordsProcessed = table.Column<int>(type: "int", nullable: true),
                    RecordsFailed = table.Column<int>(type: "int", nullable: true),
                    ErrorLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationSyncs_IntegrationConnections_IntegrationConnectionId",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_IssueCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IssueCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MeetingActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_MeetingActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingActions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAgendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MeetingAgendas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAgendas_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Decisions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscussionPoints = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcomes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AttendanceStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MeetingParticipants", x => x.Id);
                    table.CheckConstraint("CK_MeetingParticipants_UserOrEmail", "[UserId] IS NOT NULL OR [Email] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingReminderDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
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
                    table.PrimaryKey("PK_MeetingReminderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingReminderDispatches_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadOfUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessUnits_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceBaselineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanVersions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependencyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginDependencies_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPlugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPlugins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPlugins_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    FiscalQuarter = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioBudgets_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PortfolioHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioHandoverRequests_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduleVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CostVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioHealths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioHealths_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PortfolioManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioManagerAssignments_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioRisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Likelihood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PotentialCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    MitigationPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioRisks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioRisks_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPortfolioManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshots_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualSpend = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Health = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Programs_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategicObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicObjectives_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Baselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    BaselineDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Baselines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QualityCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliverables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Milestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Impact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BudgetBeforeChange = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BudgetChangeAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Attachments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectChangeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectManagerAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllocationPercentage = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    HoursPerWeek = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalendarPolicy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_ProjectMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPhases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaselineStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BaselineEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForecastStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForecastEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDays = table.Column<int>(type: "int", nullable: true),
                    RemainingDays = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSchedules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PlannedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    EarnedValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CostVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ScheduleVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Health = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RagStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskRating = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessCase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProjectSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStakeholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Stakeholder"),
                    Interest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Influence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStakeholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStakeholders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OverallHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectLifecycleStatusAtReport = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OverallProgressPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    ScheduleHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CostHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ScopeHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QualityHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ResourceHealth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    KeyAchievements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ChallengesConcerns = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NextPeriodPriorities = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ProjectNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProjectCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProjectManagerIdAtReport = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectManagerNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreparedByNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceDataCapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrencyCodeSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BudgetAmountSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualCostAmountSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CommittedCostAmountSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ForecastCostAmountSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CostVarianceAmountSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ScheduleVarianceDaysSnapshot = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ScheduleVariancePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    BaselineIdAtReport = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaselineNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BaselineVersionSnapshot = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScheduleVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CostVariance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProjectStatusReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusReports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkBreakdownStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Deliverable = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkBreakdownStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkBreakdownStructures_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkBreakdownStructures_WorkBreakdownStructures_ParentId",
                        column: x => x.ParentId,
                        principalTable: "WorkBreakdownStructures",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeneratedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportExports_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSchedules_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Risks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Impact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Likelihood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PotentialCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PotentialDelay = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RiskResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Risks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Risks_RiskCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RiskCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    PermissionsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RolesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.PermissionsId, x.RolesId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionsId",
                        column: x => x.PermissionsId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TimerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ElapsedSeconds = table.Column<int>(type: "int", nullable: false),
                    ActualSeconds = table.Column<int>(type: "int", nullable: false),
                    BreakSeconds = table.Column<int>(type: "int", nullable: false),
                    PauseSeconds = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntrySource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_TimerSessions_TimerSessionId",
                        column: x => x.TimerSessionId,
                        principalTable: "TimerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TimerSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_TimerSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimerSegments_TimerSessions_TimerSessionId",
                        column: x => x.TimerSessionId,
                        principalTable: "TimerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkingCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_WorkingCalendars_WorkingCalendarId",
                        column: x => x.WorkingCalendarId,
                        principalTable: "WorkingCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Seniority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateOfJoining = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UtilizationTarget = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    CurrentUtilization = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Resources_WorkingCalendars_BaseCalendarId",
                        column: x => x.BaseCalendarId,
                        principalTable: "WorkingCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAssignments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_WorkItemAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommentText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemComments_WorkItemComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "WorkItemComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemComments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationSyncId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemoteValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncConflicts_IntegrationSyncs_IntegrationSyncId",
                        column: x => x.IntegrationSyncId,
                        principalTable: "IntegrationSyncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueActions_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_MeetingMinutesAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesAttachments_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MeetingMinutesChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesChecklistItems_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommentText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutesComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesComments_MeetingMinutesComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "MeetingMinutesComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesComments_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesWorkItemLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MeetingMinutesWorkItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesWorkItemLinks_MeetingMinutes_MeetingMinutesId",
                        column: x => x.MeetingMinutesId,
                        principalTable: "MeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostCentres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnnualBudget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCentres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostCentres_BusinessUnits_BusinessUnitId",
                        column: x => x.BusinessUnitId,
                        principalTable: "BusinessUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadOfDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_BusinessUnits_BusinessUnitId",
                        column: x => x.BusinessUnitId,
                        principalTable: "BusinessUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    HoursPerWeek = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PlannedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanAssignments_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanDeliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlanDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDeliverables_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Lag = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PlanDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDependencies_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CalendarKind = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: true),
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
                    table.PrimaryKey("PK_PlanningCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningCalendars_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningCalendars_PlanningCalendars_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PlanningCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanWbsNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentWbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TextFormatSpecified = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatBold = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatItalic = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatUnderline = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatTextColor = table.Column<int>(type: "int", nullable: true),
                    TextFormatHighlightColor = table.Column<int>(type: "int", nullable: true),
                    TextFormatFontSize = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PlanWbsNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanWbsNodes_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanWbsNodes_PlanWbsNodes_ParentWbsId",
                        column: x => x.ParentWbsId,
                        principalTable: "PlanWbsNodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProgramHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProgramHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramHandoverRequests_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    HandoverFromProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProgramManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramManagerAssignments_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProgramManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProgramSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramSnapshots_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPlanDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBaselineVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProjectPlanDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_ProjectBaselineVersions_SourceBaselineVersionId",
                        column: x => x.SourceBaselineVersionId,
                        principalTable: "ProjectBaselineVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_ProjectChangeRequests_SubmittedChangeRequestId",
                        column: x => x.SubmittedChangeRequestId,
                        principalTable: "ProjectChangeRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMemberRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProjectMemberRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMemberRoles_ProjectMembers_ProjectMemberId",
                        column: x => x.ProjectMemberId,
                        principalTable: "ProjectMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHandoverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProjectHandoverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverRequests_ProjectSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "ProjectSnapshots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectHandoverRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StakeholderInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectStakeholderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakeholderInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StakeholderInvitations_ProjectStakeholders_ProjectStakeholderId",
                        column: x => x.ProjectStakeholderId,
                        principalTable: "ProjectStakeholders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StakeholderInvitations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusReportChangeRequestSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectStatusReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Impact = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ChangeRequestStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectStatusReportChangeRequestSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusReportChangeRequestSnapshots_ProjectStatusReports_ProjectStatusReportId",
                        column: x => x.ProjectStatusReportId,
                        principalTable: "ProjectStatusReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusReportIssueSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectStatusReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssueStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectStatusReportIssueSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusReportIssueSnapshots_ProjectStatusReports_ProjectStatusReportId",
                        column: x => x.ProjectStatusReportId,
                        principalTable: "ProjectStatusReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusReportRiskSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectStatusReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Impact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Likelihood = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RiskStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PotentialCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PotentialDelay = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RiskResponse = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectStatusReportRiskSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusReportRiskSnapshots_ProjectStatusReports_ProjectStatusReportId",
                        column: x => x.ProjectStatusReportId,
                        principalTable: "ProjectStatusReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OwnerResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RiskActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskActions_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskContingencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskContingencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskContingencies_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskImpactAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
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
                    table.PrimaryKey("PK_RiskImpactAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskImpactAssessments_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskMitigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskMitigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskMitigations_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocationPercentage = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    HoursPerWeek = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PlannedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AllocationSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    SourceReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ResourceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceAllocations_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DayType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AvailableHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCalendars_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceCostProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompensationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OvertimeEligible = table.Column<bool>(type: "bit", nullable: false),
                    OvertimeRateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OvertimeRateValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_ResourceCostProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCostProfiles_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Proficiency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceSkills_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TeamLeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarDayRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanningCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
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
                    table.PrimaryKey("PK_CalendarDayRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarDayRules_PlanningCalendars_PlanningCalendarId",
                        column: x => x.PlanningCalendarId,
                        principalTable: "PlanningCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanningCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CalendarHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarHolidays_PlanningCalendars_PlanningCalendarId",
                        column: x => x.PlanningCalendarId,
                        principalTable: "PlanningCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SchedulingMode = table.Column<int>(type: "int", nullable: false),
                    ConstraintType = table.Column<int>(type: "int", nullable: false),
                    ConstraintDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualFinish = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EstimateHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    WbsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TextFormatSpecified = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatBold = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatItalic = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatUnderline = table.Column<bool>(type: "bit", nullable: true),
                    TextFormatTextColor = table.Column<int>(type: "int", nullable: true),
                    TextFormatHighlightColor = table.Column<int>(type: "int", nullable: true),
                    TextFormatFontSize = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PlanActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanActivities_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanActivities_PlanningCalendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "PlanningCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskActionUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdateText = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangeDetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true),
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
                    table.PrimaryKey("PK_RiskActionUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskActionUpdates_RiskActions_RiskActionId",
                        column: x => x.RiskActionId,
                        principalTable: "RiskActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskImpactAssessmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskImpactAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_RiskImpactAssessmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskImpactAssessmentTypes_RiskImpactAssessments_RiskImpactAssessmentId",
                        column: x => x.RiskImpactAssessmentId,
                        principalTable: "RiskImpactAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivePublishedVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangeLog = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_WorkflowDefinitionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionVersions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowStates", x => x.Id);
                    table.UniqueConstraint("AK_WorkflowStates_WorkflowDefinitionVersionId_Id", x => new { x.WorkflowDefinitionVersionId, x.Id });
                    table.ForeignKey(
                        name: "FK_WorkflowStates_WorkflowDefinitionVersions_WorkflowDefinitionVersionId",
                        column: x => x.WorkflowDefinitionVersionId,
                        principalTable: "WorkflowDefinitionVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowDefinitionVersions_WorkflowDefinitionVersionId",
                        column: x => x.WorkflowDefinitionVersionId,
                        principalTable: "WorkflowDefinitionVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowStates_WorkflowDefinitionVersionId_CurrentStateId",
                        columns: x => new { x.WorkflowDefinitionVersionId, x.CurrentStateId },
                        principalTable: "WorkflowStates",
                        principalColumns: new[] { "WorkflowDefinitionVersionId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowDefinitionVersions_WorkflowDefinitionVersionId",
                        column: x => x.WorkflowDefinitionVersionId,
                        principalTable: "WorkflowDefinitionVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStates_WorkflowDefinitionVersionId_FromStateId",
                        columns: x => new { x.WorkflowDefinitionVersionId, x.FromStateId },
                        principalTable: "WorkflowStates",
                        principalColumns: new[] { "WorkflowDefinitionVersionId", "Id" });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStates_WorkflowDefinitionVersionId_ToStateId",
                        columns: x => new { x.WorkflowDefinitionVersionId, x.ToStateId },
                        principalTable: "WorkflowStates",
                        principalColumns: new[] { "WorkflowDefinitionVersionId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowTransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowTransitionActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionActions_WorkflowTransitions_WorkflowTransitionId",
                        column: x => x.WorkflowTransitionId,
                        principalTable: "WorkflowTransitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionGuards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowTransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuardType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowTransitionGuards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionGuards_WorkflowTransitions_WorkflowTransitionId",
                        column: x => x.WorkflowTransitionId,
                        principalTable: "WorkflowTransitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowTransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExecutedByNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionHistory_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionHistory_WorkflowStates_FromStateId",
                        column: x => x.FromStateId,
                        principalTable: "WorkflowStates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionHistory_WorkflowStates_ToStateId",
                        column: x => x.ToStateId,
                        principalTable: "WorkflowStates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionHistory_WorkflowTransitions_WorkflowTransitionId",
                        column: x => x.WorkflowTransitionId,
                        principalTable: "WorkflowTransitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIPlanResponses_AIPlanRequestId",
                table: "AIPlanResponses",
                column: "AIPlanRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalActions_ApprovalRequestId",
                table: "ApprovalActions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditActivities_AuditEntryId",
                table: "AuditActivities",
                column: "AuditEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Baselines_ProjectId",
                table: "Baselines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRevisions_BudgetId",
                table: "BudgetRevisions",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUnits_OrganizationId",
                table: "BusinessUnits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarDayRules_PlanningCalendarId",
                table: "CalendarDayRules",
                column: "PlanningCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarDayRules_Tenant_Calendar_Day",
                table: "CalendarDayRules",
                columns: new[] { "TenantId", "PlanningCalendarId", "DayOfWeek" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarHolidays_PlanningCalendarId",
                table: "CalendarHolidays",
                column: "PlanningCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarHolidays_Tenant_Calendar_Date",
                table: "CalendarHolidays",
                columns: new[] { "TenantId", "PlanningCalendarId", "Date" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ResourceId",
                table: "Contracts",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenantId",
                table: "Contracts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostApprovals_BudgetId",
                table: "CostApprovals",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCentres_BusinessUnitId",
                table: "CostCentres",
                column: "BusinessUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_BudgetId",
                table: "CostEntries",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ProjectId",
                table: "Deliverables",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_BusinessUnitId",
                table: "Departments",
                column: "BusinessUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviews_DocumentId",
                table: "DocumentReviews",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CategoryId",
                table: "Documents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceTemplateId",
                table: "Documents",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId",
                table: "DocumentVersions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_WorkingCalendarId",
                table: "Holidays",
                column: "WorkingCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSyncs_IntegrationConnectionId",
                table: "IntegrationSyncs",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueActions_IssueId",
                table: "IssueActions",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_CategoryId",
                table: "Issues",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_TenantId_Category_Code",
                table: "Lookups",
                columns: new[] { "TenantId", "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_TenantId_Category_IsActive_SortOrder",
                table: "Lookups",
                columns: new[] { "TenantId", "Category", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActions_MeetingId",
                table: "MeetingActions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAgendas_MeetingId",
                table: "MeetingAgendas",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_MeetingId",
                table: "MeetingMinutes",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesAttachments_MeetingMinutesId",
                table: "MeetingMinutesAttachments",
                column: "MeetingMinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesChecklistItems_MeetingMinutesId_SortOrder",
                table: "MeetingMinutesChecklistItems",
                columns: new[] { "MeetingMinutesId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesComments_MeetingMinutesId_CreatedAt",
                table: "MeetingMinutesComments",
                columns: new[] { "MeetingMinutesId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesComments_ParentCommentId",
                table: "MeetingMinutesComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesWorkItemLinks_MeetingMinutesId_WorkItemId",
                table: "MeetingMinutesWorkItemLinks",
                columns: new[] { "MeetingMinutesId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId",
                table: "MeetingParticipants",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId_Email",
                table: "MeetingParticipants",
                columns: new[] { "MeetingId", "Email" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId_UserId",
                table: "MeetingParticipants",
                columns: new[] { "MeetingId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingReminderDispatches_MeetingId_ScheduledAtUtc",
                table: "MeetingReminderDispatches",
                columns: new[] { "MeetingId", "ScheduledAtUtc" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_StartTime_ReminderOffsetMinutes",
                table: "Meetings",
                columns: new[] { "StartTime", "ReminderOffsetMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_ProjectId",
                table: "Milestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Resource_Action",
                table: "Permissions",
                columns: new[] { "Resource", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_CalendarId",
                table: "PlanActivities",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_PlanVersionId",
                table: "PlanActivities",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_Tenant_Version_Code",
                table: "PlanActivities",
                columns: new[] { "TenantId", "PlanVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanActivities_Tenant_Version_SourceActivity",
                table: "PlanActivities",
                columns: new[] { "TenantId", "PlanVersionId", "SourceActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_PlanVersionId",
                table: "PlanAssignments",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_ResourceAllocationId",
                table: "PlanAssignments",
                column: "ResourceAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanDeliverables_PlanVersionId",
                table: "PlanDeliverables",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDeliverables_Tenant_Version_Name",
                table: "PlanDeliverables",
                columns: new[] { "TenantId", "PlanVersionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanDependencies_PlanVersionId",
                table: "PlanDependencies",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanDependencies_Tenant_Version_Edge",
                table: "PlanDependencies",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "PredecessorActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanCalendars_Tenant_Version_Default",
                table: "PlanningCalendars",
                columns: new[] { "TenantId", "PlanVersionId", "IsDefault" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PlanCalendars_Tenant_Version_Name",
                table: "PlanningCalendars",
                columns: new[] { "TenantId", "PlanVersionId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningCalendars_PlanVersionId",
                table: "PlanningCalendars",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningCalendars_TemplateId",
                table: "PlanningCalendars",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningTablePreferences_TenantId_UserId_PlanId",
                table: "PlanningTablePreferences",
                columns: new[] { "TenantId", "UserId", "PlanId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Tenant_Project_Type",
                table: "Plans",
                columns: new[] { "TenantId", "ProjectId", "PlanType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_PlanId",
                table: "PlanVersions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_ActiveWorking",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "Type" },
                unique: true,
                filter: "\"Type\" = 0 AND \"Status\" = 0 AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_ApprovedBaseline",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "Status" },
                filter: "\"Type\" = 1 AND \"Status\" = 3 AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersions_Tenant_Plan_Version",
                table: "PlanVersions",
                columns: new[] { "TenantId", "PlanId", "VersionNumber", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_ParentWbsId",
                table: "PlanWbsNodes",
                column: "ParentWbsId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_PlanVersionId",
                table: "PlanWbsNodes",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanWbsNodes_Tenant_Version_Parent_Code",
                table: "PlanWbsNodes",
                columns: new[] { "TenantId", "PlanVersionId", "ParentWbsId", "Code" },
                unique: true,
                filter: "[ParentWbsId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PluginDependencies_PluginId",
                table: "PluginDependencies",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioBudgets_PortfolioId",
                table: "PortfolioBudgets",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_PortfolioId",
                table: "PortfolioHandoverRequests",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_TenantId",
                table: "PortfolioHandoverRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHandoverRequests_TenantId_PortfolioId_Status",
                table: "PortfolioHandoverRequests",
                columns: new[] { "TenantId", "PortfolioId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHealths_PortfolioId",
                table: "PortfolioHealths",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_PortfolioId",
                table: "PortfolioManagerAssignments",
                column: "PortfolioId",
                unique: true,
                filter: "[IsCurrent] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_PortfolioManagerId",
                table: "PortfolioManagerAssignments",
                column: "PortfolioManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioManagerAssignments_TenantId",
                table: "PortfolioManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioRisks_PortfolioId",
                table: "PortfolioRisks",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_TenantId_Code",
                table: "Portfolios",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_TenantId_Status",
                table: "Portfolios",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_PortfolioId",
                table: "PortfolioSnapshots",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_TenantId",
                table: "PortfolioSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_TenantId_PortfolioId_SnapshotNumber",
                table: "PortfolioSnapshots",
                columns: new[] { "TenantId", "PortfolioId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_ProgramId",
                table: "ProgramHandoverRequests",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_TenantId",
                table: "ProgramHandoverRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramHandoverRequests_TenantId_ProgramId_Status",
                table: "ProgramHandoverRequests",
                columns: new[] { "TenantId", "ProgramId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_ProgramId",
                table: "ProgramManagerAssignments",
                column: "ProgramId",
                unique: true,
                filter: "[IsCurrent] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_ProgramManagerId",
                table: "ProgramManagerAssignments",
                column: "ProgramManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramManagerAssignments_TenantId",
                table: "ProgramManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_PortfolioId",
                table: "Programs",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_ProgramId",
                table: "ProgramSnapshots",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_TenantId",
                table: "ProgramSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSnapshots_TenantId_ProgramId_SnapshotNumber",
                table: "ProgramSnapshots",
                columns: new[] { "TenantId", "ProgramId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselineVersions_ApprovedBaselineMetadataId",
                table: "ProjectBaselineVersions",
                column: "ApprovedBaselineMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Project_CurrentApproved",
                table: "ProjectBaselineVersions",
                columns: new[] { "TenantId", "ProjectId", "IsCurrent", "Status" },
                filter: "\"IsCurrent\" = 1 AND \"Status\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Project_Version",
                table: "ProjectBaselineVersions",
                columns: new[] { "TenantId", "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChangeRequests_ProjectId",
                table: "ProjectChangeRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_NewProjectManagerId",
                table: "ProjectHandoverRequests",
                column: "NewProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_ProjectId",
                table: "ProjectHandoverRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_SnapshotId",
                table: "ProjectHandoverRequests",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_Status",
                table: "ProjectHandoverRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_TenantId",
                table: "ProjectHandoverRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_ProjectId",
                table: "ProjectManagerAssignments",
                column: "ProjectId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_ProjectManagerId",
                table: "ProjectManagerAssignments",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagerAssignments_TenantId",
                table: "ProjectManagerAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemberRoles_ProjectMemberId",
                table: "ProjectMemberRoles",
                column: "ProjectMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemberRoles_TenantId",
                table: "ProjectMemberRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemberRoles_TenantId_ProjectMemberId_ProjectRoleId",
                table: "ProjectMemberRoles",
                columns: new[] { "TenantId", "ProjectMemberId", "ProjectRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId",
                table: "ProjectMembers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_ResourceId_Status",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "ResourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ResourceId",
                table: "ProjectMembers",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_TenantId",
                table: "ProjectMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhases_ProjectId",
                table: "ProjectPhases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_ProjectId",
                table: "ProjectPlanDrafts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_SourceBaselineVersionId",
                table: "ProjectPlanDrafts",
                column: "SourceBaselineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_SubmittedChangeRequestId",
                table: "ProjectPlanDrafts",
                column: "SubmittedChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_Tenant_Project_Active",
                table: "ProjectPlanDrafts",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "\"IsActive\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectManagerId",
                table: "Projects",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkingCalendarId",
                table: "Projects",
                column: "WorkingCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSchedules_ProjectId",
                table: "ProjectSchedules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_ProjectId",
                table: "ProjectSnapshots",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_SnapshotNumber",
                table: "ProjectSnapshots",
                column: "SnapshotNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_TenantId",
                table: "ProjectSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStakeholders_ProjectId",
                table: "ProjectStakeholders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStakeholders_TenantId_ProjectId_NormalizedEmail",
                table: "ProjectStakeholders",
                columns: new[] { "TenantId", "ProjectId", "NormalizedEmail" },
                filter: "[IsDeleted] = 0 AND [Status] = 'ContactOnly' AND [NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStakeholders_TenantId_ProjectId_UserId",
                table: "ProjectStakeholders",
                columns: new[] { "TenantId", "ProjectId", "UserId" },
                filter: "[IsDeleted] = 0 AND [Status] = 'Active' AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportChangeRequestSnapshots_ProjectStatusReportId",
                table: "ProjectStatusReportChangeRequestSnapshots",
                column: "ProjectStatusReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportChangeRequestSnapshots_TenantId_ProjectStatusReportId_Rank",
                table: "ProjectStatusReportChangeRequestSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportChangeRequestSnapshots_TenantId_ProjectStatusReportId_SourceChangeRequestId",
                table: "ProjectStatusReportChangeRequestSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "SourceChangeRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportIssueSnapshots_ProjectStatusReportId",
                table: "ProjectStatusReportIssueSnapshots",
                column: "ProjectStatusReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportIssueSnapshots_TenantId_ProjectStatusReportId_Rank",
                table: "ProjectStatusReportIssueSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportIssueSnapshots_TenantId_ProjectStatusReportId_SourceIssueId",
                table: "ProjectStatusReportIssueSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "SourceIssueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportRiskSnapshots_ProjectStatusReportId",
                table: "ProjectStatusReportRiskSnapshots",
                column: "ProjectStatusReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportRiskSnapshots_TenantId_ProjectStatusReportId_Rank",
                table: "ProjectStatusReportRiskSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReportRiskSnapshots_TenantId_ProjectStatusReportId_SourceRiskId",
                table: "ProjectStatusReportRiskSnapshots",
                columns: new[] { "TenantId", "ProjectStatusReportId", "SourceRiskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReports_ProjectId",
                table: "ProjectStatusReports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReports_TenantId_ProjectId_ReportDate",
                table: "ProjectStatusReports",
                columns: new[] { "TenantId", "ProjectId", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusReports_TenantId_ProjectId_ReportState",
                table: "ProjectStatusReports",
                columns: new[] { "TenantId", "ProjectId", "ReportState" });

            migrationBuilder.CreateIndex(
                name: "IX_RateCards_TenantId",
                table: "RateCards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExports_ReportDefinitionId",
                table: "ReportExports",
                column: "ReportDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_ReportDefinitionId",
                table: "ReportSchedules",
                column: "ReportDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_AllocationSource_SourceReferenceId",
                table: "ResourceAllocations",
                columns: new[] { "AllocationSource", "SourceReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_ProjectId",
                table: "ResourceAllocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_ResourceId",
                table: "ResourceAllocations",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_ResourceId_StartDate_EndDate",
                table: "ResourceAllocations",
                columns: new[] { "ResourceId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_Status",
                table: "ResourceAllocations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_TenantId",
                table: "ResourceAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_WorkItemId",
                table: "ResourceAllocations",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendars_ResourceId_Date",
                table: "ResourceCalendars",
                columns: new[] { "ResourceId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendars_TenantId",
                table: "ResourceCalendars",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCostProfiles_ResourceId",
                table: "ResourceCostProfiles",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCostProfiles_ResourceId_EffectiveFrom",
                table: "ResourceCostProfiles",
                columns: new[] { "ResourceId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCostProfiles_TenantId",
                table: "ResourceCostProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequests_TenantId",
                table: "ResourceRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_BaseCalendarId",
                table: "Resources",
                column: "BaseCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Email",
                table: "Resources",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_EmployeeId",
                table: "Resources",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Status",
                table: "Resources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_TenantId",
                table: "Resources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Type",
                table: "Resources",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_VendorId",
                table: "Resources",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSkills_ResourceId_SkillId",
                table: "ResourceSkills",
                columns: new[] { "ResourceId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSkills_SkillId",
                table: "ResourceSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSkills_TenantId",
                table: "ResourceSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_RiskId",
                table: "RiskActions",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_TenantId_RiskId_SortOrder",
                table: "RiskActions",
                columns: new[] { "TenantId", "RiskId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionUpdates_RiskActionId",
                table: "RiskActionUpdates",
                column: "RiskActionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionUpdates_TenantId_RiskActionId_CreatedAt",
                table: "RiskActionUpdates",
                columns: new[] { "TenantId", "RiskActionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskContingencies_RiskId",
                table: "RiskContingencies",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactAssessments_RiskId",
                table: "RiskImpactAssessments",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactAssessments_TenantId_RiskId",
                table: "RiskImpactAssessments",
                columns: new[] { "TenantId", "RiskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactAssessmentTypes_RiskImpactAssessmentId",
                table: "RiskImpactAssessmentTypes",
                column: "RiskImpactAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactAssessmentTypes_TenantId_RiskImpactAssessmentId_ImpactType",
                table: "RiskImpactAssessmentTypes",
                columns: new[] { "TenantId", "RiskImpactAssessmentId", "ImpactType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskMitigations_RiskId",
                table: "RiskMitigations",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_CategoryId",
                table: "Risks",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_OwnerResourceId",
                table: "Risks",
                columns: new[] { "TenantId", "OwnerResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_ProjectId",
                table: "Risks",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_Status",
                table: "Risks",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RolesId",
                table: "RolePermissions",
                column: "RolesId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TenantId",
                table: "Skills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderInvitations_ProjectId",
                table: "StakeholderInvitations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderInvitations_ProjectStakeholderId",
                table: "StakeholderInvitations",
                column: "ProjectStakeholderId");

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderInvitations_TenantId_ResourceId_ProjectId_Status",
                table: "StakeholderInvitations",
                columns: new[] { "TenantId", "ResourceId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StakeholderInvitations_TokenHash",
                table: "StakeholderInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_PortfolioId",
                table: "StrategicObjectives",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_IntegrationSyncId",
                table: "SyncConflicts",
                column: "IntegrationSyncId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPlugins_PluginId",
                table: "TenantPlugins",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_Activity_WorkDate",
                table: "TimeEntries",
                columns: new[] { "TenantId", "PlanActivityId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_Project_WorkDate",
                table: "TimeEntries",
                columns: new[] { "TenantId", "ProjectId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_Resource_WorkDate",
                table: "TimeEntries",
                columns: new[] { "TenantId", "ResourceId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_Role_WorkDate",
                table: "TimeEntries",
                columns: new[] { "TenantId", "ProjectRoleId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_TimerSession",
                table: "TimeEntries",
                columns: new[] { "TenantId", "TimerSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_Tenant_WorkItem_WorkDate",
                table: "TimeEntries",
                columns: new[] { "TenantId", "WorkItemId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_TimerSessionId",
                table: "TimeEntries",
                column: "TimerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimerSegments_Tenant_Session_StartedAt",
                table: "TimerSegments",
                columns: new[] { "TenantId", "TimerSessionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TimerSegments_TimerSessionId",
                table: "TimerSegments",
                column: "TimerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimerSessions_Tenant_User_Status",
                table: "TimerSessions",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_TimerSessions_Active_Resource",
                table: "TimerSessions",
                columns: new[] { "TenantId", "ResourceId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Running', 'OnBreak', 'Paused', 'IdlePaused')");

            migrationBuilder.CreateIndex(
                name: "UX_TimerSessions_Active_User",
                table: "TimerSessions",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Running', 'OnBreak', 'Paused', 'IdlePaused')");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Name",
                table: "Vendors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_TenantId",
                table: "Vendors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkBreakdownStructures_ParentId",
                table: "WorkBreakdownStructures",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkBreakdownStructures_ProjectId",
                table: "WorkBreakdownStructures",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_ActivePublishedVersionId",
                table: "WorkflowDefinitions",
                column: "ActivePublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_Code",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_EntityType_IsActive",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "EntityType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_TenantId_WorkflowDefinitionId_LifecycleStatus",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "TenantId", "WorkflowDefinitionId", "LifecycleStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_TenantId_WorkflowDefinitionId_VersionNumber",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "TenantId", "WorkflowDefinitionId", "VersionNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_WorkflowDefinitionId",
                table: "WorkflowDefinitionVersions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_TenantId_EntityType_EntityId_WorkflowDefinitionId",
                table: "WorkflowInstances",
                columns: new[] { "TenantId", "EntityType", "EntityId", "WorkflowDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_TenantId_EntityType_EntityId_WorkflowDefinitionId_IsActive",
                table: "WorkflowInstances",
                columns: new[] { "TenantId", "EntityType", "EntityId", "WorkflowDefinitionId", "IsActive" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionId",
                table: "WorkflowInstances",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionVersionId_CurrentStateId",
                table: "WorkflowInstances",
                columns: new[] { "WorkflowDefinitionVersionId", "CurrentStateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_TenantId_WorkflowDefinitionVersionId_Code",
                table: "WorkflowStates",
                columns: new[] { "TenantId", "WorkflowDefinitionVersionId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionActions_TenantId_WorkflowTransitionId_ExecutionOrder",
                table: "WorkflowTransitionActions",
                columns: new[] { "TenantId", "WorkflowTransitionId", "ExecutionOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionActions_WorkflowTransitionId",
                table: "WorkflowTransitionActions",
                column: "WorkflowTransitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionGuards_TenantId_WorkflowTransitionId_ExecutionOrder",
                table: "WorkflowTransitionGuards",
                columns: new[] { "TenantId", "WorkflowTransitionId", "ExecutionOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionGuards_WorkflowTransitionId",
                table: "WorkflowTransitionGuards",
                column: "WorkflowTransitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionHistory_FromStateId",
                table: "WorkflowTransitionHistory",
                column: "FromStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionHistory_TenantId_WorkflowInstanceId_ExecutedAt",
                table: "WorkflowTransitionHistory",
                columns: new[] { "TenantId", "WorkflowInstanceId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionHistory_ToStateId",
                table: "WorkflowTransitionHistory",
                column: "ToStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionHistory_WorkflowInstanceId",
                table: "WorkflowTransitionHistory",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionHistory_WorkflowTransitionId",
                table: "WorkflowTransitionHistory",
                column: "WorkflowTransitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TenantId_WorkflowDefinitionVersionId_Code",
                table: "WorkflowTransitions",
                columns: new[] { "TenantId", "WorkflowDefinitionVersionId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TenantId_WorkflowDefinitionVersionId_FromStateId_ToStateId",
                table: "WorkflowTransitions",
                columns: new[] { "TenantId", "WorkflowDefinitionVersionId", "FromStateId", "ToStateId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionVersionId_FromStateId",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowDefinitionVersionId", "FromStateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionVersionId_ToStateId",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowDefinitionVersionId", "ToStateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAssignments_WorkItemId",
                table: "WorkItemAssignments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_WorkItemId_CreatedAt",
                table: "WorkItemAttachments",
                columns: new[] { "WorkItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemComments_ParentCommentId",
                table: "WorkItemComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemComments_WorkItemId_CreatedAt",
                table: "WorkItemComments",
                columns: new[] { "WorkItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId",
                table: "WorkItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Status_ProjectId",
                table: "WorkItems",
                columns: new[] { "Status", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowDefinitionVersions_ActivePublishedVersionId",
                table: "WorkflowDefinitions",
                column: "ActivePublishedVersionId",
                principalTable: "WorkflowDefinitionVersions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowDefinitionVersions_ActivePublishedVersionId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "AIPlanResponses");

            migrationBuilder.DropTable(
                name: "ApprovalActions");

            migrationBuilder.DropTable(
                name: "AuditActivities");

            migrationBuilder.DropTable(
                name: "Baselines");

            migrationBuilder.DropTable(
                name: "BudgetRevisions");

            migrationBuilder.DropTable(
                name: "CalendarDayRules");

            migrationBuilder.DropTable(
                name: "CalendarHolidays");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "CostApprovals");

            migrationBuilder.DropTable(
                name: "CostCategories");

            migrationBuilder.DropTable(
                name: "CostCentres");

            migrationBuilder.DropTable(
                name: "CostEntries");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "Delegations");

            migrationBuilder.DropTable(
                name: "Deliverables");

            migrationBuilder.DropTable(
                name: "DocumentCategories");

            migrationBuilder.DropTable(
                name: "DocumentReviews");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "IntegrationWebhooks");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "IssueActions");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Lookups");

            migrationBuilder.DropTable(
                name: "MeetingActions");

            migrationBuilder.DropTable(
                name: "MeetingAgendas");

            migrationBuilder.DropTable(
                name: "MeetingMinutesAttachments");

            migrationBuilder.DropTable(
                name: "MeetingMinutesChecklistItems");

            migrationBuilder.DropTable(
                name: "MeetingMinutesComments");

            migrationBuilder.DropTable(
                name: "MeetingMinutesWorkItemLinks");

            migrationBuilder.DropTable(
                name: "MeetingParticipants");

            migrationBuilder.DropTable(
                name: "MeetingReminderDispatches");

            migrationBuilder.DropTable(
                name: "Milestones");

            migrationBuilder.DropTable(
                name: "NotificationDigests");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "PlanActivities");

            migrationBuilder.DropTable(
                name: "PlanAssignments");

            migrationBuilder.DropTable(
                name: "PlanDeliverables");

            migrationBuilder.DropTable(
                name: "PlanDependencies");

            migrationBuilder.DropTable(
                name: "PlanningTablePreferences");

            migrationBuilder.DropTable(
                name: "PlanWbsNodes");

            migrationBuilder.DropTable(
                name: "PluginDependencies");

            migrationBuilder.DropTable(
                name: "PortfolioBudgets");

            migrationBuilder.DropTable(
                name: "PortfolioHandoverRequests");

            migrationBuilder.DropTable(
                name: "PortfolioHealths");

            migrationBuilder.DropTable(
                name: "PortfolioManagerAssignments");

            migrationBuilder.DropTable(
                name: "PortfolioRisks");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "ProgramHandoverRequests");

            migrationBuilder.DropTable(
                name: "ProgramManagerAssignments");

            migrationBuilder.DropTable(
                name: "ProgramSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectHandoverRequests");

            migrationBuilder.DropTable(
                name: "ProjectManagerAssignments");

            migrationBuilder.DropTable(
                name: "ProjectMemberRoles");

            migrationBuilder.DropTable(
                name: "ProjectPhases");

            migrationBuilder.DropTable(
                name: "ProjectPlanDrafts");

            migrationBuilder.DropTable(
                name: "ProjectSchedules");

            migrationBuilder.DropTable(
                name: "ProjectStatusReportChangeRequestSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectStatusReportIssueSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectStatusReportRiskSnapshots");

            migrationBuilder.DropTable(
                name: "PurchaseCommitments");

            migrationBuilder.DropTable(
                name: "RateCards");

            migrationBuilder.DropTable(
                name: "ReportExports");

            migrationBuilder.DropTable(
                name: "ReportSchedules");

            migrationBuilder.DropTable(
                name: "ResourceAllocations");

            migrationBuilder.DropTable(
                name: "ResourceCalendars");

            migrationBuilder.DropTable(
                name: "ResourceCostProfiles");

            migrationBuilder.DropTable(
                name: "ResourceRequests");

            migrationBuilder.DropTable(
                name: "ResourceSkills");

            migrationBuilder.DropTable(
                name: "RiskActionUpdates");

            migrationBuilder.DropTable(
                name: "RiskContingencies");

            migrationBuilder.DropTable(
                name: "RiskImpactAssessmentTypes");

            migrationBuilder.DropTable(
                name: "RiskMitigations");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "StakeholderInvitations");

            migrationBuilder.DropTable(
                name: "StrategicObjectives");

            migrationBuilder.DropTable(
                name: "SyncConflicts");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "TenantPlugins");

            migrationBuilder.DropTable(
                name: "TimeEntries");

            migrationBuilder.DropTable(
                name: "TimerSegments");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "WorkBreakdownStructures");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionActions");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionGuards");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionHistory");

            migrationBuilder.DropTable(
                name: "WorkItemAssignments");

            migrationBuilder.DropTable(
                name: "WorkItemAttachments");

            migrationBuilder.DropTable(
                name: "WorkItemComments");

            migrationBuilder.DropTable(
                name: "AIPlanRequests");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "MeetingMinutes");

            migrationBuilder.DropTable(
                name: "PlanningCalendars");

            migrationBuilder.DropTable(
                name: "Programs");

            migrationBuilder.DropTable(
                name: "ProjectSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "ProjectBaselineVersions");

            migrationBuilder.DropTable(
                name: "ProjectChangeRequests");

            migrationBuilder.DropTable(
                name: "ProjectStatusReports");

            migrationBuilder.DropTable(
                name: "ReportDefinitions");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "RiskActions");

            migrationBuilder.DropTable(
                name: "RiskImpactAssessments");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "ProjectStakeholders");

            migrationBuilder.DropTable(
                name: "IntegrationSyncs");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Plugins");

            migrationBuilder.DropTable(
                name: "TimerSessions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "IssueCategories");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "PlanVersions");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "WorkingCalendars");

            migrationBuilder.DropTable(
                name: "Risks");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "IntegrationConnections");

            migrationBuilder.DropTable(
                name: "BusinessUnits");

            migrationBuilder.DropTable(
                name: "WorkflowStates");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "RiskCategories");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionVersions");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");
        }
    }
}
