using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests;

public class ProjectChangeRequestServiceTests
{
    private readonly Mock<DbContext> _mockContext;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ProjectChangeRequestWorkflowService _service;

    private readonly Guid _tenantA_Id = Guid.NewGuid();
    private readonly Guid _userA_Id = Guid.NewGuid();
    private readonly Guid _projectId_A = Guid.NewGuid();

    public ProjectChangeRequestServiceTests()
    {
        _mockContext = new Mock<DbContext>();
        _mockAuditService = new Mock<IAuditService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _service = new ProjectChangeRequestWorkflowService(_mockContext.Object, _mockAuditService.Object, _mockHttpContextAccessor.Object,
            null!, null!, null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task CreateAsync_MissingTenantClaim_ReturnsUnauthorized()
    {
        // Arrange: No tenantId claim
        SetupHttpContext(null, _userA_Id.ToString());

        var dto = new CreateProjectChangeRequestDto
        {
            Type = ProjectChangeRequest.ChangeTypes.Baseline,
            ProjectId = _projectId_A,
            Reason = "Test reason",
            Justification = "Test justification"
        };

        // Act
        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task CreateAsync_MissingUserClaim_ReturnsUnauthorized()
    {
        // Arrange: No userId claim
        SetupHttpContext(_tenantA_Id.ToString(), null);

        var dto = new CreateProjectChangeRequestDto
        {
            Type = ProjectChangeRequest.ChangeTypes.Baseline,
            ProjectId = _projectId_A,
            Reason = "Test reason",
            Justification = "Test justification"
        };

        // Act
        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetByIdAsync_MissingTenantClaim_ReturnsUnauthorized()
    {
        // Arrange: No tenantId claim
        SetupHttpContext(null, _userA_Id.ToString());

        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetByProjectIdAsync_MissingTenantClaim_ReturnsUnauthorized()
    {
        // Arrange: No tenantId claim
        SetupHttpContext(null, _userA_Id.ToString());

        // Act
        var result = await _service.GetByProjectIdAsync(_projectId_A);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error!.Code, ignoreCase: true);
    }

    private void SetupHttpContext(string? tenantId, string? userId)
    {
        var claims = new List<System.Security.Claims.Claim>();
        if (!string.IsNullOrEmpty(tenantId))
        {
            claims.Add(new System.Security.Claims.Claim("tenantId", tenantId));
        }
        if (!string.IsNullOrEmpty(userId))
        {
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        }
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }
}
