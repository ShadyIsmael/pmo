using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused service/integration tests for the Project Planning WBS foundation
/// (tree CRUD inside the active Working version). Uses the same in-memory SQLite
/// setup and shared tenant resolution as the other API integration tests.
/// </summary>
public class PlanWbsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IAuditService> _auditService;
    private readonly PlanService _planService;
    private readonly PlanWbsService _wbsService;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PlanWbsServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        _accessor = new Mock<IHttpContextAccessor>();
        _auditService = new Mock<IAuditService>();
        _planService = new PlanService(_context, _auditService.Object, _accessor.Object);
        _wbsService = new PlanWbsService(_context, _auditService.Object, _accessor.Object);
    }

    private void SetTenant(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "mock"));
        _accessor.Setup(x => x.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    private async Task<Guid> CreatePlanAsync(PlanType type = PlanType.Execution)
    {
        SetTenant(_tenantA, _userA);
        var result = await _planService.CreateAsync(new CreatePlanDto
        {
            ProjectId = _projectId,
            PlanType = type,
            Name = "Plan"
        }, _userA.ToString());
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private async Task<PlanVersion> CreateBaselineVersionAsync(Guid planId, Guid workingVersionId)
    {
        var baseline = new PlanVersion
        {
            TenantId = _tenantA,
            PlanId = planId,
            VersionNumber = 2,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline v1",
            SourceVersionId = workingVersionId,
            CreatedBy = _userA.ToString()
        };
        _context.PlanVersions.Add(baseline);
        await _context.SaveChangesAsync();
        return baseline;
    }

    private async Task<Guid> CreateBaselineNodeAsync(Guid planId, Guid workingVersionId, string code = "9")
    {
        var baseline = await CreateBaselineVersionAsync(planId, workingVersionId);
        var node = new PlanWbsNode
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            Code = code,
            Name = code,
            CreatedBy = _userA.ToString()
        };
        _context.PlanWbsNodes.Add(node);
        await _context.SaveChangesAsync();
        return node.Id;
    }

    [Fact]
    public async Task Create_RootNode_Succeeds()
    {
        var planId = await CreatePlanAsync();

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto
        {
            Code = "1",
            Name = "Phase 1"
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("1", result.Value.Code);
        Assert.Null(result.Value.ParentWbsId);
        Assert.Equal(0, result.Value.SortOrder);
        Assert.NotEmpty(result.Value.RowVersion);
        Assert.Single(_context.PlanWbsNodes.IgnoreQueryFilters());
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanWbsNode" && e.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ChildNode_AssignsHierarchyAndOrder()
    {
        var planId = await CreatePlanAsync();
        var parent = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Phase 1" }, _userA.ToString())).Value!;
        var child = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto
        {
            Code = "1.1",
            Name = "Group",
            ParentWbsId = parent.Id
        }, _userA.ToString())).Value!;

        Assert.True(child.ParentWbsId.HasValue);
        Assert.Equal(parent.Id, child.ParentWbsId);
        Assert.Equal(0, child.SortOrder);
    }

    [Fact]
    public async Task Create_AutoAssignsNextSiblingOrder()
    {
        var planId = await CreatePlanAsync();
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "One" }, _userA.ToString());
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "2", Name = "Two" }, _userA.ToString());

        var second = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "3", Name = "Three" }, _userA.ToString())).Value!;

        Assert.Equal(2, second.SortOrder);
    }

    [Fact]
    public async Task Create_DuplicateCode_SameVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString());

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "B" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        Assert.Single(_context.PlanWbsNodes.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_SameCodeDifferentParent_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString());
        var b = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "2", Name = "B" }, _userA.ToString())).Value!;

        // WBS codes are unique within the whole plan version, even across branches.
        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A-inner", ParentWbsId = b.Id }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_ParentFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var foreignParentId = await CreateBaselineNodeAsync(planId, workingVersion.Id);

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto
        {
            Code = "9",
            Name = "Orphan",
            ParentWbsId = foreignParentId
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_ParentNotExist_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto
        {
            Code = "9",
            Name = "Orphan",
            ParentWbsId = Guid.NewGuid()
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "  " }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_MissingCode_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Name = "Group" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _wbsService.CreateAsync(Guid.NewGuid(), new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetTree_ReturnsNestedHierarchyInOrder()
    {
        var planId = await CreatePlanAsync();
        var root = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Phase A" }, _userA.ToString())).Value!;
        var child = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1.1", Name = "Group", ParentWbsId = root.Id }, _userA.ToString())).Value!;
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1.2", Name = "Group 2", ParentWbsId = root.Id }, _userA.ToString());

        var result = await _wbsService.GetTreeAsync(planId);

        Assert.True(result.IsSuccess);
        var tree = result.Value!;
        Assert.Single(tree);
        Assert.Equal(root.Id, tree[0].Id);
        Assert.Equal(2, tree[0].Children.Count);
        Assert.Equal("1.1", tree[0].Children![0].Code);
        Assert.Equal("1.2", tree[0].Children[1].Code);
    }

    [Fact]
    public async Task GetTree_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _wbsService.GetTreeAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_ValidChanges_SucceedsAndPersists()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Before" }, _userA.ToString())).Value!;

        var result = await _wbsService.UpdateAsync(planId, node.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "After",
            SortOrder = 5,
            RowVersion = node.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal("After", result.Value!.Name);
        Assert.Equal(5, result.Value.SortOrder);
    }

    [Fact]
    public async Task Update_ExposedCodeConflict_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var a = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString())).Value!;
        var b = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "2", Name = "B" }, _userA.ToString())).Value!;

        var result = await _wbsService.UpdateAsync(planId, b.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "B",
            SortOrder = 0,
            RowVersion = b.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString())).Value!;

        var result = await _wbsService.UpdateAsync(planId, node.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "Edited",
            SortOrder = 0,
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_BaselineNode_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineNodeId = await CreateBaselineNodeAsync(planId, workingVersion.Id);

        var result = await _wbsService.UpdateAsync(planId, baselineNodeId, new UpdatePlanWbsNodeDto
        {
            Code = "9",
            Name = "Mutated",
            SortOrder = 0,
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_SelfParent_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString())).Value!;

        var result = await _wbsService.UpdateAsync(planId, node.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "A",
            SortOrder = 0,
            ParentWbsId = node.Id,
            RowVersion = node.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_Cycle_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var parent = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Root" }, _userA.ToString())).Value!;
        var child = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1.1", Name = "Child", ParentWbsId = parent.Id }, _userA.ToString())).Value!;

        // Try to make the root a child of its own descendant.
        var result = await _wbsService.UpdateAsync(planId, parent.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "Root",
            SortOrder = 0,
            ParentWbsId = child.Id,
            RowVersion = parent.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_ParentFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "A" }, _userA.ToString())).Value!;
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineParentId = await CreateBaselineNodeAsync(planId, workingVersion.Id);

        var result = await _wbsService.UpdateAsync(planId, node.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "A",
            SortOrder = 0,
            ParentWbsId = baselineParentId,
            RowVersion = node.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_UnknownNode_ReturnsNotFound()
    {
        var planId = await CreatePlanAsync();

        var result = await _wbsService.UpdateAsync(planId, Guid.NewGuid(), new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "A",
            SortOrder = 0,
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_ChildlessNode_SoftDeletes()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Leaf" }, _userA.ToString())).Value!;

        var result = await _wbsService.DeleteAsync(planId, node.Id, node.RowVersion, _userA.ToString());

        Assert.True(result.IsSuccess);
        var stored = await _context.PlanWbsNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == node.Id);
        Assert.True(stored.IsDeleted);
        var remaining = await _wbsService.GetTreeAsync(planId);
        Assert.True(remaining.IsSuccess);
        Assert.Empty(remaining.Value!);
    }

    [Fact]
    public async Task Delete_ParentWithChildren_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var parent = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Root" }, _userA.ToString())).Value!;
        await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1.1", Name = "Child", ParentWbsId = parent.Id }, _userA.ToString());

        var result = await _wbsService.DeleteAsync(planId, parent.Id, parent.RowVersion, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_BaselineNode_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineNodeId = await CreateBaselineNodeAsync(planId, workingVersion.Id);

        var result = await _wbsService.DeleteAsync(planId, baselineNodeId, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Leaf" }, _userA.ToString())).Value!;

        var result = await _wbsService.DeleteAsync(planId, node.Id, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task TenantB_CannotModifyTenantAClaimedNode()
    {
        var planId = await CreatePlanAsync();
        var node = (await _wbsService.CreateAsync(planId, new CreatePlanWbsNodeDto { Code = "1", Name = "Secret" }, _userA.ToString())).Value!;

        SetTenant(_tenantB, Guid.NewGuid());

        var result = await _wbsService.UpdateAsync(planId, node.Id, new UpdatePlanWbsNodeDto
        {
            Code = "1",
            Name = "Hacked",
            SortOrder = 0,
            RowVersion = node.RowVersion
        }, "tenantB-user");

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}