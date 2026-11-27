using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.ProjectManagement.Api;
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
using TenantEntity = NEXUS.Modules.Organization.Domain.Tenant;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectInitialPhaseAssignmentTests
{
    private const string PhaseCode = "pre-initiation";
    private const string NormalizedPhaseCode = "PRE-INITIATION";
    private const string PhaseNameEn = "Pre-Initiation";
    private const string PhaseNameAr = "\u0645\u0631\u062D\u0644\u0629 \u0645\u0627 \u0642\u0628\u0644 \u0627\u0644\u0628\u062F\u0621";

    [Fact]
    public async Task CreateAsync_ProvisioningMissingOrganizationPhase_AssignsAndReturnsLocalizedPhase()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: false);
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var created = await fixture.CreateProjectAsync();

        Assert.True(created.IsSuccess, created.Error?.Message);
        var createdProject = created.Value ?? throw new InvalidOperationException("Project creation returned no DTO.");
        var project = await fixture.RootDb.Projects.SingleAsync(item => item.Id == createdProject.Id);
        var phase = await fixture.RootDb.ProjectPhaseCatalogEntries.SingleAsync(item => item.Id == project.ProjectPhaseId);
        Assert.Equal(fixture.OrganizationId, phase.OrganizationId);
        Assert.Equal(PhaseNameEn, createdProject.PhaseNameEn);
        Assert.Equal(PhaseNameAr, createdProject.PhaseNameAr);
        Assert.Equal(phase.Id, createdProject.PhaseId);
        Assert.Equal(PhaseCode, project.Phase);
    }

    [Fact]
    public async Task CreateAsync_ExistingOrganizationPhase_ReusesItsIdentity()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: true);
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var created = await fixture.CreateProjectAsync();

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(fixture.PhaseId, created.Value!.PhaseId);
        Assert.Equal(1, await fixture.RootDb.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId
                && item.OrganizationId == fixture.OrganizationId
                && item.NormalizedCode == NormalizedPhaseCode));
    }

    [Fact]
    public async Task CreateAsync_ConcurrentProjects_AssignTheSameOrganizationPhase()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: false);
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();

        var results = await Task.WhenAll(
            fixture.CreateProjectAsync(firstContext, "Concurrent A"),
            fixture.CreateProjectAsync(secondContext, "Concurrent B"));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        var phaseIds = results.Select(result => result.Value!.PhaseId).Distinct().ToArray();
        Assert.Single(phaseIds);
        Assert.NotNull(phaseIds[0]);
        Assert.Equal(1, await fixture.RootDb.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId
                && item.OrganizationId == fixture.OrganizationId
                && item.NormalizedCode == NormalizedPhaseCode));
        Assert.Equal(2, await fixture.RootDb.Projects.CountAsync(item => item.TenantId == fixture.TenantId));
    }

    [Fact]
    public async Task CreateAsync_UnresolvableOrganization_DoesNotPersistProject()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: false, portfolioHasOrganization: false);
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.CreateProjectAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(0, await fixture.RootDb.Projects.CountAsync(item => item.TenantId == fixture.TenantId));
        Assert.Equal(0, await fixture.RootDb.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId));
    }

    [Fact]
    public async Task CreateEndpoint_ProjectCreateOnlyOrganizationRole_CreatesPortfolioOnlyProjectAndAssignsPreInitiation()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: false);
        await fixture.AddOrganizationScopedProjectCreatorAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var httpContext = fixture.CreateAuthenticatedHttpContext("Project.Create");
        var managementAccessor = fixture.CreateProjectManagerContext();
        await using var db = fixture.CreateContext();
        var service = fixture.CreateProjectService(
            db,
            httpContext,
            managementAccessor,
            new UserOrganizationScopeLookup(db));
        var controller = Fixture.CreateProjectController(service, httpContext);

        var action = await controller.Create(new CreateProjectDto
        {
            Name = "Organization-scoped project",
            Code = $"P-{Guid.NewGuid():N}"[..14],
            PortfolioId = fixture.PortfolioId,
            Currency = "USD"
        }, CancellationToken.None);

        Assert.Equal(["Project.Create"], httpContext.User.FindAll("permission").Select(claim => claim.Value));
        var response = Assert.IsType<CreatedAtActionResult>(action);
        var created = Assert.IsType<ProjectDto>(response.Value);
        var project = await fixture.RootDb.Projects.SingleAsync(item => item.Id == created.Id);
        Assert.Equal(fixture.PortfolioId, project.PortfolioId);
        Assert.Null(project.ProgramId);
        Assert.Equal(PhaseCode, project.Phase);
        Assert.NotNull(project.ProjectPhaseId);
        var phase = await fixture.RootDb.ProjectPhaseCatalogEntries.SingleAsync(item => item.Id == project.ProjectPhaseId);
        Assert.Equal(fixture.OrganizationId, phase.OrganizationId);
        Assert.True(phase.IsDefault);
    }

    [Fact]
    public async Task ProjectCreateOnlyContextualLookups_ReturnOnlyOrganizationScopedPortfolioAndPrograms()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: true);
        await fixture.AddOrganizationScopedProjectCreatorAsync();
        var otherOrganizationId = Guid.NewGuid();
        var otherPortfolioId = Guid.NewGuid();
        var allowedProgramId = Guid.NewGuid();
        var otherProgramId = Guid.NewGuid();
        fixture.RootDb.Organizations.Add(new OrganizationEntity
        {
            Id = otherOrganizationId,
            TenantId = fixture.TenantId,
            Name = "Other organization",
            IsActive = true,
            CreatedBy = fixture.ActorId.ToString("D")
        });
        fixture.RootDb.Portfolios.Add(new PortfolioEntity
        {
            Id = otherPortfolioId,
            TenantId = fixture.TenantId,
            OrganizationId = otherOrganizationId,
            Name = "Other portfolio",
            Code = "OTHER-PORT",
            Currency = "USD",
            Status = "Active",
            CreatedBy = fixture.ActorId.ToString("D")
        });
        fixture.RootDb.Programs.AddRange(
            new ProgramEntity
            {
                Id = allowedProgramId,
                TenantId = fixture.TenantId,
                PortfolioId = fixture.PortfolioId,
                Name = "Allowed program",
                Code = "ALLOWED-PROG",
                Currency = "USD",
                Status = "Active",
                CreatedBy = fixture.ActorId.ToString("D")
            },
            new ProgramEntity
            {
                Id = otherProgramId,
                TenantId = fixture.TenantId,
                PortfolioId = otherPortfolioId,
                Name = "Other program",
                Code = "OTHER-PROG",
                Currency = "USD",
                Status = "Active",
                CreatedBy = fixture.ActorId.ToString("D")
            });
        await fixture.RootDb.SaveChangesAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var httpContext = fixture.CreateAuthenticatedHttpContext("Project.Create");
        var managementAccessor = fixture.CreateProjectManagerContext();
        await using var db = fixture.CreateContext();
        var organizationScope = new UserOrganizationScopeLookup(db);
        var portfolioService = new PortfolioService(
            db,
            new HttpContextAccessor { HttpContext = httpContext },
            managementAccessor,
            organizationScopeLookup: organizationScope);
        var programService = new ProgramService(
            db,
            new HttpContextAccessor { HttpContext = httpContext },
            managementAccessor,
            organizationScopeLookup: organizationScope);

        var portfolios = await portfolioService.GetProjectLookupAsync(1, 200, null, null);
        var allPrograms = await programService.GetProjectLookupAsync(1, 200, null, null, null);
        var selectedPortfolioPrograms = await programService.GetProjectLookupAsync(1, 200, null, null, fixture.PortfolioId);

        Assert.Equal([fixture.PortfolioId], portfolios.Items.Select(item => item.Id));
        Assert.Equal([allowedProgramId], allPrograms.Items.Select(item => item.Id));
        Assert.Equal([allowedProgramId], selectedPortfolioPrograms.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task ProjectCreateOnlyActor_CannotCreateWithForeignOrganizationOrMismatchedProgramPortfolio()
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase: true);
        await fixture.AddOrganizationScopedProjectCreatorAsync();
        var otherOrganizationId = Guid.NewGuid();
        var otherPortfolioId = Guid.NewGuid();
        var allowedProgramId = Guid.NewGuid();
        fixture.RootDb.Organizations.Add(new OrganizationEntity
        {
            Id = otherOrganizationId,
            TenantId = fixture.TenantId,
            Name = "Other organization",
            IsActive = true,
            CreatedBy = fixture.ActorId.ToString("D")
        });
        fixture.RootDb.Portfolios.Add(new PortfolioEntity
        {
            Id = otherPortfolioId,
            TenantId = fixture.TenantId,
            OrganizationId = otherOrganizationId,
            Name = "Other portfolio",
            Code = "OTHER-PORT",
            Currency = "USD",
            Status = "Active",
            CreatedBy = fixture.ActorId.ToString("D")
        });
        fixture.RootDb.Programs.Add(new ProgramEntity
        {
            Id = allowedProgramId,
            TenantId = fixture.TenantId,
            PortfolioId = fixture.PortfolioId,
            Name = "Allowed program",
            Code = "ALLOWED-PROG",
            Currency = "USD",
            Status = "Active",
            CreatedBy = fixture.ActorId.ToString("D")
        });
        await fixture.RootDb.SaveChangesAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var httpContext = fixture.CreateAuthenticatedHttpContext("Project.Create");
        var managementAccessor = fixture.CreateProjectManagerContext();
        await using var db = fixture.CreateContext();
        var service = fixture.CreateProjectService(
            db,
            httpContext,
            managementAccessor,
            new UserOrganizationScopeLookup(db));
        var controller = Fixture.CreateProjectController(service, httpContext);

        var foreignPortfolioResponse = await controller.Create(new CreateProjectDto
        {
            Name = "Foreign organization project",
            Code = $"P-{Guid.NewGuid():N}"[..14],
            PortfolioId = otherPortfolioId,
            Currency = "USD"
        }, CancellationToken.None);
        var mismatchedHierarchyResponse = await controller.Create(new CreateProjectDto
        {
            Name = "Mismatched hierarchy project",
            Code = $"P-{Guid.NewGuid():N}"[..14],
            ProgramId = allowedProgramId,
            PortfolioId = otherPortfolioId,
            Currency = "USD"
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(foreignPortfolioResponse);
        Assert.IsType<BadRequestObjectResult>(mismatchedHierarchyResponse);
        Assert.Empty(await fixture.RootDb.Projects.Where(item => item.TenantId == fixture.TenantId).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateEndpoint_ThenReloadShell_ReturnsPersistedOrganizationPhase(bool seedPhase)
    {
        await using var fixture = await Fixture.CreateAsync(seedPhase);
        using var tenantScope = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        ProjectDto createdProject;
        CreatedAtActionResult createResponse;
        var createHttpContext = fixture.CreateAuthenticatedHttpContext();
        await using (var createDb = fixture.CreateContext())
        {
            createHttpContext.Request.Method = HttpMethods.Post;
            createHttpContext.Request.Path = "/api/projects";
            createHttpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] =
                ManagementPerspective.PortfolioManager.ToString();
            var createManagementAccessor = new ManagementContextAccessor();
            var createManagementContext = fixture.CreateManagementContext();
            var createService = fixture.CreateProjectService(createDb, createHttpContext, createManagementAccessor);
            var createController = Fixture.CreateProjectController(createService, createHttpContext);
            var createDto = new CreateProjectDto
            {
                Name = "Create endpoint reload project",
                Code = $"P-{Guid.NewGuid():N}"[..14],
                PortfolioId = fixture.PortfolioId,
                Currency = "USD"
            };
            IActionResult? createAction = null;
            var createManagementMiddleware = new ManagementContextMiddleware(async _ =>
            {
                createAction = await createController.Create(createDto, CancellationToken.None);
            });
            await createManagementMiddleware.InvokeAsync(
                createHttpContext,
                fixture.CreateManagementScopeResolver(createManagementContext),
                createManagementAccessor,
                NullLogger<ManagementContextMiddleware>.Instance,
                Mock.Of<IWebHostEnvironment>());

            Assert.NotNull(createAction);
            if (createAction is BadRequestObjectResult badRequest)
            {
                throw new InvalidOperationException(
                    $"ProjectController.Create rejected the fixture request: {System.Text.Json.JsonSerializer.Serialize(badRequest.Value)}");
            }
            createResponse = Assert.IsType<CreatedAtActionResult>(createAction);
            createdProject = Assert.IsType<ProjectDto>(createResponse.Value);
            Assert.Equal(createdProject.PublicId, createResponse.RouteValues!["id"]);
            Assert.NotEqual(createdProject.Id, createdProject.PublicId);
        }

        await using var reloadDb = fixture.CreateContext();
        var persistedProject = await reloadDb.Projects.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == fixture.TenantId && item.Id == createdProject.Id);
        Assert.NotNull(persistedProject.ProjectPhaseId);
        Assert.Equal(PhaseCode, persistedProject.Phase);

        var persistedPhase = await reloadDb.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == fixture.TenantId && item.Id == persistedProject.ProjectPhaseId);
        Assert.Equal(fixture.OrganizationId, persistedPhase.OrganizationId);
        Assert.True(persistedPhase.IsActive);
        Assert.True(persistedPhase.IsDefault);
        Assert.False(persistedPhase.IsDeleted);
        Assert.Equal(1, await reloadDb.ProjectPhaseCatalogEntries.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId
                && item.OrganizationId == fixture.OrganizationId
                && item.NormalizedCode == NormalizedPhaseCode
                && !item.IsDeleted));

        var reloadHttpContext = fixture.CreateAuthenticatedHttpContext();
        reloadHttpContext.Request.Path = $"/api/projects/{createdProject.PublicId:D}/shell";
        reloadHttpContext.Request.Method = HttpMethods.Get;
        reloadHttpContext.Request.Headers[ManagementContextMiddleware.PerspectiveHeader] =
            ManagementPerspective.PortfolioManager.ToString();
        reloadHttpContext.Request.RouteValues["id"] = createdProject.PublicId.ToString("D");
        reloadHttpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Project" }),
            "GET /api/projects/{id}/shell"));

        var reloadManagementAccessor = new ManagementContextAccessor();
        var reloadManagementContext = fixture.CreateManagementContext([createdProject.Id]);
        var reloadService = fixture.CreateProjectService(reloadDb, reloadHttpContext, reloadManagementAccessor);
        var reloadController = Fixture.CreateProjectController(reloadService, reloadHttpContext);
        IActionResult? shellResponse = null;
        var managementMiddleware = new ManagementContextMiddleware(async context =>
        {
            var internalProjectId = Guid.Parse(Convert.ToString(context.Request.RouteValues["id"])!);
            shellResponse = await reloadController.GetShell(internalProjectId, CancellationToken.None);
        });
        var routingMiddleware = new ProjectPublicIdRoutingMiddleware(context => managementMiddleware.InvokeAsync(
            context,
            fixture.CreateManagementScopeResolver(reloadManagementContext),
            reloadManagementAccessor,
            NullLogger<ManagementContextMiddleware>.Instance,
            Mock.Of<IWebHostEnvironment>()));

        await routingMiddleware.InvokeAsync(
            reloadHttpContext,
            reloadDb,
            NullLogger<ProjectPublicIdRoutingMiddleware>.Instance,
            Mock.Of<IWebHostEnvironment>());

        var shellDto = Assert.IsType<ProjectDto>(Assert.IsType<OkObjectResult>(shellResponse).Value);
        Assert.Equal(createdProject.Id, reloadHttpContext.Request.RouteValues["id"]);
        Assert.Equal(persistedPhase.Id, shellDto.PhaseId);
        Assert.Equal(PhaseCode, shellDto.Phase);
        Assert.Equal(PhaseNameEn, shellDto.PhaseNameEn);
        Assert.Equal(PhaseNameAr, shellDto.PhaseNameAr);
        var serializedShell = System.Text.Json.JsonSerializer.SerializeToElement(
            shellDto,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(PhaseCode, serializedShell.GetProperty("phase").GetString());
        Assert.Equal(persistedPhase.Id.ToString("D"), serializedShell.GetProperty("phaseId").GetString());
        Assert.Equal(PhaseNameEn, serializedShell.GetProperty("phaseNameEn").GetString());
        Assert.Equal(PhaseNameAr, serializedShell.GetProperty("phaseNameAr").GetString());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _rootConnection;
        private readonly DbContextOptions<AppDbContext> _options;

        public AppDbContext RootDb { get; }
        public Guid TenantId { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid ActorId { get; private set; } = Guid.NewGuid();
        public Guid PortfolioId { get; } = Guid.NewGuid();
        public Guid? PhaseId { get; private set; }

        private Fixture(SqliteConnection rootConnection, DbContextOptions<AppDbContext> options,
            AppDbContext rootDb)
        {
            _rootConnection = rootConnection;
            _options = options;
            RootDb = rootDb;
        }

        public static async Task<Fixture> CreateAsync(bool seedPhase, bool portfolioHasOrganization = true)
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var tenantAccessor = CreateTenantAccessor(tenantId, actorId);
            AppDbContext.ConfigureTenantResolution(tenantAccessor);

            var databaseName = $"preinit-{Guid.NewGuid():N}";
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databaseName,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 30
            }.ToString();
            var rootConnection = new SqliteConnection(connectionString);
            await rootConnection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
            var rootDb = new SqliteTestAppDbContext(options);
            await rootDb.Database.EnsureCreatedAsync();

            var fixture = new Fixture(rootConnection, options, rootDb)
            {
                TenantId = tenantId,
                ActorId = actorId
            };
            rootDb.Tenants.Add(new TenantEntity
            {
                Id = tenantId,
                Code = $"TENANT-{tenantId:N}"[..18],
                DisplayName = "Phase test tenant",
                Status = TenantLifecycleStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actorId.ToString("D")
            });
            rootDb.Organizations.Add(new OrganizationEntity
            {
                Id = fixture.OrganizationId,
                TenantId = tenantId,
                Name = "Phase test organization",
                IsActive = true,
                CreatedBy = actorId.ToString("D")
            });
            rootDb.Currencies.Add(new Currency
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                IsBase = true
            });
            rootDb.Portfolios.Add(new PortfolioEntity
            {
                Id = fixture.PortfolioId,
                TenantId = tenantId,
                OrganizationId = portfolioHasOrganization ? fixture.OrganizationId : null,
                Name = "Phase test portfolio",
                Code = "PHASE-PORT",
                Currency = "USD",
                Status = "Active",
                CreatedBy = actorId.ToString("D")
            });
            await rootDb.SaveChangesAsync();

            if (seedPhase)
            {
                var provisioned = await CreateLifecycleService(rootDb).EnsurePreInitiationPhaseAsync(
                    tenantId, fixture.OrganizationId, actorId);
                Assert.True(provisioned.IsSuccess, provisioned.Error?.Message);
                fixture.PhaseId = provisioned.Value!.Id;
            }

            return fixture;
        }

        public AppDbContext CreateContext() => new SqliteTestAppDbContext(_options);

        public DefaultHttpContext CreateAuthenticatedHttpContext(params string[] permissions) => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D")),
                    new Claim("tenantId", TenantId.ToString("D"))
                }.Concat(permissions.Select(permission => new Claim("permission", permission))),
                "phase-test"))
        };

        public ManagementContextAccessor CreateProjectManagerContext()
        {
            var accessor = new ManagementContextAccessor();
            accessor.Set(new ManagementContext(
                ActorId,
                TenantId,
                ManagementPerspective.ProjectManager,
                ManagementViewMode.Native,
                null,
                null,
                null,
                new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())));
            return accessor;
        }

        public async Task AddOrganizationScopedProjectCreatorAsync()
        {
            var roleId = Guid.NewGuid();
            RootDb.Users.Add(new User
            {
                Id = ActorId,
                TenantId = TenantId,
                Email = "project.creator@example.test",
                FirstName = "Project",
                LastName = "Creator",
                IsActive = true,
                CreatedBy = ActorId.ToString("D")
            });
            RootDb.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TenantId,
                Name = "Project Manager",
                Category = "Management",
                RoleKey = "project-manager",
                OrganizationId = OrganizationId,
                CreatedBy = ActorId.ToString("D")
            });
            RootDb.UserRoles.Add(new UserRole
            {
                TenantId = TenantId,
                UserId = ActorId,
                RoleId = roleId,
                CreatedBy = ActorId.ToString("D")
            });
            await RootDb.SaveChangesAsync();
        }

        public ProjectService CreateProjectService(AppDbContext context, HttpContext httpContext)
            => CreateProjectService(context, httpContext, managementContext: null);

        public ProjectService CreateProjectService(
            AppDbContext context,
            HttpContext httpContext,
            IManagementContextAccessor? managementContext,
            IUserOrganizationScopeLookup? organizationScopeLookup = null)
        {
            var audit = new Mock<IAuditService>();
            audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            var lookup = new ProjectOrganizationLookup(context);
            return new ProjectService(
                context,
                audit.Object,
                new HttpContextAccessor { HttpContext = httpContext },
                new ProjectManagerMembershipSynchronizer(context),
                managementContext: managementContext,
                lifecycleCatalog: CreateLifecycleService(context, lookup, audit.Object),
                projectOrganizationLookup: lookup,
                organizationScopeLookup: organizationScopeLookup);
        }

        public ManagementContext CreateManagementContext(IReadOnlyCollection<Guid>? projectIds = null) => new(
            ActorId,
            TenantId,
            ManagementPerspective.PortfolioManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(
                new HashSet<Guid> { PortfolioId },
                new HashSet<Guid>(),
                projectIds?.ToHashSet() ?? new HashSet<Guid>()));

        public IManagementScopeResolver CreateManagementScopeResolver(ManagementContext context)
        {
            var resolver = new Mock<IManagementScopeResolver>();
            resolver.Setup(item => item.ResolveAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ManagementPerspective?>(),
                    It.IsAny<ManagementViewMode>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);
            return resolver.Object;
        }

        public static ProjectController CreateProjectController(ProjectService service, HttpContext httpContext)
        {
            var controller = new ProjectController(
                service,
                Mock.Of<IProjectSnapshotService>(),
                Mock.Of<IProjectHandoverRequestService>(),
                Mock.Of<IProjectStatusReportService>(),
                Mock.Of<IProjectOverviewService>(),
                Mock.Of<IProjectReportingService>());
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        public async Task<Result<NEXUS.Modules.ProjectManagement.Application.DTOs.ProjectDto>> CreateProjectAsync(
            string? name = null)
        {
            await using var context = CreateContext();
            return await CreateProjectAsync(context, name);
        }

        public async Task<Result<NEXUS.Modules.ProjectManagement.Application.DTOs.ProjectDto>> CreateProjectAsync(
            AppDbContext context, string? name = null)
        {
            var audit = new Mock<IAuditService>();
            audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            var lookup = new ProjectOrganizationLookup(context);
            var service = new ProjectService(
                context,
                audit.Object,
                new HttpContextAccessor(),
                new ProjectManagerMembershipSynchronizer(context),
                lifecycleCatalog: CreateLifecycleService(context, lookup, audit.Object),
                projectOrganizationLookup: lookup);

            return await service.CreateAsync(new CreateProjectDto
            {
                Name = name ?? "Phase assignment project",
                Code = $"P-{Guid.NewGuid():N}"[..14],
                PortfolioId = PortfolioId,
                Currency = "USD"
            }, ActorId.ToString("D"));
        }

        private static ProjectLifecycleCatalogService CreateLifecycleService(
            AppDbContext context,
            IProjectOrganizationLookup? lookup = null,
            IAuditService? audit = null) => new(
                context,
                new HttpContextAccessor(),
                audit ?? Mock.Of<IAuditService>(),
                projectOrganizationLookup: lookup);

        private static HttpContextAccessor CreateTenantAccessor(Guid tenantId, Guid actorId) => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                    new Claim("tenantId", tenantId.ToString("D"))
                ], "phase-test"))
            }
        };

        public async ValueTask DisposeAsync()
        {
            await RootDb.DisposeAsync();
            await _rootConnection.DisposeAsync();
        }
    }
}
