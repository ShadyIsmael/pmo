using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectOwnershipIntegrityTests : IDisposable
{
    [Fact]
    public async Task Create_rejects_portfolio_without_a_resolvable_organization()
    {
        var tenantId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            Name = "Unowned portfolio",
            Code = "PF-UNOWNED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        db.Currencies.Add(Currency(tenantId));
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Project without owner",
            Code = "PRJ-UNOWNED",
            PortfolioId = portfolioId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("active Organization in the current tenant");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_rejects_reassignment_to_a_portfolio_without_a_resolvable_organization()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        db.Projects.Add(Project(projectId, tenantId));
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            Name = "Unowned portfolio",
            Code = "PF-UNOWNED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        db.Currencies.Add(Currency(tenantId));
        await db.SaveChangesAsync();

        var result = await CreateService(db).UpdateAsync(projectId, new UpdateProjectDto
        {
            PortfolioId = portfolioId
        }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("ownership reassignment must resolve");
        (await db.Projects.SingleAsync(item => item.Id == projectId).ConfigureAwait(false)).PortfolioId.Should().BeNull();
    }

    [Fact]
    public async Task Update_allows_unrelated_edit_to_legacy_project_without_resolvable_organization()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var project = Project(projectId, tenantId);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var result = await CreateService(db).UpdateAsync(projectId, new UpdateProjectDto
        {
            Notes = "Legacy record updated without changing ownership."
        }, Guid.NewGuid().ToString());

        result.IsSuccess.Should().BeTrue();
        (await db.Projects.SingleAsync(item => item.Id == projectId).ConfigureAwait(false)).Notes
            .Should().Be("Legacy record updated without changing ownership.");
    }

    [Fact]
    public async Task Create_accepts_direct_portfolio_organization_ownership()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var organization = Organization(organizationId, tenantId);
        db.AddRange(organization, new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = "Owned portfolio",
            Code = "PF-OWNED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        await SeedLifecycleAsync(db, tenantId, organizationId);

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Direct portfolio project",
            Code = "PRJ-DIRECT",
            PortfolioId = portfolioId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value!.PortfolioId.Should().Be(portfolioId);
        (await db.Projects.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_rejects_department_only_ownership_without_a_portfolio()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var organization = Organization(organizationId, tenantId);
        var businessUnit = new BusinessUnit
        {
            Id = businessUnitId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Organization = organization,
            Name = "Business Unit",
            IsActive = true,
            CreatedBy = "ownership-test"
        };
        db.AddRange(organization, businessUnit, new Department
        {
            Id = departmentId,
            TenantId = tenantId,
            BusinessUnitId = businessUnitId,
            BusinessUnit = businessUnit,
            Name = "Department",
            IsActive = true,
            CreatedBy = "ownership-test"
        });
        await SeedLifecycleAsync(db, tenantId, organizationId);

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Department-owned project",
            Code = "PRJ-DEPARTMENT",
            DepartmentId = departmentId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        result.Error.Message.Should().Contain("Portfolio is required");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_rejects_department_only_ownership_even_when_the_department_is_out_of_scope()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        await SeedDepartmentOwnershipAsync(db, tenantId, organizationId, businessUnitId, departmentId);

        var result = await CreateScopedService(db, tenantId, new HashSet<Guid> { Guid.NewGuid() })
            .CreateAsync(new CreateProjectDto
            {
                Name = "Out-of-scope department project",
                Code = "PRJ-OUT-OF-SCOPE-DEPT",
                DepartmentId = departmentId,
                Currency = "USD"
            }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        result.Error.Message.Should().Contain("Portfolio is required");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_rejects_initial_project_manager_assignment_without_handover_initiation_permission()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "Project.Create" });

        var result = await CreateService(db, permissions.Object).CreateAsync(new CreateProjectDto
        {
            Name = "Unauthorized initial assignment",
            Code = "PRJ-UNAUTHORIZED-MANAGER",
            ProjectManagerId = candidateId,
            Currency = "USD"
        }, actorId.ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(403);
        result.Error.Message.Should().Contain("Handover.AdminInitiate");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_rejects_department_only_ownership_even_when_department_is_in_scope()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        await SeedDepartmentOwnershipAsync(db, tenantId, organizationId, businessUnitId, departmentId);

        var result = await CreateScopedService(db, tenantId, new HashSet<Guid> { organizationId })
            .CreateAsync(new CreateProjectDto
            {
                Name = "In-scope department project",
                Code = "PRJ-IN-SCOPE-DEPT",
                DepartmentId = departmentId,
                Currency = "USD"
            }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        result.Error.Message.Should().Contain("Portfolio is required");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_rejects_department_reassignment_outside_the_callers_organization_scope()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        db.Projects.Add(Project(projectId, tenantId));
        await SeedDepartmentOwnershipAsync(db, tenantId, organizationId, businessUnitId, departmentId);

        var result = await CreateScopedService(db, tenantId, new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid> { projectId })
            .UpdateAsync(projectId, new UpdateProjectDto { DepartmentId = departmentId }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(404);
        (await db.Projects.SingleAsync(item => item.Id == projectId).ConfigureAwait(false)).DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task Create_accepts_program_portfolio_organization_ownership()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var organization = Organization(organizationId, tenantId);
        var portfolio = new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = "Program portfolio",
            Code = "PF-PROGRAM",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        };
        db.AddRange(organization, portfolio, new ProgramEntity
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Portfolio = portfolio,
            Name = "Program",
            Code = "PGM-OWNED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        await SeedLifecycleAsync(db, tenantId, organizationId);

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Program project",
            Code = "PRJ-PROGRAM",
            ProgramId = programId,
            PortfolioId = portfolioId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProgramId.Should().Be(programId);
        result.Value.PortfolioId.Should().Be(portfolioId);
        (await db.Projects.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_rejects_program_and_portfolio_ownership_mismatch()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var programPortfolioId = Guid.NewGuid();
        var selectedPortfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var organization = Organization(organizationId, tenantId);
        var programPortfolio = new PortfolioEntity
        {
            Id = programPortfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = "Program portfolio",
            Code = "PF-PROGRAM",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        };
        db.AddRange(organization, programPortfolio, new PortfolioEntity
        {
            Id = selectedPortfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = "Different portfolio",
            Code = "PF-OTHER",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        }, new ProgramEntity
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = programPortfolioId,
            Portfolio = programPortfolio,
            Name = "Program",
            Code = "PGM-OWNED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        await SeedLifecycleAsync(db, tenantId, organizationId);

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Mismatched project",
            Code = "PRJ-PROGRAM-MISMATCH",
            ProgramId = programId,
            PortfolioId = selectedPortfolioId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("must match");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_rejects_program_without_an_explicit_portfolio_selection()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase();
        var organization = Organization(organizationId, tenantId);
        var portfolio = new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = "Selected hierarchy portfolio",
            Code = "PF-SELECTED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        };
        db.AddRange(organization, portfolio, new ProgramEntity
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Portfolio = portfolio,
            Name = "Owned program",
            Code = "PGM-SELECTED",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "ownership-test"
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new CreateProjectDto
        {
            Name = "Program without selected Portfolio",
            Code = "PRJ-PROGRAM-NO-PORTFOLIO",
            ProgramId = programId,
            Currency = "USD"
        }, Guid.NewGuid().ToString());

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(400);
        result.Error.Message.Should().Contain("Portfolio is required");
        (await db.Projects.CountAsync()).Should().Be(0);
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static AppDbContext CreateDatabase()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-ownership-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static ProjectService CreateService(AppDbContext db, IUserEffectivePermissionService? effectivePermissions = null)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new ProjectService(
            db,
            audit.Object,
            new HttpContextAccessor(),
            new ProjectManagerMembershipSynchronizer(db),
            effectivePermissions: effectivePermissions);
    }

    private static ProjectService CreateScopedService(
        AppDbContext db,
        Guid tenantId,
        IReadOnlySet<Guid> organizationIds,
        IReadOnlySet<Guid>? projectIds = null)
    {
        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(service => service.GetEffectivePermissionsAsync(
                It.IsAny<Guid>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "Project.Create" });
        var contextAccessor = new ManagementContextAccessor();
        contextAccessor.Set(new ManagementContext(
            Guid.NewGuid(), tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds ?? new HashSet<Guid>())
            {
                OrganizationIds = organizationIds
            }));
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new ProjectService(
            db,
            audit.Object,
            new HttpContextAccessor(),
            new ProjectManagerMembershipSynchronizer(db),
            contextAccessor,
            effectivePermissions: effectivePermissions.Object);
    }

    private static async Task SeedDepartmentOwnershipAsync(
        AppDbContext db, Guid tenantId, Guid organizationId, Guid businessUnitId, Guid departmentId)
    {
        var organization = Organization(organizationId, tenantId);
        var businessUnit = new BusinessUnit
        {
            Id = businessUnitId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Organization = organization,
            Name = "Scoped Business Unit",
            IsActive = true,
            CreatedBy = "ownership-test"
        };
        db.AddRange(organization, businessUnit, new Department
        {
            Id = departmentId,
            TenantId = tenantId,
            BusinessUnitId = businessUnitId,
            BusinessUnit = businessUnit,
            Name = "Scoped Department",
            IsActive = true,
            CreatedBy = "ownership-test"
        });
        await SeedLifecycleAsync(db, tenantId, organizationId);
    }

    private static Project Project(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = "Legacy project",
        Code = "PRJ-LEGACY",
        Currency = "USD",
        CreatedBy = "ownership-test"
    };

    private static OrganizationEntity Organization(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = "Owning Organization",
        IsActive = true,
        CreatedBy = "ownership-test"
    };

    private static Currency Currency(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Code = "USD",
        Name = "US Dollar",
        Symbol = "$",
        ExchangeRate = 1m,
        CreatedBy = "ownership-test"
    };

    private static async Task SeedLifecycleAsync(AppDbContext db, Guid tenantId, Guid organizationId)
    {
        db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
        {
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = "INITIATION",
            NormalizedCode = "INITIATION",
            NameEn = "Initiation",
            NameAr = "Initiation",
            IsDefault = true,
            IsActive = true,
            CreatedBy = "ownership-test"
        });
        db.ProjectStatusCatalogEntries.Add(new ProjectStatusCatalogEntry
        {
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = "PLANNING",
            NormalizedCode = "PLANNING",
            NameEn = "Planning",
            NameAr = "Planning",
            IsDefault = true,
            IsActive = true,
            CreatedBy = "ownership-test"
        });
        db.Currencies.Add(Currency(tenantId));
        await db.SaveChangesAsync();
    }
}
