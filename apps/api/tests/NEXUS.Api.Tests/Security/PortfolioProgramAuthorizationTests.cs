using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Management;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel.Management;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PortfolioProgramAuthorizationTests
{
    [Fact]
    public async Task Create_rejects_initial_program_manager_assignment_without_handover_permission()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = "Program", PortfolioId = Guid.NewGuid(), ProgramManagerId = Guid.NewGuid(), Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Contains("Handover.AdminInitiate", result.Error?.Message);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    [Fact]
    public async Task Update_rejects_first_program_manager_assignment_without_handover_permission()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        db.Programs.Add(new ProgramEntity
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Program",
            Status = "Planning",
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId, programId));

        var result = await service.UpdateAsync(programId, new SaveProgramDto
        {
            Name = "Program", PortfolioId = portfolioId, ProgramManagerId = Guid.NewGuid(), Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Contains("Handover.AdminInitiate", result.Error?.Message);
        Assert.Null((await db.Programs.SingleAsync(item => item.Id == programId)).ProgramManagerId);
    }

    private static ProgramAuthorizationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ProgramAuthorizationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IHttpContextAccessor CreateHttpContextAccessor(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("permission", "Program.Create"),
            new Claim("permission", "Program.Edit"),
            new Claim("permission", "Portfolio.View")
        ], "test"));
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private static ManagementContextAccessor CreateManagementContextAccessor(
        Guid tenantId,
        Guid userId,
        Guid? scopedProgramId = null)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProgramManager,
            ManagementViewMode.Native,
            null,
            scopedProgramId,
            null,
            new ManagementScope(
                new HashSet<Guid>(),
                scopedProgramId.HasValue ? new HashSet<Guid> { scopedProgramId.Value } : new HashSet<Guid>(),
                new HashSet<Guid>())));
        return accessor;
    }

    private sealed class ProgramAuthorizationDbContext(DbContextOptions<ProgramAuthorizationDbContext> options) : DbContext(options)
    {
        public DbSet<ProgramEntity> Programs => Set<ProgramEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramEntity>().Ignore(program => program.Portfolio);
        }
    }
}
