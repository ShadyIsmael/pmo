using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public class ProjectChangeRequestServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ProjectChangeRequestService _service;

    private static readonly Guid _tenantA_Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid _tenantB_Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid _userA_Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid _userB_Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid _projectId_A = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid _projectId_B = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid _pcrId_A = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid _pcrId_B = Guid.Parse("20000000-0000-0000-0000-000000000004");

    public ProjectChangeRequestServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);

        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        SeedData();

        _mockAuditService = new Mock<IAuditService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _service = new ProjectChangeRequestService(_context, _mockAuditService.Object, _mockHttpContextAccessor.Object);
    }

    private void SeedData()
    {
        var users = new List<User>
        {
            new User { Id = _userA_Id, TenantId = _tenantA_Id, Email = "userA@tenantA.example", PasswordHash = "hash", FirstName = "UserA", LastName = "TenantA", RowVersion = new byte[8] },
            new User { Id = _userB_Id, TenantId = _tenantB_Id, Email = "userB@tenantB.example", PasswordHash = "hash", FirstName = "UserB", LastName = "TenantB", RowVersion = new byte[8] }
        };

        var projects = new List<Project>
        {
            new Project { Id = _projectId_A, TenantId = _tenantA_Id, Name = "Project A Name", Code = "PA", Description = "Description A", Status = "Active", Currency = "GBP", CreatedBy = _userA_Id.ToString(), RowVersion = new byte[8] },
            new Project { Id = _projectId_B, TenantId = _tenantB_Id, Name = "Project B Name", Code = "PB", Description = "Description B", Status = "Active", Currency = "GBP", CreatedBy = _userB_Id.ToString(), RowVersion = new byte[8] }
        };

        var pcrs = new List<ProjectChangeRequest>
        {
            new ProjectChangeRequest
            {
                Id = _pcrId_A,
                TenantId = _tenantA_Id,
                ProjectId = _projectId_A,
                Number = 1,
                Type = ProjectChangeRequest.ChangeTypes.Administrative,
                Reason = "Update project governance",
                Status = ProjectChangeRequest.Statuses.Pending,
                RequestedBy = _userA_Id,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = _userA_Id.ToString(),
                RowVersion = new byte[8]
            },
            new ProjectChangeRequest
            {
                Id = _pcrId_B,
                TenantId = _tenantB_Id,
                ProjectId = _projectId_B,
                Number = 1,
                Type = ProjectChangeRequest.ChangeTypes.Baseline,
                Reason = "Update project baseline",
                Status = ProjectChangeRequest.Statuses.Pending,
                RequestedBy = _userB_Id,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = _userB_Id.ToString(),
                RowVersion = new byte[8]
            }
        };

        _context.Users.AddRange(users);
        _context.Projects.AddRange(projects);
        _context.ProjectChangeRequests.AddRange(pcrs);
        _context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private void SetupHttpContext(string tenantId, string userId)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("tenantId", tenantId),
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        AppDbContext.ConfigureTenantResolution(_mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task CreateAsync_AuthenticatedSucceeds()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var dto = new CreateProjectChangeRequestDto
        {
            ProjectId = _projectId_A,
            Type = ProjectChangeRequest.ChangeTypes.Baseline,
            Reason = "Align baseline to budget",
            Justification = "Approved budget changes require a baseline update"
        };

        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Number);
    }

    [Fact]
    public async Task CreateAsync_AssignsSequentialNumberPerProject()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var dto = new CreateProjectChangeRequestDto
        {
            ProjectId = _projectId_A,
            Type = ProjectChangeRequest.ChangeTypes.Administrative,
            Reason = "Update governance",
            Justification = "Governance change"
        };

        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Number);
    }

    [Fact]
    public async Task CreateAsync_DuplicateType_PendingNoConflict()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        // Two pending Administrative change requests for the same project are allowed (header-only model).
        var dto = new CreateProjectChangeRequestDto
        {
            ProjectId = _projectId_A,
            Type = ProjectChangeRequest.ChangeTypes.Administrative,
            Reason = "Another governance update",
            Justification = "Additional governance change"
        };

        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Number);
    }

    [Fact]
    public async Task GetByIdAsync_SameTenantAccess_Succeeds()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByIdAsync(_pcrId_A);

        Assert.True(result.IsSuccess);
        Assert.Equal(_pcrId_A, result.Value!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenantAccess_ReturnsNotFound()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByIdAsync(_pcrId_B);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound().Code, result.Error!.Code);
    }

    [Fact]
    public async Task GetByProjectIdAsync_SameTenantAccess_Succeeds()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByProjectIdAsync(_projectId_A);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Contains(result.Value!, pcr => pcr.Id == _pcrId_A);
    }

    [Fact]
    public async Task GetByProjectIdAsync_CrossTenantAccess_ReturnsNotFound()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByProjectIdAsync(_projectId_B);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound().Code, result.Error!.Code);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}