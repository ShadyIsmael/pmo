using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectSponsorMembershipSynchronizerTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid SponsorUserId = Guid.Parse("90000000-0000-0000-0000-000000000002");
    private static readonly Guid ResourceId = Guid.Parse("90000000-0000-0000-0000-000000000003");
    private static readonly Guid ProjectId = Guid.Parse("90000000-0000-0000-0000-000000000004");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Project _project;
    private readonly ProjectSponsorMembershipSynchronizer _synchronizer;

    public ProjectSponsorMembershipSynchronizerTests()
    {
        _connection.Open();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", TenantId.ToString()) }, "test"));
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _project = new Project
        {
            Id = ProjectId, TenantId = TenantId, Name = "Sponsor sync test", Code = "SP-1",
            Status = "Active", CreatedBy = "test", RowVersion = new byte[8]
        };
        _context.Users.Add(new User
        {
            Id = SponsorUserId, TenantId = TenantId, Email = "sponsor@example.test", PasswordHash = "hash",
            FirstName = "Sponsor", LastName = "User", IsActive = true, RowVersion = new byte[8]
        });
        _context.Resources.Add(new Resource
        {
            Id = ResourceId, TenantId = TenantId, UserId = SponsorUserId, Name = "Sponsor User",
            Type = "Employee", Status = "Active", IsActive = true, RowVersion = new byte[8]
        });
        _context.Projects.Add(_project);
        _context.SaveChanges();
        _synchronizer = new ProjectSponsorMembershipSynchronizer(_context);
    }

    [Fact]
    public async Task Synchronize_IsIdempotent_PreservesExistingTeamRole_AndOnlyDeactivatesManagedRows()
    {
        var existing = new ProjectMember
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = ProjectId, ResourceId = ResourceId,
            Role = "Delivery Lead", AllocationPercentage = 20m, StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1), Status = "Active", IsBillable = true,
            CreatedBy = "test", RowVersion = new byte[8]
        };
        _context.ProjectMembers.Add(existing);
        await _context.SaveChangesAsync();

        await _synchronizer.SynchronizeAsync(_project, null, SponsorUserId, "test");
        await _synchronizer.SynchronizeAsync(_project, null, SponsorUserId, "test");
        await _context.SaveChangesAsync();

        var activeMembership = await _context.ProjectMembers.SingleAsync(item => item.Status == "Active");
        Assert.Equal("Delivery Lead", activeMembership.Role);
        Assert.Equal(20m, activeMembership.AllocationPercentage);
        Assert.Null(activeMembership.Notes);
        Assert.Single(await _context.ProjectMembers.ToListAsync());

        var managedProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Name = "Managed sponsor", Code = "SP-2",
            Status = "Active", CreatedBy = "test", RowVersion = new byte[8]
        };
        _context.Projects.Add(managedProject);
        await _context.SaveChangesAsync();
        await _synchronizer.SynchronizeAsync(managedProject, null, SponsorUserId, "test");
        await _context.SaveChangesAsync();
        var managedRow = await _context.ProjectMembers.SingleAsync(item => item.ProjectId == managedProject.Id);
        Assert.Equal(ProjectRelationshipType.Sponsor, managedRow.Role);
        Assert.Equal(0m, managedRow.AllocationPercentage);
        Assert.True(ProjectSponsorMembershipSynchronizer.IsManagedMembership(managedRow));

        await _synchronizer.SynchronizeAsync(managedProject, SponsorUserId, null, "test");
        await _context.SaveChangesAsync();
        var inactiveManagedRow = await _context.ProjectMembers.SingleAsync(item => item.Id == managedRow.Id);
        Assert.Equal("Inactive", inactiveManagedRow.Status);
        Assert.False(inactiveManagedRow.IsDeleted);
        Assert.Equal("Delivery Lead", (await _context.ProjectMembers.SingleAsync(item => item.Id == existing.Id)).Role);
    }

    [Fact]
    public async Task SponsorWithoutLinkedResource_RemainsCanonicalWithoutSyntheticResourceOrMembership()
    {
        var userId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Name = "Governance only", Code = "SP-3",
            Status = "Active", CreatedBy = "test", RowVersion = new byte[8], SponsorId = userId
        };
        _context.Users.Add(new User
        {
            Id = userId, TenantId = TenantId, Email = "governance@example.test", PasswordHash = "hash",
            FirstName = "Governance", LastName = "Only", IsActive = true, RowVersion = new byte[8]
        });
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        Assert.Null(await _synchronizer.ValidateAssignmentAsync(userId, TenantId));
        var resourceCount = await _context.Resources.CountAsync();
        await _synchronizer.SynchronizeAsync(project, null, userId, "test");
        await _context.SaveChangesAsync();

        Assert.Equal(resourceCount, await _context.Resources.CountAsync());
        Assert.Empty(await _context.ProjectMembers.Where(item => item.ProjectId == project.Id).ToListAsync());
        Assert.Equal(userId, (await _context.Projects.SingleAsync(item => item.Id == project.Id)).SponsorId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
