using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Approvals.Application;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.Approvals.Tests;

public class P003SecurityRegressionTests
{
    private static ApprovalService CreateService(TestDbContext context, Mock<IAuditService> auditMock, Guid? tenantId = null)
    {
        var http = new Mock<IHttpContextAccessor>();
        if (tenantId.HasValue)
        {
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", tenantId.Value.ToString()),
                new Claim("permission", "ApprovalRequest.View"),
                new Claim("permission", "ApprovalRequest.Approve"),
                new Claim("permission", "ApprovalRequest.Reject"),
                new Claim("permission", "ApprovalRequest.Delegate"),
            }, "test"));
            http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = claims });
        }
        return new ApprovalService(context, auditMock.Object, http.Object, Mock.Of<IApprovalDelegationEligibilityLookup>());
    }

    [Fact]
    public async Task GetById_WithoutViewPermission_ReturnsForbidden()
    {
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var caller = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = caller, CurrentApproverId = Guid.NewGuid(), ApprovalLevel = 1, CreatedBy = caller.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var svc = CreateService(ctx, audit);
        var res = await svc.GetByIdAsync(req.Id, caller);
        Assert.False(res.IsSuccess);
        Assert.Equal(403, res.Error!.StatusCode);
    }

    [Fact]
    public async Task Approve_ByRequester_Rejected()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = requester, CurrentApproverId = requester, Status = ApprovalRequest.StatusSubmitted, ApprovalLevel = 1, CreatedBy = requester.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var svc = CreateService(ctx, audit, tenant);
        var res = await svc.ApproveAsync(req.Id, requester);
        Assert.False(res.IsSuccess);
        Assert.Equal(403, res.Error!.StatusCode);
    }

    [Fact]
    public async Task AuditFailure_AbortsMutation()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = requester, CurrentApproverId = approver, Status = ApprovalRequest.StatusSubmitted, ApprovalLevel = 1, CreatedBy = requester.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure(Error.Validation("audit down")));
        var svc = CreateService(ctx, audit, tenant);
        var res = await svc.ApproveAsync(req.Id, approver);
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task NoTenant_ReturnsForbidden()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = requester, CurrentApproverId = Guid.NewGuid(), ApprovalLevel = 1, CreatedBy = requester.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var otherTenant = Guid.NewGuid();
        var svc = CreateService(ctx, audit, otherTenant);
        var res = await svc.GetByIdAsync(req.Id, requester);
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Delegate_ToIneligibleTarget_ReturnsForbidden()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var target = Guid.NewGuid();
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = requester, CurrentApproverId = requester, Status = ApprovalRequest.StatusSubmitted, ApprovalLevel = 1, CreatedBy = requester.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var eligibility = new Mock<IApprovalDelegationEligibilityLookup>();
        eligibility.Setup(e => e.CheckAsync(target, tenant, "ApprovalRequest.Approve", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalDelegationEligibility(false, false, false, false));
        var http = new Mock<IHttpContextAccessor>();
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenant.ToString()),
            new Claim("permission", "ApprovalRequest.View"),
            new Claim("permission", "ApprovalRequest.Delegate"),
        }, "test"));
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = claims });
        var svc = new ApprovalService(ctx, audit.Object, http.Object, eligibility.Object);
        var res = await svc.DelegateAsync(req.Id, requester, target, "on leave");

        Assert.False(res.IsSuccess);
        Assert.Equal(403, res.Error!.StatusCode);
    }

    [Fact]
    public async Task Delegate_ToSameTenantActiveEligibleTarget_Succeeds()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var target = Guid.NewGuid();
        var (ctx, conn) = TestDbContext.CreateSqlite();
        await using var _ = conn; await using var __ = ctx;
        var req = new ApprovalRequest { Id = Guid.NewGuid(), TenantId = tenant, RequestedBy = requester, CurrentApproverId = requester, Status = ApprovalRequest.StatusSubmitted, ApprovalLevel = 1, CreatedBy = requester.ToString() };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();
        var reqId = req.Id;
        ctx.ChangeTracker.Clear();

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var eligibility = new Mock<IApprovalDelegationEligibilityLookup>();
        eligibility.Setup(e => e.CheckAsync(target, tenant, "ApprovalRequest.Approve", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalDelegationEligibility(true, true, true, true));
        var http = new Mock<IHttpContextAccessor>();
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenant.ToString()),
            new Claim("permission", "ApprovalRequest.View"),
            new Claim("permission", "ApprovalRequest.Delegate"),
        }, "test"));
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = claims });
        var svc = new ApprovalService(ctx, audit.Object, http.Object, eligibility.Object);
        var res = await svc.DelegateAsync(reqId, requester, target, "on leave");

        Assert.True(res.IsSuccess, res.Error?.Message ?? res.Error?.Code ?? res.Error?.ToString());
        Assert.Equal(target, res.Value!.CurrentApproverId);
    }

    [Fact]
    public async Task ConcurrencyConflict_ReturnsControlled409()
    {
        var tenant = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var ctx = new ConcurrencyFailingTestDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var req = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            RequestedBy = requester,
            CurrentApproverId = approver,
            Status = ApprovalRequest.StatusSubmitted,
            ApprovalLevel = 1,
            CreatedBy = requester.ToString()
        };
        ctx.ApprovalRequests.Add(req);
        await ctx.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var svc = CreateService(ctx, audit, tenant);

        // Arm the provider-independent concurrency failure only after the submitted request is persisted.
        ctx.FailNextSaveChangesAsync = true;
        var res = await svc.ApproveAsync(req.Id, approver);

        Assert.False(res.IsSuccess);
        Assert.Equal(409, res.Error!.StatusCode);
        audit.Verify(a => a.AppendAsync(
            It.Is<AuditEntry>(entry => entry.TenantId == tenant
                && entry.EntityType == "ApprovalRequest"
                && entry.EntityId == req.Id
                && entry.Action == "Approved"),
            It.IsAny<CancellationToken>()), Times.Once);

        // The failed SaveChanges must leave the persisted request and action history untouched.
        ctx.ChangeTracker.Clear();
        var persisted = await ctx.ApprovalRequests.Include(request => request.Actions)
            .SingleAsync(request => request.Id == req.Id);
        Assert.Equal(ApprovalRequest.StatusSubmitted, persisted.Status);
        Assert.Equal(approver, persisted.CurrentApproverId);
        Assert.Equal(1, persisted.ApprovalLevel);
        Assert.Empty(persisted.Actions);
        Assert.Equal(0, await ctx.ApprovalActions.CountAsync());
    }

    private sealed class ConcurrencyFailingTestDbContext(DbContextOptions<TestDbContext> options) : TestDbContext(options)
    {
        public bool FailNextSaveChangesAsync { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSaveChangesAsync)
            {
                FailNextSaveChangesAsync = false;
                throw new DbUpdateConcurrencyException("Simulated optimistic-concurrency conflict.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
