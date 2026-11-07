using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.WorkflowEngine.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class WorkflowDefinitionTenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("a1100000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2200000-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor;
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
            _httpContextAccessor);
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
