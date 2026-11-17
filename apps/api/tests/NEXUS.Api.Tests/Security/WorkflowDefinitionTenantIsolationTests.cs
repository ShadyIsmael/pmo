using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Api;
using NEXUS.Modules.WorkflowEngine.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class WorkflowDefinitionTenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("a1100000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2200000-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor;
    private readonly ManagementContextAccessor _managementContextAccessor = new();
    private readonly AppDbContext _db;
    private readonly WorkflowManagementService _service;
    private readonly Guid _definitionAId = Guid.NewGuid();
    private readonly Guid _definitionBId = Guid.NewGuid();
    private readonly Guid _draftAId = Guid.NewGuid();
    private readonly Guid _draftBId = Guid.NewGuid();

    public WorkflowDefinitionTenantIsolationTests()
    {
        _httpContextAccessor = AuthenticatedHttpContext(TenantA);
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SqliteTestAppDbContext(options);
        _db.Database.EnsureCreated();
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);
        _managementContextAccessor.Set(new ManagementContext(
            Guid.NewGuid(), TenantA, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, ManagementScope.Empty));

        SeedDefinition(TenantA, _definitionAId, _draftAId, "TENANT_A_FLOW");
        SeedDefinition(TenantB, _definitionBId, _draftBId, "TENANT_B_FLOW");

        var registries = new WorkflowConfigurationRegistry(
            Array.Empty<IWorkflowGuardHandler>(),
            Array.Empty<IWorkflowActionHandler>());
        var validator = new WorkflowDefinitionValidator(registries, registries);
        _service = new WorkflowManagementService(
            _db,
            validator,
            Mock.Of<IWorkflowEntityAdapterRegistry>(),
            Array.Empty<IWorkflowEntityMetadataProvider>(),
            Array.Empty<IWorkflowDefinitionConsumerValidator>(),
            Array.Empty<IWorkflowGuardHandler>(),
            Array.Empty<IWorkflowActionHandler>(),
            Mock.Of<IAuditService>(),
            _httpContextAccessor,
            _managementContextAccessor);
    }

    [Fact]
    public async Task ListAndGet_ReturnOnlyCurrentTenantDefinitions()
    {
        var list = await _service.ListDefinitionsAsync(null, null, null, null);
        var definition = await _service.GetDefinitionAsync(_definitionAId);
        var foreignDefinition = await _service.GetDefinitionAsync(_definitionBId);

        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Equal(new[] { _definitionAId }, list.Value!.Select(item => item.Id));
        Assert.True(definition.IsSuccess, definition.Error?.Message);
        Assert.Equal(TenantA, _db.WorkflowDefinitions.IgnoreQueryFilters().Single(item => item.Id == _definitionAId).TenantId);
        Assert.False(foreignDefinition.IsSuccess);
        Assert.Equal(404, foreignDefinition.Error!.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDraftDelete_CannotMutateForeignTenantRecords_ButSameTenantUpdateWorks()
    {
        var foreignUpdate = await _service.UpdateDefinitionAsync(
            _definitionBId,
            new UpdateWorkflowDefinitionRequest { Name = "Attacker changed this", IsActive = false },
            "tenant-a-user");
        var foreignDelete = await _service.DeleteDraftAsync(_draftBId, null, "tenant-a-user");

        Assert.False(foreignUpdate.IsSuccess);
        Assert.Equal(404, foreignUpdate.Error!.StatusCode);
        Assert.False(foreignDelete.IsSuccess);
        Assert.Equal(404, foreignDelete.Error!.StatusCode);

        var foreignDefinition = await _db.WorkflowDefinitions.IgnoreQueryFilters().SingleAsync(item => item.Id == _definitionBId);
        var foreignDraft = await _db.WorkflowDefinitionVersions.IgnoreQueryFilters().SingleAsync(item => item.Id == _draftBId);
        Assert.Equal("TENANT_B_FLOW", foreignDefinition.Name);
        Assert.True(foreignDefinition.IsActive);
        Assert.False(foreignDraft.IsDeleted);

        var sameTenantUpdate = await _service.UpdateDefinitionAsync(
            _definitionAId,
            new UpdateWorkflowDefinitionRequest { Name = "Tenant A Updated", IsActive = true },
            "tenant-a-user");

        Assert.True(sameTenantUpdate.IsSuccess, sameTenantUpdate.Error?.Message);
        Assert.Equal("Tenant A Updated", sameTenantUpdate.Value!.Name);
    }

    [Fact]
    public async Task DraftDefinitionMutations_RejectReadOnlyManagementContextBeforeReadingOrWriting()
    {
        _managementContextAccessor.Set(_managementContextAccessor.Current! with
        {
            ViewMode = ManagementViewMode.ReadOnlyViewAs
        });
        var actor = "tenant-a-user";
        var stateRequest = new SaveWorkflowStateRequest
        {
            Code = "Added",
            NameEn = "Added",
            NameAr = "Added",
            IsInitial = true,
            IsActive = true
        };
        var transitionRequest = new SaveWorkflowTransitionRequest
        {
            Code = "Added",
            NameEn = "Added",
            NameAr = "Added",
            FromStateId = Guid.NewGuid(),
            ToStateId = Guid.NewGuid(),
            IsActive = true
        };
        var guardRequest = new SaveWorkflowGuardRequest { GuardType = "RequireComment" };
        var actionRequest = new SaveWorkflowActionRequest { ActionType = "AuditProjection" };

        var statusCodes = new List<int>
        {
            (await _service.CreateDefinitionAsync(new CreateWorkflowDefinitionRequest { Name = "Created", Code = "CREATED" }, actor)).Error!.StatusCode,
            (await _service.UpdateDefinitionAsync(_definitionAId, new UpdateWorkflowDefinitionRequest { Name = "Changed" }, actor)).Error!.StatusCode,
            (await _service.CreateDraftAsync(_definitionAId, new CreateDraftRequest(), actor)).Error!.StatusCode,
            (await _service.DeleteDraftAsync(_draftAId, null, actor)).Error!.StatusCode,
            (await _service.PublishAsync(_draftAId, new PublishWorkflowRequest(), actor)).Error!.StatusCode,
            (await _service.CreateStateAsync(_draftAId, stateRequest, actor)).Error!.StatusCode,
            (await _service.UpdateStateAsync(Guid.NewGuid(), stateRequest, actor)).Error!.StatusCode,
            (await _service.DeleteStateAsync(Guid.NewGuid(), null, actor)).Error!.StatusCode,
            (await _service.CreateTransitionAsync(_draftAId, transitionRequest, actor)).Error!.StatusCode,
            (await _service.UpdateTransitionAsync(Guid.NewGuid(), transitionRequest, actor)).Error!.StatusCode,
            (await _service.DeleteTransitionAsync(Guid.NewGuid(), null, actor)).Error!.StatusCode,
            (await _service.CreateGuardAsync(Guid.NewGuid(), guardRequest, actor)).Error!.StatusCode,
            (await _service.UpdateGuardAsync(Guid.NewGuid(), guardRequest, actor)).Error!.StatusCode,
            (await _service.DeleteGuardAsync(Guid.NewGuid(), null, actor)).Error!.StatusCode,
            (await _service.CreateActionAsync(Guid.NewGuid(), actionRequest, actor)).Error!.StatusCode,
            (await _service.UpdateActionAsync(Guid.NewGuid(), actionRequest, actor)).Error!.StatusCode,
            (await _service.DeleteActionAsync(Guid.NewGuid(), null, actor)).Error!.StatusCode
        };

        Assert.All(statusCodes, statusCode => Assert.Equal(403, statusCode));
        Assert.Empty(_db.Set<WorkflowState>());
        Assert.Empty(_db.Set<WorkflowTransition>());
        Assert.Empty(_db.Set<WorkflowTransitionGuard>());
        Assert.Empty(_db.Set<WorkflowTransitionAction>());
        Assert.Empty(_db.Set<AuditEntry>().Where(entry => entry.EntityType == "WorkflowManagement"));
        Assert.True(_db.WorkflowDefinitions.Single(item => item.Id == _definitionAId).IsActive);
        Assert.Equal(1, _db.WorkflowDefinitionVersions.Count(item => item.WorkflowDefinitionId == _definitionAId));
    }

    [Fact]
    public async Task DefinitionMutation_RejectsActorThatDoesNotMatchAuthenticatedIdentity()
    {
        var result = await _service.UpdateDefinitionAsync(
            _definitionAId,
            new UpdateWorkflowDefinitionRequest { Name = "Spoofed actor update" },
            "different-user");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.StatusCode);
        Assert.Equal("TENANT_A_FLOW", _db.WorkflowDefinitions.Single(item => item.Id == _definitionAId).Name);
        Assert.Empty(_db.Set<AuditEntry>().Where(entry => entry.EntityType == "WorkflowManagement"));
    }

    [Fact]
    public async Task WorkflowManagementController_MapsMissingActorIdentityToUnauthorized()
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var originalUser = httpContext.User;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenantId", TenantA.ToString())],
            authenticationType: "test"));
        try
        {
            var controller = new WorkflowManagementController(_service)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Update(
                _definitionAId,
                new UpdateWorkflowDefinitionRequest { Name = "Missing actor update" },
                CancellationToken.None);

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("TENANT_A_FLOW", _db.WorkflowDefinitions.Single(item => item.Id == _definitionAId).Name);
        }
        finally
        {
            httpContext.User = originalUser;
        }
    }

    private void SeedDefinition(Guid tenantId, Guid definitionId, Guid draftId, string code)
    {
        var definition = new WorkflowDefinition
        {
            Id = definitionId,
            TenantId = tenantId,
            Code = code,
            Name = code,
            Domain = WorkflowDomains.Task,
            EntityType = WorkflowDomains.EntityTypeFor(WorkflowDomains.Task),
            IsActive = true,
            CreatedBy = "fixture"
        };
        var version = new WorkflowDefinitionVersion
        {
            Id = draftId,
            TenantId = tenantId,
            WorkflowDefinitionId = definitionId,
            VersionNumber = 1,
            LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Draft,
            CreatedBy = "fixture"
        };
        definition.Versions.Add(version);
        _db.WorkflowDefinitions.Add(definition);
        _db.SaveChanges();
    }

    private static HttpContextAccessor AuthenticatedHttpContext(Guid tenantId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString()), new Claim("sub", "tenant-a-user")],
                authenticationType: "test"))
        }
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
    }
}
