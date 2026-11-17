using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class OrganizationalUnitManagementTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IResourceReferenceLookup> _resources = new();
    private readonly Mock<IManagementContextAccessor> _managementContext = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _resourceA = Guid.NewGuid();
    private readonly Guid _resourceB = Guid.NewGuid();

    public OrganizationalUnitManagementTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        SetTenant(_tenantA);
        _context.Lookups.Add(new Lookup { TenantId = _tenantA, Category = ReferenceDataCategories.OrganizationalUnitType, Code = "Division", Name = "Division", NameEn = "Division", NameAr = "قطاع", IsActive = true, CreatedBy = "test" });
        _context.OrganizationalUnits.AddRange(
            new OrganizationalUnit { TenantId = _tenantA, Code = "ORG-000001", Name = "A Root", TypeCode = "Division", CreatedBy = "test" },
            new OrganizationalUnit { TenantId = _tenantB, Code = "ORG-000001", Name = "B Root", TypeCode = "Division", CreatedBy = "test" });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Unit_Reads_Are_Tenant_Isolated()
    {
        var service = CreateService();
        var units = await service.GetUnitsAsync(includeInactive: true);

        Assert.Single(units);
        Assert.Equal("A Root", units[0].Name);
    }

    [Fact]
    public async Task ReadOnly_View_As_Get_Does_Not_Seed_Default_Organizational_Unit_Types()
    {
        _context.Lookups.RemoveRange(_context.Lookups.IgnoreQueryFilters()
            .Where(item => item.TenantId == _tenantA && item.Category == ReferenceDataCategories.OrganizationalUnitType));
        await _context.SaveChangesAsync();

        _managementContext.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            Guid.NewGuid(),
            _tenantA,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            null,
            ManagementScope.Empty));

        var service = CreateService();
        Assert.Empty(await service.GetTypesAsync(includeInactive: true));
        Assert.Empty(await service.GetUnitsAsync(includeInactive: true));

        Assert.Empty(await _context.Lookups.IgnoreQueryFilters()
            .Where(item => item.TenantId == _tenantA && item.Category == ReferenceDataCategories.OrganizationalUnitType)
            .ToListAsync());
    }

    [Fact]
    public async Task Root_Unit_Can_Be_Created_Without_A_Parent()
    {
        var result = await CreateService().CreateUnitAsync(
            new SaveOrganizationalUnitDto { Name = "Another Root", TypeCode = "Division", IsActive = true },
            "test");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value!.ParentOrganizationalUnitId);
        var persisted = await _context.OrganizationalUnits.IgnoreQueryFilters().SingleAsync(unit => unit.Id == result.Value.Id);
        Assert.Equal(_tenantA, persisted.TenantId);
    }

    [Fact]
    public async Task Moving_Unit_To_Tenant_Root_Clears_Parent()
    {
        var root = await _context.OrganizationalUnits.SingleAsync(unit => unit.TenantId == _tenantA);
        var childResult = await CreateService().CreateUnitAsync(
            new SaveOrganizationalUnitDto { Name = "Child", TypeCode = "Division", ParentOrganizationalUnitId = root.Id },
            "test");

        Assert.True(childResult.IsSuccess);
        var moveResult = await CreateService().UpdateUnitAsync(
            childResult.Value!.Id,
            new SaveOrganizationalUnitDto { Name = "Child", TypeCode = "Division", ParentOrganizationalUnitId = null },
            "test");

        Assert.True(moveResult.IsSuccess);
        Assert.Null(moveResult.Value!.ParentOrganizationalUnitId);
    }

    [Fact]
    public async Task Parent_From_Another_Tenant_Is_Rejected()
    {
        var otherTenantRoot = await _context.OrganizationalUnits
            .IgnoreQueryFilters()
            .SingleAsync(unit => unit.TenantId == _tenantB);

        var result = await CreateService().CreateUnitAsync(
            new SaveOrganizationalUnitDto { Name = "Cross Tenant Child", TypeCode = "Division", ParentOrganizationalUnitId = otherTenantRoot.Id },
            "test");

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error!.StatusCode);
        Assert.Contains("current tenant", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parent_Cycles_Are_Rejected()
    {
        var root = await _context.OrganizationalUnits.SingleAsync(unit => unit.TenantId == _tenantA);
        var child = new OrganizationalUnit { TenantId = _tenantA, Code = "ORG-000002", Name = "A Child", TypeCode = "Division", ParentOrganizationalUnitId = root.Id, CreatedBy = "test" };
        _context.OrganizationalUnits.Add(child);
        await _context.SaveChangesAsync();

        var result = await CreateService().UpdateUnitAsync(root.Id, new SaveOrganizationalUnitDto { Name = root.Name, TypeCode = root.TypeCode, ParentOrganizationalUnitId = child.Id }, "test");

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Reporting_Cycles_Are_Rejected()
    {
        _resources.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>
            {
                [_resourceA] = new ResourceReference(_resourceA, null, "Resource A", null, null, "Staff", "Active", true),
                [_resourceB] = new ResourceReference(_resourceB, null, "Resource B", null, null, "Staff", "Active", true)
            });
        _context.ResourceReportingLines.Add(new ResourceReportingLine { TenantId = _tenantA, ResourceId = _resourceA, ManagerResourceId = _resourceB, IsPrimary = true, IsActive = true, CreatedBy = "test" });
        await _context.SaveChangesAsync();

        var result = await CreateService().AssignReportingLineAsync(new SaveResourceReportingLineDto { ResourceId = _resourceB, ManagerResourceId = _resourceA }, "test");

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    private OrganizationalUnitService CreateService() => new(_context, _http.Object, _resources.Object, _managementContext.Object);

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", tenantId.ToString()), new Claim(ClaimTypes.NameIdentifier, "test") }, "test"));
        _http.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
