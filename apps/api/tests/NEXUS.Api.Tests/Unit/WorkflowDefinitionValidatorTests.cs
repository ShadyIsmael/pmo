using Moq;
using NEXUS.Modules.WorkflowEngine.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Guards;
using NEXUS.Modules.WorkflowEngine.Application.Actions;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class WorkflowDefinitionValidatorTests
{
    private static WorkflowConfigurationRegistry CreateRegistry()
    {
        var guards = new List<IWorkflowGuardHandler>
        {
            new RequirePermissionGuardHandler(),
            new RequireRoleGuardHandler(),
            new RequireCommentGuardHandler(),
            new RequireFactGuardHandler()
        };
        var actions = new List<IWorkflowActionHandler>
        {
            new DomainProjectionActionHandler(),
            new AuditProjectionActionHandler(Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>())
        };
        return new WorkflowConfigurationRegistry(guards, actions);
    }

    private readonly WorkflowDefinitionValidator _validator = new(CreateRegistry(), CreateRegistry());

    [Fact]
    public void Accepts_valid_definition_with_typed_rules()
    {
        var result = _validator.Validate("""
        {"states":[{"code":"Draft","isInitial":true},{"code":"Done","isFinal":true}],"transitions":[{"code":"Complete","fromStateCode":"Draft","toStateCode":"Done","guards":[{"type":"RequireComment","configuration":{}}],"actions":[{"type":"AuditProjection","configurationJson":"{}"}]}]}
        """);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public void Rejects_missing_or_multiple_initial_states()
    {
        Assert.True(_validator.Validate("{\"states\":[{\"code\":\"Draft\"}],\"transitions\":[]}").IsFailure);
        Assert.True(_validator.Validate("{\"states\":[{\"code\":\"A\",\"isInitial\":true},{\"code\":\"B\",\"isInitial\":true}],\"transitions\":[]}").IsFailure);
    }

    [Fact]
    public void Rejects_duplicate_codes_and_duplicate_edges()
    {
        var result = _validator.Validate("""
        {"states":[{"code":"A","isInitial":true},{"code":"B"}],"transitions":[{"code":"Go","from":"A","to":"B"},{"code":"Go","from":"A","to":"B"}]}
        """);

        Assert.True(result.IsFailure);
        Assert.Contains("Duplicate transition code", result.Error!.Message);
        Assert.Contains("Duplicate active transition", result.Error.Message);
    }

    [Fact]
    public void Rejects_final_outgoing_self_and_unknown_rule_types()
    {
        var result = _validator.Validate("""
        {"states":[{"code":"A","isInitial":true,"isFinal":true}],"transitions":[{"code":"Bad","from":"A","to":"A","guards":[{"type":"NoSuchGuard","configuration":[]}] }]}
        """);

        Assert.True(result.IsFailure);
        Assert.Contains("same state", result.Error!.Message);
        Assert.Contains("Final state", result.Error.Message);
        Assert.Contains("Unsupported guard type", result.Error.Message);
        Assert.Contains("configuration", result.Error.Message);
    }

    [Fact]
    public void Rejects_malformed_json_and_non_object_root()
    {
        Assert.True(_validator.Validate("not-json").IsFailure);
        Assert.True(_validator.Validate("[]").IsFailure);
    }

    [Fact]
    public void Configures_history_as_non_cascading_and_instances_with_concurrency()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var context = new AppDbContext(options);
        var history = context.Model.FindEntityType(typeof(WorkflowTransitionHistory))!;
        var instance = context.Model.FindEntityType(typeof(WorkflowInstance))!;

        Assert.All(history.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.True(instance.FindProperty(nameof(WorkflowInstance.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(instance.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(WorkflowInstance.EntityId)));
    }
}
