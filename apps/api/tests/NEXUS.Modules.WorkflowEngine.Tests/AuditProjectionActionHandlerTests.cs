using System.Text.Json;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Actions;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.WorkflowEngine.Tests;

public class AuditProjectionActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WritesAuditEntry()
    {
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new AuditProjectionActionHandler(auditMock.Object);

        var context = new WorkflowTransitionContext(
            new WorkflowExecutionContext(Guid.NewGuid(), "user-1", "User One"),
            Guid.NewGuid(),
            new WorkflowEntitySnapshot("WorkItem", Guid.NewGuid(), new Dictionary<string, object?> { ["StateCode"] = "InProgress" }),
            new WorkflowState { Code = "InProgress", NameEn = "In Progress", NameAr = "قيد التقدم" },
            new WorkflowState { Code = "Done", NameEn = "Done", NameAr = "منتهي" },
            new WorkflowTransition { Code = "MOVE_DONE", WorkflowDefinitionVersion = new WorkflowDefinitionVersion { VersionNumber = 1, WorkflowDefinition = new WorkflowDefinition { Code = "WORKITEM_STANDARD" } } },
            Mock.Of<IWorkflowEntityAdapter>());

        var result = await handler.ExecuteAsync(context, JsonDocument.Parse("{}").RootElement, CancellationToken.None);

        Assert.True(result.IsSuccess);
        auditMock.Verify(a => a.AppendAsync(It.Is<AuditEntry>(e =>
            e.Action == "Transitioned" &&
            e.EntityType == "WorkItem" &&
            e.UserName == "User One"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
