using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Management;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class AssignedApprovalScopeMiddlewareTests
{
    [Theory]
    [InlineData("GET", "review")]
    [InlineData("POST", "approve")]
    [InlineData("POST", "reject")]
    public async Task AssignedChangeRequestReviewAndDecisionRoutesDeferToAssignmentAuthorization(string method, string action)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(options);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        context.Request.Method = method;
        var requestId = Guid.NewGuid();
        context.Request.Path = $"/api/project-change-requests/{requestId:D}/{action}";
        context.Request.RouteValues["id"] = requestId;

        var management = new TestManagementContextAccessor(new ManagementContext(userId, tenantId,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var nextCalled = false;
        var middleware = new ProjectResourceScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db, management);

        Assert.True(nextCalled);
    }

    private sealed class TestManagementContextAccessor(ManagementContext context) : IManagementContextAccessor
    {
        public ManagementContext? Current => context;
        public void Set(ManagementContext value) => throw new NotSupportedException();
    }
}
