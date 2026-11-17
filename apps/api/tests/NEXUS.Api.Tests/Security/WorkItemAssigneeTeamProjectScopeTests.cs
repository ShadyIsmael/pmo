using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class WorkItemAssigneeTeamProjectScopeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();

    public WorkItemAssigneeTeamProjectScopeTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("tenantId", _tenantId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                }, "Test"))
            }
        });
    }

    [Fact]
    public async Task TeamCandidatesAndValidationRequireCurrentProjectTeamAssociation()
    {
        var projectA = new Project { TenantId = _tenantId, Name = "Project A", Code = "PA", CreatedBy = "test" };
        var projectB = new Project { TenantId = _tenantId, Name = "Project B", Code = "PB", CreatedBy = "test" };
        var teamA = new Team { TenantId = _tenantId, Name = "Project A Team", Code = "TEAM-A", IsActive = true, CreatedBy = "test" };
        var teamB = new Team { TenantId = _tenantId, Name = "Project B Team", Code = "TEAM-B", IsActive = true, CreatedBy = "test" };
        var unassignedTenantTeam = new Team { TenantId = _tenantId, Name = "Unassigned Tenant Team", Code = "TEAM-C", IsActive = true, CreatedBy = "test" };
        _context.AddRange(projectA, projectB, teamA, teamB, unassignedTenantTeam);
        var today = DateTime.UtcNow.Date;
        _context.Set<ProjectMember>().AddRange(
            new ProjectMember
            {
                TenantId = _tenantId, ProjectId = projectA.Id, TeamId = teamA.Id, Role = "Team",
                StartDate = today, EndDate = today.AddDays(1), Status = "Active", CreatedBy = "test"
            },
            new ProjectMember
            {
                TenantId = _tenantId, ProjectId = projectB.Id, TeamId = teamB.Id, Role = "Team",
                StartDate = today, EndDate = today.AddDays(1), Status = "Active", CreatedBy = "test"
            },
            new ProjectMember
            {
                TenantId = _tenantId, ProjectId = projectA.Id, TeamId = unassignedTenantTeam.Id, Role = "Team",
                StartDate = today.AddDays(1), EndDate = today.AddDays(2), Status = "Active", CreatedBy = "test"
            });
        await _context.SaveChangesAsync();

        var lookup = new WorkItemAssigneeLookup(_context);
        var candidates = await lookup.GetEligibleTeamsAsync(projectA.Id);

        var candidate = Assert.Single(candidates);
        Assert.Equal(teamA.Id, candidate.TeamId);
        Assert.True(await lookup.IsActiveProjectTeamAsync(projectA.Id, teamA.Id));
        Assert.False(await lookup.IsActiveProjectTeamAsync(projectA.Id, teamB.Id));
        Assert.False(await lookup.IsActiveProjectTeamAsync(projectA.Id, unassignedTenantTeam.Id));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
