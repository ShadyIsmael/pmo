using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectStatusReportWorkflowProvisioningTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Legacy_workflow_upgrade_inserts_new_graph_instead_of_returning_publish_409()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failures = new ProvisioningFailureCapture();
        await using var context = CreateContext(connection, failures);
        await context.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var definition = LegacyDefinition(tenantId);
        context.Add(definition);
        await context.SaveChangesAsync();
        var legacyVersion = definition.Versions.Single();
        definition.ActivePublishedVersionId = legacyVersion.Id;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        failures.Clear();

        var service = new ProjectStatusReportWorkflowProvisioningService(context, NullLogger<ProjectStatusReportWorkflowProvisioningService>.Instance);
        var result = await service.EnsureAsync(tenantId, "publisher");

        foreach (var failure in failures.Messages) output.WriteLine(failure);
        Assert.True(result.IsSuccess,
            $"Publish provisioning returned HTTP {result.Error?.StatusCode}: {result.Error?.Message}\n" +
            string.Join("\n", failures.Messages));
        Assert.Empty(failures.Messages);
        context.ChangeTracker.Clear();
        var persisted = await context.Set<WorkflowDefinition>().SingleAsync();
        Assert.Equal(result.Value!.VersionId, persisted.ActivePublishedVersionId);
        var versions = await context.Set<WorkflowDefinitionVersion>()
            .Include(version => version.States)
            .Include(version => version.Transitions).ThenInclude(transition => transition.FromState)
            .Include(version => version.Transitions).ThenInclude(transition => transition.ToState)
            .Include(version => version.Transitions).ThenInclude(transition => transition.Actions)
            .OrderBy(version => version.VersionNumber).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(legacyVersion.Id, versions[0].Id);
        Assert.True(ProjectStatusReportWorkflow.IsLegacyWorkflowVersion(versions[0]));
        Assert.True(ProjectStatusReportWorkflow.ValidateWorkflowVersion(versions[1]).IsSuccess);
        Assert.Equal("DomainProjection", Assert.Single(Assert.Single(versions[1].Transitions).Actions).ActionType);
        var repeated = await service.EnsureAsync(tenantId, "publisher");
        Assert.True(repeated.IsSuccess);
        Assert.Equal(result.Value, repeated.Value);
        Assert.Equal(2, await context.Set<WorkflowDefinitionVersion>().CountAsync());
    }

    [Fact]
    public async Task First_provisioning_persists_the_definition_and_all_required_children()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection, new ProvisioningFailureCapture());
        await context.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var result = await new ProjectStatusReportWorkflowProvisioningService(
            context, NullLogger<ProjectStatusReportWorkflowProvisioningService>.Instance).EnsureAsync(tenantId, "publisher");

        Assert.True(result.IsSuccess, result.Error?.Message);
        context.ChangeTracker.Clear();
        Assert.Equal(result.Value!.DefinitionId, (await context.Set<WorkflowDefinition>().SingleAsync()).Id);
        Assert.Equal(result.Value.VersionId, (await context.Set<WorkflowDefinitionVersion>().SingleAsync()).Id);
        Assert.Equal(2, await context.Set<WorkflowState>().CountAsync());
        Assert.Single(await context.Set<WorkflowTransition>().ToListAsync());
        Assert.Single(await context.Set<WorkflowTransitionAction>().ToListAsync());
    }

    private static AppDbContext CreateContext(SqliteConnection connection, SaveChangesInterceptor interceptor) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options);

    private static void SetTenant(Guid tenantId) => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", tenantId.ToString())], "test"))
        }
    });

    private static WorkflowDefinition LegacyDefinition(Guid tenantId)
    {
        var definition = new WorkflowDefinition
        {
            TenantId = tenantId, Code = ProjectStatusReportWorkflow.DefinitionCode,
            Name = "Legacy status report", Domain = WorkflowDomains.Reporting,
            EntityType = ProjectStatusReportWorkflow.EntityType, CreatedBy = "seed"
        };
        var version = new WorkflowDefinitionVersion
        {
            TenantId = tenantId, VersionNumber = 1,
            LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published, CreatedBy = "seed"
        };
        var states = new Dictionary<string, WorkflowState>();
        foreach (var code in new[] { "Draft", "Generated", "Submitted", "Approved", "Returned", "Published" })
        {
            var state = new WorkflowState
            {
                TenantId = tenantId, Code = code, NameEn = code, NameAr = code,
                IsInitial = code == "Draft", IsFinal = code == "Published", CreatedBy = "seed"
            };
            version.States.Add(state);
            states.Add(code, state);
        }
        foreach (var (code, from, to) in new[]
        {
            ("GENERATE", "Draft", "Generated"), ("SUBMIT", "Generated", "Submitted"),
            ("APPROVE", "Submitted", "Approved"), ("RETURN", "Submitted", "Returned"),
            ("REOPEN", "Returned", "Draft"), ("PUBLISH", "Approved", "Published")
        })
            version.Transitions.Add(new WorkflowTransition
            {
                TenantId = tenantId, Code = code, NameEn = code, NameAr = code,
                FromState = states[from], ToState = states[to], CreatedBy = "seed"
            });
        definition.Versions.Add(version);
        return definition;
    }

    private sealed class ProvisioningFailureCapture : SaveChangesInterceptor
    {
        public List<string> Messages { get; } = [];
        public void Clear() => Messages.Clear();

        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
            ConcurrencyExceptionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            var exception = (DbUpdateConcurrencyException)eventData.Exception;
            var states = string.Join(", ", exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"));
            Messages.Add($"{exception}\nConflicting EF entries: {states}");
            return ValueTask.FromResult(result);
        }
    }
}
