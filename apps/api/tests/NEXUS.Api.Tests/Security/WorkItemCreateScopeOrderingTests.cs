using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.Application.DTOs;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class WorkItemCreateScopeOrderingTests
{
    [Fact]
    public async Task Create_OutOfScopeProjectIsRejectedBeforeWorkflowProvisioning()
    {
        var projectId = Guid.NewGuid();
        var management = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        management.SetupGet(accessor => accessor.Current).Returns((ManagementContext?)null);
        var workflowConfiguration = new Mock<IProjectWorkflowConfigurationService>(MockBehavior.Strict);
        var workflowProvisioning = new Mock<IWorkItemWorkflowProvisioningService>(MockBehavior.Strict);
        using var context = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
        var service = new WorkItemService(
            context,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectReferenceLookup>(),
            new HttpContextAccessor(),
            Mock.Of<IWorkItemCommentAccess>(),
            Mock.Of<IWorkItemCommentAuthorLookup>(),
            Mock.Of<IWorkItemAttachmentStorage>(),
            Options.Create(new WorkItemAttachmentOptions()),
            Mock.Of<IWorkItemStatusReferenceLookup>(),
            management.Object,
            Mock.Of<IWorkItemAssigneeLookup>(),
            Mock.Of<IResourceReferenceLookup>(),
            Mock.Of<IUserReferenceLookup>(),
            workflowProvisioning.Object,
            Mock.Of<IWorkflowInstanceService>(),
            workflowConfiguration.Object);

        var result = await service.CreateAsync(new CreateWorkItemDto
        {
            ProjectId = projectId,
            Title = "Out of scope"
        }, Guid.NewGuid().ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error!.StatusCode);
        management.VerifyGet(accessor => accessor.Current, Times.Once);
        management.VerifyNoOtherCalls();
        workflowConfiguration.VerifyNoOtherCalls();
        workflowProvisioning.VerifyNoOtherCalls();
    }
}
