using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using Moq;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class OrganizationResponseSerializationTests : IDisposable
{
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private readonly OrganizationTestDbContext _context;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Organization _organizationA;
    private readonly Organization _organizationA2;
    private readonly Organization _organizationB;
    private readonly BusinessUnit _businessUnitA1;
    private readonly Department _departmentA1;
    private readonly Team _teamA1;

    public OrganizationResponseSerializationTests()
    {
        _connection.Open();
        _context = new OrganizationTestDbContext(
            new DbContextOptionsBuilder<OrganizationTestDbContext>().UseSqlite(_connection).Options,
            _httpContextAccessor);
        SetTenant(_tenantA);
        _context.Database.EnsureCreated();

        _organizationA = OrganizationFor(_tenantA, "A Organization", "A-ORG");
        _organizationA2 = OrganizationFor(_tenantA, "B Organization", "B-ORG");
        _organizationB = OrganizationFor(_tenantB, "Foreign Organization", "X-ORG");

        _businessUnitA1 = BusinessUnitFor(_tenantA, _organizationA, "Technology", "TECH");
        var businessUnitA2 = BusinessUnitFor(_tenantA, _organizationA, "Operations", "OPS");
        var businessUnitA3 = BusinessUnitFor(_tenantA, _organizationA2, "Finance", "FIN");
        var businessUnitB = BusinessUnitFor(_tenantB, _organizationB, "Foreign Unit", "X-BU");
        _departmentA1 = DepartmentFor(_tenantA, _businessUnitA1, "Engineering", "ENG");
        var departmentA2 = DepartmentFor(_tenantA, _businessUnitA1, "Product", "PROD");
        _teamA1 = TeamFor(_tenantA, _departmentA1, "Platform", "PLATFORM");
        _ = TeamFor(_tenantA, _departmentA1, "Applications", "APPS");
        _ = DepartmentFor(_tenantA, businessUnitA2, "Operations Support", "OPS-SUP");
        _ = DepartmentFor(_tenantA, businessUnitA3, "Accounting", "ACCT");
        _ = DepartmentFor(_tenantB, businessUnitB, "Foreign Department", "X-DEPT");

        _context.Organizations.AddRange(_organizationA, _organizationA2, _organizationB);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Paged_Organizations_Serialize_With_Bounded_Business_Units_And_Stable_Paging()
    {
        var firstPage = await CreateOrganizationService().GetPagedAsync(1, 1);
        var secondPage = await CreateOrganizationService().GetPagedAsync(2, 1);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Single(firstPage.Items);
        Assert.Single(secondPage.Items);
        Assert.Equal(_organizationA.Id, firstPage.Items[0].Id);
        Assert.Equal(_organizationA2.Id, secondPage.Items[0].Id);
        Assert.Equal(2, firstPage.Items[0].BusinessUnits.Count);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(firstPage, ApiJsonOptions));
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal("A Organization", root.GetProperty("items")[0].GetProperty("name").GetString());
        var businessUnit = root.GetProperty("items")[0].GetProperty("businessUnits")[0];
        Assert.Equal(_organizationA.Id, businessUnit.GetProperty("organizationId").GetGuid());
        Assert.Equal(0, businessUnit.GetProperty("departments").GetArrayLength());
        Assert.False(businessUnit.TryGetProperty("organization", out _));
    }

    [Fact]
    public async Task Organization_Detail_Serializes_Departments_And_Teams_Without_Back_References()
    {
        var result = await CreateOrganizationService().GetByIdAsync(_organizationA.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var json = JsonSerializer.Serialize(result.Value, ApiJsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(_organizationA.Id, root.GetProperty("id").GetGuid());
        Assert.Equal(2, root.GetProperty("businessUnits").GetArrayLength());
        var businessUnit = root.GetProperty("businessUnits")[1];
        Assert.False(businessUnit.TryGetProperty("organization", out _));
        var department = businessUnit.GetProperty("departments")[0];
        Assert.False(department.TryGetProperty("businessUnit", out _));
        var team = department.GetProperty("teams")[0];
        Assert.False(team.TryGetProperty("department", out _));
        Assert.Equal(0, team.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Empty_And_Cross_Tenant_Organization_Reads_Are_Safe()
    {
        SetTenant(Guid.NewGuid());
        var emptyPage = await CreateOrganizationService().GetPagedAsync(1, 100);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(0, emptyPage.TotalCount);
        Assert.NotEmpty(JsonSerializer.Serialize(emptyPage, ApiJsonOptions));

        SetTenant(_tenantA);
        var foreign = await CreateOrganizationService().GetByIdAsync(_organizationB.Id);
        Assert.True(foreign.IsFailure);
    }

    [Fact]
    public async Task Business_Unit_And_Department_Reads_Return_Finite_Dtos()
    {
        var businessUnitService = new BusinessUnitService(_context);
        var departmentService = new DepartmentService(_context);
        var businessUnit = await businessUnitService.GetByIdAsync(_businessUnitA1.Id);
        var departments = await departmentService.GetPagedAsync(1, 200);

        Assert.True(businessUnit.IsSuccess);
        Assert.Equal(2, businessUnit.Value!.Departments.Count);
        Assert.Equal(4, departments.TotalCount);
        Assert.All(departments.Items, department => Assert.True(department.IsActive));

        using var unitJson = JsonDocument.Parse(JsonSerializer.Serialize(businessUnit.Value, ApiJsonOptions));
        var departmentJson = unitJson.RootElement.GetProperty("departments")[0];
        Assert.False(unitJson.RootElement.TryGetProperty("organization", out _));
        Assert.False(departmentJson.TryGetProperty("businessUnit", out _));

        var departmentDetail = await departmentService.GetByIdAsync(_departmentA1.Id);
        Assert.True(departmentDetail.IsSuccess);
        using var departmentDetailJson = JsonDocument.Parse(JsonSerializer.Serialize(departmentDetail.Value, ApiJsonOptions));
        var team = departmentDetailJson.RootElement.GetProperty("teams")[0];
        Assert.False(departmentDetailJson.RootElement.TryGetProperty("businessUnit", out _));
        Assert.False(team.TryGetProperty("department", out _));
        Assert.Equal("Engineering", departments.Items.Single(item => item.Id == _departmentA1.Id).Name);
        Assert.Equal("ENG", departments.Items.Single(item => item.Id == _departmentA1.Id).Code);
    }

    [Fact]
    public async Task Read_Mapping_Does_Not_Change_Tracked_Entity_Relationships_Or_State()
    {
        var trackedOrganization = await _context.Organizations
            .Include(organization => organization.BusinessUnits)
                .ThenInclude(unit => unit.Departments)
                    .ThenInclude(department => department.Teams)
            .SingleAsync(organization => organization.Id == _organizationA.Id);
        var trackedBusinessUnit = trackedOrganization.BusinessUnits.Single(unit => unit.Id == _businessUnitA1.Id);
        var trackedDepartment = trackedBusinessUnit.Departments.Single(department => department.Id == _departmentA1.Id);
        var trackedTeam = trackedDepartment.Teams.Single(team => team.Id == _teamA1.Id);
        Assert.Same(trackedOrganization, trackedBusinessUnit.Organization);
        Assert.Same(trackedBusinessUnit, trackedDepartment.BusinessUnit);
        Assert.Same(trackedDepartment, trackedTeam.Department);
        var trackedEntries = _context.ChangeTracker.Entries().ToArray();

        var page = await CreateOrganizationService().GetPagedAsync(1, 100);
        _ = JsonSerializer.Serialize(page, ApiJsonOptions);
        _context.ChangeTracker.DetectChanges();

        Assert.Same(trackedOrganization, trackedBusinessUnit.Organization);
        Assert.Same(trackedBusinessUnit, trackedDepartment.BusinessUnit);
        Assert.Same(trackedDepartment, trackedTeam.Department);
        Assert.All(trackedEntries, entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Equal(0, await _context.SaveChangesAsync());
    }

    private OrganizationService CreateOrganizationService() => new(
        _context,
        _httpContextAccessor,
        Mock.Of<IAuditService>(),
        Mock.Of<IOrganizationSettingsAccessService>());

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()) }, "tests"))
        };
        _httpContextAccessor.HttpContext = httpContext;
    }

    private static Organization OrganizationFor(Guid tenantId, string name, string code) => new()
    {
        TenantId = tenantId,
        Name = name,
        Code = code,
        IsActive = true,
        CreatedBy = "serialization-tests"
    };

    private static BusinessUnit BusinessUnitFor(Guid tenantId, Organization organization, string name, string code)
    {
        var unit = new BusinessUnit
        {
            TenantId = tenantId,
            OrganizationId = organization.Id,
            Name = name,
            Code = code,
            IsActive = true,
            CreatedBy = "serialization-tests"
        };
        organization.BusinessUnits.Add(unit);
        return unit;
    }

    private static Department DepartmentFor(Guid tenantId, BusinessUnit businessUnit, string name, string code)
    {
        var department = new Department
        {
            TenantId = tenantId,
            BusinessUnitId = businessUnit.Id,
            Name = name,
            Code = code,
            IsActive = true,
            CreatedBy = "serialization-tests"
        };
        businessUnit.Departments.Add(department);
        return department;
    }

    private static Team TeamFor(Guid tenantId, Department department, string name, string code)
    {
        var team = new Team
        {
            TenantId = tenantId,
            DepartmentId = department.Id,
            Name = name,
            Code = code,
            IsActive = true,
            CreatedBy = "serialization-tests"
        };
        department.Teams.Add(team);
        return team;
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class OrganizationTestDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrganizationTestDbContext(DbContextOptions<OrganizationTestDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Organization> Organizations => Set<Organization>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Organization>()
                .HasMany(organization => organization.BusinessUnits)
                .WithOne(unit => unit.Organization)
                .HasForeignKey(unit => unit.OrganizationId);
            modelBuilder.Entity<BusinessUnit>()
                .HasMany(unit => unit.Departments)
                .WithOne(department => department.BusinessUnit)
                .HasForeignKey(department => department.BusinessUnitId);
            modelBuilder.Entity<Department>()
                .HasMany(department => department.Teams)
                .WithOne(team => team.Department)
                .HasForeignKey(team => team.DepartmentId);
            modelBuilder.Entity<Team>()
                .HasMany(team => team.Members)
                .WithOne(member => member.Team)
                .HasForeignKey(member => member.TeamId);

            modelBuilder.Entity<Organization>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);
            modelBuilder.Entity<BusinessUnit>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);
            modelBuilder.Entity<Department>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);
            modelBuilder.Entity<Team>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);
            modelBuilder.Entity<TeamMember>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);
        }

        private Guid CurrentTenantId => Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value,
            out var tenantId) ? tenantId : Guid.Empty;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
