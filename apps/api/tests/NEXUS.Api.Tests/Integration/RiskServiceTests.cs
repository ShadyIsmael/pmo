using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class RiskServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly ProjectReferenceLookup _projects;
    private readonly ResourceReferenceLookup _resources;
    private readonly RiskService _service;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    public RiskServiceTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _audit.Setup(a => a.LogAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _projects = new ProjectReferenceLookup(_context);
        _resources = new ResourceReferenceLookup(_context);
        _service = new RiskService(_context, _audit.Object, _projects, _resources, _accessor.Object);
    }

    [Fact]
    public async Task Create_ValidRiskPersistsProjectAndOwnerReferences()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        var owner = await AddResourceAsync(_tenantA, "Owner A");
        SetTenant(_tenantA);

        var result = await _service.CreateAsync(new CreateRiskDto
        {
            ProjectId = project.Id,
            Title = "Supplier delay",
            Description = "The supplier may miss the delivery date.",
            RiskLevel = RiskLevel.High,
            OwnerResourceId = owner.Id
        }, _user.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(project.Id, result.Value!.ProjectId);
        Assert.Equal(owner.Id, result.Value.OwnerResourceId);
        Assert.Equal("Project A", result.Value.ProjectName);
        Assert.Equal("Owner A", result.Value.OwnerResourceName);
        _audit.Verify(a => a.LogAsync(It.Is<NEXUS.Modules.Audit.Domain.AuditEntry>(entry => entry.EntityType == "Risk" && entry.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAndLists_ReturnThePersistedIdThatResolvesToDetail()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);

        var created = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        var persisted = await _context.Risks.IgnoreQueryFilters().SingleAsync(risk => risk.Id == created.Value!.Id);
        Assert.Equal(created.Value.Id, persisted.Id);

        var detail = await _service.GetByIdAsync(created.Value.Id);
        Assert.True(detail.IsSuccess, detail.Error?.Message);

        var mainList = await _service.GetPagedAsync(1, 100);
        Assert.Contains(mainList.Items, risk => risk.Id == created.Value.Id);

        var projectList = await _service.GetPagedAsync(1, 100, project.Id);
        Assert.Contains(projectList.Items, risk => risk.Id == created.Value.Id);
    }

    [Fact]
    public async Task Create_CrossTenantProjectIsRejected()
    {
        var project = await AddProjectAsync(_tenantB, "Other tenant project");
        SetTenant(_tenantA);

        var result = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error!.StatusCode);
        Assert.Empty(await _context.Risks.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Create_InvalidOwnerIsRejected()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var dto = ValidDto(project.Id);
        dto.OwnerResourceId = Guid.NewGuid();

        var result = await _service.CreateAsync(dto, _user.ToString());

        Assert.True(result.IsFailure);
        Assert.Contains("owner", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _context.Risks.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Create_MultipleImpactTypesPersistWithOneSharedDescription_AndDuplicateTypeIsRejected()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var dto = ValidDto(project.Id);
        dto.ImpactAssessment = new CreateRiskImpactAssessmentDto { ImpactTypes = new[] { RiskImpactType.Schedule, RiskImpactType.Budget, RiskImpactType.Resources }, Description = "Delivery may slip, expediting may add cost, and additional resources may be needed." };
        var created = await _service.CreateAsync(dto, _user.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);
        var impacts = await _service.GetImpactsAsync(created.Value!.Id);
        Assert.True(impacts.IsSuccess);
        Assert.Single(impacts.Value!);
        Assert.Equal(3, impacts.Value[0].ImpactTypes.Count);
        Assert.Equal("Delivery may slip, expediting may add cost, and additional resources may be needed.", impacts.Value[0].Description);

        var duplicate = await _service.CreateImpactAsync(created.Value.Id, new CreateRiskImpactAssessmentDto { ImpactTypes = new[] { RiskImpactType.Schedule, RiskImpactType.Schedule }, Description = "Another description" }, _user.ToString());
        Assert.True(duplicate.IsFailure);
        Assert.Equal(409, duplicate.Error!.StatusCode);
    }

    [Fact]
    public async Task UpdateImpact_ReplacesTypesAndDescriptionWithoutDuplicateRows()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var created = await _service.CreateAsync(new CreateRiskDto
        {
            ProjectId = project.Id,
            Title = "Supplier delay",
            Description = "A delivery date may slip.",
            RiskLevel = RiskLevel.Medium,
            ImpactAssessment = new CreateRiskImpactAssessmentDto
            {
                ImpactTypes = new[] { RiskImpactType.Schedule },
                Description = "Schedule impact"
            }
        }, _user.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);
        var initial = (await _service.GetImpactsAsync(created.Value!.Id)).Value!.Single();

        var updated = await _service.UpdateImpactAsync(created.Value.Id, initial.Id, new UpdateRiskImpactAssessmentDto
        {
            ImpactTypes = new[] { RiskImpactType.Budget, RiskImpactType.Resources },
            Description = "Budget and resource impact",
            RowVersion = initial.RowVersion
        }, _user.ToString());

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        var persisted = (await _service.GetImpactsAsync(created.Value.Id)).Value!;
        Assert.Single(persisted);
        Assert.Equal(new[] { RiskImpactType.Budget, RiskImpactType.Resources }, persisted[0].ImpactTypes);
        Assert.Equal("Budget and resource impact", persisted[0].Description);
        Assert.Equal(2, await _context.RiskImpactAssessmentTypes.CountAsync(type => type.RiskImpactAssessmentId == initial.Id));
    }

    [Fact]
    public async Task ListAndGet_AreTenantScoped()
    {
        var projectA = await AddProjectAsync(_tenantA, "Project A");
        var projectB = await AddProjectAsync(_tenantB, "Project B");
        SetTenant(_tenantA);
        var created = await _service.CreateAsync(ValidDto(projectA.Id), _user.ToString());
        Assert.True(created.IsSuccess);

        SetTenant(_tenantB);
        var list = await _service.GetPagedAsync(1, 100);
        var hidden = await _service.GetByIdAsync(created.Value!.Id);
        var hiddenActions = await _service.GetActionsAsync(created.Value.Id);
        var hiddenImpacts = await _service.GetImpactsAsync(created.Value.Id);

        Assert.Empty(list.Items);
        Assert.True(hidden.IsFailure);
        Assert.Equal(404, hidden.Error!.StatusCode);
        Assert.True(hiddenActions.IsFailure);
        Assert.True(hiddenImpacts.IsFailure);
        Assert.Equal(404, hiddenActions.Error!.StatusCode);
        Assert.Equal(404, hiddenImpacts.Error!.StatusCode);
        Assert.NotEqual(projectA.Id, projectB.Id);
    }

    [Fact]
    public async Task NestedWorkflowQueries_ReturnEmptyCollectionsForExistingRisk_And404ForMissingRisk()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var created = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(created.IsSuccess);

        var impacts = await _service.GetImpactsAsync(created.Value!.Id);
        var actions = await _service.GetActionsAsync(created.Value.Id);
        Assert.True(impacts.IsSuccess);
        Assert.True(actions.IsSuccess);
        Assert.Empty(impacts.Value!);
        Assert.Empty(actions.Value!);

        var missing = Guid.Parse("67f082cd-af32-420d-ab88-7ceb11eb6c6e");
        var missingImpacts = await _service.GetImpactsAsync(missing);
        var missingActions = await _service.GetActionsAsync(missing);
        Assert.True(missingImpacts.IsFailure);
        Assert.True(missingActions.IsFailure);
        Assert.Equal(404, missingImpacts.Error!.StatusCode);
        Assert.Equal(404, missingActions.Error!.StatusCode);
    }

    [Fact]
    public async Task DeletedRisk_IsInaccessibleFromDetailAndNestedWorkflowQueries()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var created = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        var stored = await _context.Risks.IgnoreQueryFilters().SingleAsync(risk => risk.Id == created.Value!.Id);
        stored.IsDeleted = true;
        await _context.SaveChangesAsync();

        var detail = await _service.GetByIdAsync(stored.Id);
        var actions = await _service.GetActionsAsync(stored.Id);
        var impacts = await _service.GetImpactsAsync(stored.Id);

        Assert.True(detail.IsFailure);
        Assert.True(actions.IsFailure);
        Assert.True(impacts.IsFailure);
        Assert.Equal(404, detail.Error!.StatusCode);
        Assert.Equal(404, actions.Error!.StatusCode);
        Assert.Equal(404, impacts.Error!.StatusCode);
    }

    [Fact]
    public async Task Workflow_CreateActionAndUpdates_AdvancesRiskStatusAndKeepsHistory()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);

        var created = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(RiskStatus.Identified, created.Value!.Status);

        var action = await _service.CreateActionAsync(created.Value.Id, new CreateRiskActionDto
        {
            Title = "Contact supplier",
            Description = "Confirm the revised delivery date."
        }, _user.ToString());

        Assert.True(action.IsSuccess, action.Error?.Message);
        var afterAction = await _service.GetByIdAsync(created.Value.Id);
        Assert.Equal(RiskStatus.ActionRequired, afterAction.Value!.Status);

        var edited = await _service.UpdateActionAsync(created.Value.Id, action.Value.Id, new UpdateRiskActionDto { Title = "Contact supplier", Description = "Confirm the revised delivery date.", Status = RiskActionStatus.Planned, NextFollowUpDate = DateTime.UtcNow.AddDays(7), RowVersion = action.Value.RowVersion }, _user.ToString());
        Assert.True(edited.IsSuccess, edited.Error?.Message);
        var firstUpdate = await _service.AddActionUpdateAsync(created.Value.Id, action.Value.Id, new CreateRiskActionUpdateDto
        {
            UpdateText = "Supplier acknowledged the request."
        }, _user.ToString());
        var secondUpdate = await _service.AddActionUpdateAsync(created.Value.Id, action.Value.Id, new CreateRiskActionUpdateDto
        {
            UpdateText = "New delivery date confirmed."
        }, _user.ToString());

        Assert.True(firstUpdate.IsSuccess, firstUpdate.Error?.Message);
        Assert.True(secondUpdate.IsSuccess, secondUpdate.Error?.Message);
        var history = await _service.GetActionUpdatesAsync(created.Value.Id, action.Value.Id);
        Assert.True(history.IsSuccess);
        Assert.Equal(2, history.Value!.Count);
        Assert.Equal("Supplier acknowledged the request.", history.Value[0].UpdateText);

        var mitigated = await _service.UpdateStatusAsync(created.Value.Id, new UpdateRiskStatusDto { Status = RiskStatus.Mitigated }, _user.ToString());
        Assert.True(mitigated.IsSuccess, mitigated.Error?.Message);
        var closed = await _service.UpdateStatusAsync(created.Value.Id, new UpdateRiskStatusDto { Status = RiskStatus.Closed }, _user.ToString());
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        var preserved = await _service.GetActionUpdatesAsync(created.Value.Id, action.Value.Id);
        Assert.Equal(2, preserved.Value!.Count);
    }

    [Fact]
    public async Task Workflow_CrossTenantActionIsNotVisible()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var created = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(created.IsSuccess);
        var action = await _service.CreateActionAsync(created.Value!.Id, new CreateRiskActionDto { Title = "Review" }, _user.ToString());
        Assert.True(action.IsSuccess);

        SetTenant(_tenantB);
        var hidden = await _service.GetActionsAsync(created.Value.Id);
        Assert.True(hidden.IsFailure);
        Assert.Equal(404, hidden.Error!.StatusCode);
    }

    [Fact]
    public async Task Workflow_CrossRiskActionAccessIsRejectedAndSoftDeletedActionsAreHidden()
    {
        var project = await AddProjectAsync(_tenantA, "Project A");
        SetTenant(_tenantA);
        var first = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        var second = await _service.CreateAsync(ValidDto(project.Id), _user.ToString());
        Assert.True(first.IsSuccess && second.IsSuccess);
        var action = await _service.CreateActionAsync(first.Value!.Id, new CreateRiskActionDto { Title = "Review" }, _user.ToString());
        Assert.True(action.IsSuccess);

        var crossRisk = await _service.UpdateActionAsync(second.Value!.Id, action.Value!.Id, new UpdateRiskActionDto
        {
            Title = "Should fail", Status = RiskActionStatus.Planned
        }, _user.ToString());
        Assert.True(crossRisk.IsFailure);
        Assert.Equal(404, crossRisk.Error!.StatusCode);

        var deleted = await _service.DeleteActionAsync(first.Value.Id, action.Value.Id, _user.ToString());
        Assert.True(deleted.IsSuccess);
        var actions = await _service.GetActionsAsync(first.Value.Id);
        Assert.Empty(actions.Value!);
    }

    private static CreateRiskDto ValidDto(Guid projectId) => new()
    {
        ProjectId = projectId,
        Title = "Supplier delay",
        Description = "A delivery date may slip.",
        RiskLevel = RiskLevel.Medium
    };

    private async Task<Project> AddProjectAsync(Guid tenantId, string name)
    {
        var project = new Project { TenantId = tenantId, Name = name, Code = Guid.NewGuid().ToString("N"), CreatedBy = _user.ToString() };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    private async Task<Resource> AddResourceAsync(Guid tenantId, string name)
    {
        var resource = new Resource { TenantId = tenantId, UserId = _user, Name = name, Type = "Internal", Status = "Active", CreatedBy = _user.ToString() };
        _context.Resources.Add(resource);
        await _context.SaveChangesAsync();
        return resource;
    }

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _user.ToString())
        }, "test")) };
        _accessor.Setup(a => a.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class ProjectReferenceLookup(AppDbContext context) : IProjectReferenceLookup
    {
        public Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default) => context.Projects.Where(project => project.Id == projectId).Select(project => new ProjectReference(project.Id, project.Name)).FirstOrDefaultAsync(cancellationToken);
        public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default) => await context.Projects.Where(project => projectIds.Contains(project.Id)).ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);
        public async Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default) => await context.Projects.Select(project => new ProjectReference(project.Id, project.Name)).ToListAsync(cancellationToken);
    }

    private sealed class ResourceReferenceLookup(AppDbContext context) : IResourceReferenceLookup
    {
        public Task<bool> ExistsAsync(Guid resourceId, CancellationToken cancellationToken = default) => context.Resources.AnyAsync(resource => resource.Id == resourceId, cancellationToken);
        public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default) => await context.Resources.Where(resource => resourceIds.Contains(resource.Id)).ToDictionaryAsync(resource => resource.Id, resource => resource.Name, cancellationToken);
        public Task<ResourceReference?> FindAsync(Guid resourceId, CancellationToken cancellationToken = default) => context.Resources.Where(resource => resource.Id == resourceId).Select(resource => new ResourceReference(resource.Id, resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive)).FirstOrDefaultAsync(cancellationToken);
        public Task<ResourceReference?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => context.Resources.Where(resource => resource.UserId == userId).Select(resource => new ResourceReference(resource.Id, resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive)).FirstOrDefaultAsync(cancellationToken);
        public async Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default) => await context.Resources.Where(resource => resourceIds.Contains(resource.Id)).Select(resource => new ResourceReference(resource.Id, resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive)).ToDictionaryAsync(resource => resource.Id, cancellationToken);
        public async Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => await context.Resources.Where(resource => resource.UserId.HasValue && userIds.Contains(resource.UserId.Value)).Select(resource => new ResourceReference(resource.Id, resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive)).ToDictionaryAsync(resource => resource.UserId!.Value, cancellationToken);
    }
}
