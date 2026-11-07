using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.AIPlanning.Application;
using NEXUS.Modules.AIPlanning.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class AIPlanningTenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("a1100000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2200000-0000-0000-0000-000000000002");
    private static readonly Guid UserA = Guid.Parse("a1100000-0000-0000-0000-0000000000a1");
    private static readonly Guid UserB = Guid.Parse("b2200000-0000-0000-0000-0000000000b2");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private readonly AppDbContext _db;
    private readonly Guid _requestAId = Guid.NewGuid();
    private readonly Guid _sameTenantOtherUserRequestId = Guid.NewGuid();
    private readonly Guid _requestBId = Guid.NewGuid();

    public AIPlanningTenantIsolationTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SqliteTestAppDbContext(options);
        _db.Database.EnsureCreated();
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);

        var otherUserRequest = NewRequest(_sameTenantOtherUserRequestId, TenantA, UserB, "Private plan for another user");
        otherUserRequest.Result = "private generated result";
        otherUserRequest.Responses.Add(new AIPlanResponse
        {
            TenantId = TenantA,
            AIPlanRequest = otherUserRequest,
            Content = "private generated response"
        });

        _db.AIPlanRequests.AddRange(
            NewRequest(_requestAId, TenantA, UserA, "Tenant A plan"),
            otherUserRequest,
            NewRequest(_requestBId, TenantB, UserB, "Tenant B plan"));
        _db.SaveChanges();

        SetIdentity(TenantA, UserA);
    }

    [Fact]
    public async Task Reads_ReturnOnlyCurrentTenantAndOwnerRequests()
    {
        var service = new AIPlanningService(_db, new SuccessfulAIProvider(), _httpContextAccessor);

        var ownRequest = await service.GetByIdAsync(_requestAId);
        var otherUserRequest = await service.GetByIdAsync(_sameTenantOtherUserRequestId);
        var foreignRequest = await service.GetByIdAsync(_requestBId);
        var page = await service.GetPagedAsync(1, 20);

        Assert.True(ownRequest.IsSuccess, ownRequest.Error?.Message);
        Assert.Equal(_requestAId, ownRequest.Value!.Id);
        Assert.False(otherUserRequest.IsSuccess);
        Assert.Equal(404, otherUserRequest.Error!.StatusCode);
        Assert.False(foreignRequest.IsSuccess);
        Assert.Equal(404, foreignRequest.Error!.StatusCode);
        Assert.Single(page.Items);
        Assert.Equal(_requestAId, page.Items[0].Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GeneratePlan_StampsTenantAndActorFromAuthenticatedContext()
    {
        var service = new AIPlanningService(_db, new SuccessfulAIProvider(), _httpContextAccessor);

        var result = await service.GeneratePlanAsync(new PlanGenerationCommand(
            "New plan",
            "client-supplied context",
            Provider: "Mock"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TenantA, result.Value!.TenantId);
        var persistedRequest = await _db.AIPlanRequests
            .AsNoTracking()
            .Include(request => request.Responses)
            .SingleAsync(request => request.Id == result.Value.AIPlanRequestId);
        Assert.Equal(TenantA, persistedRequest.TenantId);
        Assert.Equal(UserA, persistedRequest.RequestedBy);
        Assert.Equal(TenantA, Assert.Single(persistedRequest.Responses).TenantId);
    }

    [Fact]
    public async Task MissingOrMalformedTenantContext_DeniesWritesAndReturnsNoHistory()
    {
        var service = new AIPlanningService(_db, new SuccessfulAIProvider(), _httpContextAccessor);
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserA.ToString())],
                authenticationType: "test"))
        };

        var noTenantPage = await service.GetPagedAsync(1, 20);
        var noTenantRead = await service.GetByIdAsync(_requestAId);
        var noTenantWrite = await service.GeneratePlanAsync(new PlanGenerationCommand("Denied", "context"));

        Assert.Empty(noTenantPage.Items);
        Assert.Equal(0, noTenantPage.TotalCount);
        Assert.False(noTenantRead.IsSuccess);
        Assert.False(noTenantWrite.IsSuccess);
        Assert.Equal(403, noTenantWrite.Error!.StatusCode);

        ((ClaimsIdentity)_httpContextAccessor.HttpContext.User.Identity!).AddClaim(new Claim("tenantId", "invalid-tenant"));
        var malformedWrite = await service.GeneratePlanAsync(new PlanGenerationCommand("Denied", "context"));
        Assert.False(malformedWrite.IsSuccess);
        Assert.Equal(403, malformedWrite.Error!.StatusCode);

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", TenantA.ToString())],
                authenticationType: "test"))
        };
        var noUserPage = await service.GetPagedAsync(1, 20);
        var noUserRead = await service.GetByIdAsync(_requestAId);
        var noUserWrite = await service.GeneratePlanAsync(new PlanGenerationCommand("Denied", "context"));

        Assert.Empty(noUserPage.Items);
        Assert.Equal(0, noUserPage.TotalCount);
        Assert.False(noUserRead.IsSuccess);
        Assert.False(noUserWrite.IsSuccess);
        Assert.Equal(403, noUserWrite.Error!.StatusCode);
    }

    private static AIPlanRequest NewRequest(Guid id, Guid tenantId, Guid requestedBy, string title) => new()
    {
        Id = id,
        TenantId = tenantId,
        RequestedBy = requestedBy,
        Title = title,
        Description = title,
        Context = "isolated fixture",
        Provider = "Mock",
        Status = "Completed",
        CreatedBy = requestedBy.ToString()
    };

    private void SetIdentity(Guid tenantId, Guid userId)
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString()), new Claim("sub", userId.ToString())],
                authenticationType: "test"))
        };
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
    }

    private sealed class SuccessfulAIProvider : IAIProvider
    {
        public Task<Result<string>> GeneratePlanAsync(string context, string? parameters = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<string>.Success($"Generated from {context}"));
    }
}
