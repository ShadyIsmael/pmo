using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class CostUtilizationTeamFilterTests
{
    [Fact]
    public async Task TeamFilter_RequiresTeamViewPermission()
    {
        await using var context = CreateContext();
        var service = CreateService(context, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await service.GetAsync(
            new CostUtilizationFilterDto { From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 2), TeamId = Guid.NewGuid() },
            Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task TeamFilter_RejectsForeignTenantTeamAndIgnoresForeignTenantMemberships()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var team = new Team { TenantId = tenantId, Name = "Visible Team", IsActive = true };
        context.Teams.Add(team);
        context.Teams.Add(new Team { TenantId = foreignTenantId, Name = "Foreign Team", IsActive = true });
        context.TeamMembers.Add(new TeamMember
        {
            TenantId = foreignTenantId,
            Team = team,
            TeamId = team.Id,
            ResourceId = Guid.NewGuid(),
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
        });
        await context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("permission", "Team.View"),
            new Claim("permission", "Resource.ViewCostRate"),
        ], "test"));
        var service = CreateService(context, principal);

        var foreignTeam = await service.GetAsync(
            new CostUtilizationFilterDto { From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 2), TeamId = context.Teams.Single(item => item.Name == "Foreign Team").Id },
            Guid.NewGuid(), tenantId);
        foreignTeam.IsFailure.Should().BeTrue();
        foreignTeam.Error!.Code.Should().Be("NOT_FOUND");

        var emptyTeam = await service.GetAsync(
            new CostUtilizationFilterDto { From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 2), TeamId = team.Id },
            Guid.NewGuid(), tenantId);
        emptyTeam.IsSuccess.Should().BeTrue();
        emptyTeam.Value!.CanViewCosts.Should().BeTrue();
        emptyTeam.Value.Resources.Should().BeEmpty();
        emptyTeam.Value.Daily.Should().BeEmpty();
    }

    [Fact]
    public async Task EditingCostRatesWithoutViewPermissionDoesNotRevealCosts()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var team = new Team { TenantId = tenantId, Name = "Empty Team", IsActive = true };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("permission", "Team.View"),
            new Claim("permission", "Resource.EditCostRate"),
        ], "test"));
        var service = CreateService(context, principal);
        var result = await service.GetAsync(
            new CostUtilizationFilterDto
            {
                From = new DateTime(2026, 10, 1),
                To = new DateTime(2026, 10, 2),
                TeamId = team.Id
            },
            Guid.NewGuid(), tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CanViewCosts.Should().BeFalse();
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    private static CostUtilizationAnalyticsService CreateService(TestDbContext context, ClaimsPrincipal principal)
        => new(context, null!, null!, null!, null!, null!, null!, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } }, null!);
}
