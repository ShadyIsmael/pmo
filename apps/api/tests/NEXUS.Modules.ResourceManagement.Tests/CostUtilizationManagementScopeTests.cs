using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class CostUtilizationManagementScopeTests
{
    [Fact]
    public async Task Empty_management_scope_without_project_filter_returns_empty_without_querying_work_sources()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actualWork = CreateActualWorkLookup();
        var plannedWork = CreatePlannedWorkLookup();
        var management = CreateManagementAccessor(actorId, tenantId, ManagementScope.Empty);
        var scopeResolver = new Mock<IManagementScopeResolver>(MockBehavior.Strict);
        var service = CreateService(context, actualWork.Object, plannedWork.Object, management.Object, scopeResolver.Object);

        var result = await service.GetAsync(
            new CostUtilizationFilterDto { From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 2) },
            actorId,
            tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Resources.Should().BeEmpty();
        result.Value.Daily.Should().BeEmpty();
        actualWork.Verify(lookup => lookup.GetAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        plannedWork.Verify(lookup => lookup.GetPlannedAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        scopeResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Requested_project_is_denied_when_scope_is_empty_and_resolver_rejects_it()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actualWork = CreateActualWorkLookup();
        var plannedWork = CreatePlannedWorkLookup();
        var management = CreateManagementAccessor(actorId, tenantId, ManagementScope.Empty);
        var scopeResolver = new Mock<IManagementScopeResolver>(MockBehavior.Strict);
        scopeResolver
            .Setup(resolver => resolver.CanAccessProjectAsync(actorId, tenantId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(context, actualWork.Object, plannedWork.Object, management.Object, scopeResolver.Object);

        var result = await service.GetAsync(
            new CostUtilizationFilterDto
            {
                From = new DateTime(2026, 10, 1),
                To = new DateTime(2026, 10, 2),
                ProjectId = projectId
            },
            actorId,
            tenantId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FORBIDDEN");
        scopeResolver.Verify(resolver => resolver.CanAccessProjectAsync(actorId, tenantId, projectId, It.IsAny<CancellationToken>()), Times.Once);
        actualWork.Verify(lookup => lookup.GetAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        plannedWork.Verify(lookup => lookup.GetPlannedAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Requested_project_allowed_by_resolver_is_passed_as_the_only_project_filter()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actualWork = CreateActualWorkLookup();
        var plannedWork = CreatePlannedWorkLookup();
        var management = CreateManagementAccessor(actorId, tenantId, ManagementScope.Empty);
        var scopeResolver = new Mock<IManagementScopeResolver>(MockBehavior.Strict);
        scopeResolver
            .Setup(resolver => resolver.CanAccessProjectAsync(actorId, tenantId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(context, actualWork.Object, plannedWork.Object, management.Object, scopeResolver.Object);

        var result = await service.GetAsync(
            new CostUtilizationFilterDto
            {
                From = new DateTime(2026, 10, 1),
                To = new DateTime(2026, 10, 2),
                ProjectId = projectId
            },
            actorId,
            tenantId);

        result.IsSuccess.Should().BeTrue();
        scopeResolver.Verify(resolver => resolver.CanAccessProjectAsync(actorId, tenantId, projectId, It.IsAny<CancellationToken>()), Times.Once);
        actualWork.Verify(lookup => lookup.GetAsync(
            tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.Is<IReadOnlyCollection<Guid>?>(ids => ids != null && ids.SequenceEqual(new[] { projectId })),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        plannedWork.Verify(lookup => lookup.GetPlannedAsync(
            tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.Is<IReadOnlyCollection<Guid>?>(ids => ids != null && ids.SequenceEqual(new[] { projectId })),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    private static Mock<IActualWorkCostLookup> CreateActualWorkLookup()
    {
        var mock = new Mock<IActualWorkCostLookup>(MockBehavior.Strict);
        mock.Setup(lookup => lookup.GetAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActualWorkCostSlice>());
        return mock;
    }

    private static Mock<IPlannedWorkCostLookup> CreatePlannedWorkLookup()
    {
        var mock = new Mock<IPlannedWorkCostLookup>(MockBehavior.Strict);
        mock.Setup(lookup => lookup.GetPlannedAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlannedWorkCostSlice>());
        return mock;
    }

    private static Mock<IManagementContextAccessor> CreateManagementAccessor(Guid actorId, Guid tenantId, ManagementScope scope)
    {
        var mock = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        mock.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            scope));
        return mock;
    }

    private static CostUtilizationAnalyticsService CreateService(
        TestDbContext context,
        IActualWorkCostLookup actualWork,
        IPlannedWorkCostLookup plannedWork,
        IManagementContextAccessor management,
        IManagementScopeResolver scopeResolver)
    {
        var current = management.Current
            ?? throw new InvalidOperationException("The scope test requires an authenticated management context.");
        var identity = new ClaimsIdentity(
        [
            new Claim("tenantId", current.TenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, current.UserId.ToString()),
            new Claim("permission", "Resource.View")
        ],
        "Cost utilization scope test");
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new CostUtilizationAnalyticsService(
            context,
            actualWork,
            plannedWork,
            null!,
            null!,
            management,
            scopeResolver,
            http,
            null!);
    }
}
