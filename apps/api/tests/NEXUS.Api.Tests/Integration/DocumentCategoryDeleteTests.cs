using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class DocumentCategoryDeleteTests : IDisposable
{
    [Fact]
    public async Task An_authorized_user_can_delete_an_unused_category_and_success_is_audited()
    {
        await using var fixture = await TestFixture.CreateAsync(allowDelete: true);
        var audit = fixture.CreateAudit();
        var service = fixture.CreateService(audit.Object);
        var category = fixture.NewCategory();
        fixture.Context.Add(category);
        await fixture.Context.SaveChangesAsync();

        var result = await service.DeleteCategoryAsync(category.Id, fixture.UserId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await fixture.Context.Set<DocumentCategory>().IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == category.Id));
        audit.Verify(item => item.LogAsync(It.Is<AuditEntry>(entry => entry.EntityType == "DocumentCategory"
            && entry.EntityId == category.Id && entry.Action == "Delete" && entry.UserId == fixture.UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_is_denied_without_the_dedicated_permission()
    {
        await using var fixture = await TestFixture.CreateAsync(allowDelete: false);
        var category = fixture.NewCategory();
        fixture.Context.Add(category);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateService(fixture.CreateAudit().Object)
            .DeleteCategoryAsync(category.Id, fixture.UserId);

        Assert.Equal(403, result.Error?.StatusCode);
        Assert.True(await fixture.Context.Set<DocumentCategory>().IgnoreQueryFilters()
            .AnyAsync(item => item.Id == category.Id));
    }

    [Fact]
    public async Task Active_deny_overrides_the_delete_permission()
    {
        await using var fixture = await TestFixture.CreateAsync(allowDelete: true, denyDelete: true);
        var category = fixture.NewCategory();
        fixture.Context.Add(category);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateService(fixture.CreateAudit().Object)
            .DeleteCategoryAsync(category.Id, fixture.UserId);

        Assert.Equal(403, result.Error?.StatusCode);
        Assert.True(await fixture.Context.Set<DocumentCategory>().IgnoreQueryFilters()
            .AnyAsync(item => item.Id == category.Id));
    }

    [Fact]
    public async Task A_category_from_another_organization_is_not_disclosed_or_deleted()
    {
        await using var fixture = await TestFixture.CreateAsync(allowDelete: true);
        var category = fixture.NewCategory(fixture.OtherOrganizationId);
        fixture.Context.Add(category);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateService(fixture.CreateAudit().Object)
            .DeleteCategoryAsync(category.Id, fixture.UserId);

        Assert.Equal(404, result.Error?.StatusCode);
        Assert.True(await fixture.Context.Set<DocumentCategory>().IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == category.Id));
    }

    [Theory]
    [InlineData("document")]
    [InlineData("template")]
    [InlineData("version")]
    [InlineData("project-document")]
    [InlineData("phase-catalog")]
    [InlineData("organization-phase-gate")]
    [InlineData("project-phase-gate")]
    [InlineData("project-evidence")]
    public async Task Any_document_version_project_or_phase_reference_returns_409_and_preserves_history(string referenceType)
    {
        await using var fixture = await TestFixture.CreateAsync(allowDelete: true);
        var category = fixture.NewCategory();
        fixture.Context.Add(category);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddReferenceAsync(category, referenceType);
        var audit = fixture.CreateAudit();

        var result = await fixture.CreateService(audit.Object).DeleteCategoryAsync(category.Id, fixture.UserId);

        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Equal(DocumentCategoryPermissions.ReferencedErrorCode, result.Error?.Code);
        Assert.Contains("Deactivate", result.Error?.Message, StringComparison.Ordinal);
        Assert.True(await fixture.Context.Set<DocumentCategory>().IgnoreQueryFilters()
            .AnyAsync(item => item.Id == category.Id));
        Assert.Equal(fixture.ExpectedDocumentCount(referenceType), await fixture.Context.Documents.IgnoreQueryFilters()
            .CountAsync(document => document.TenantId == fixture.TenantId));
        Assert.Equal(fixture.ExpectedVersionCount(referenceType), await fixture.Context.DocumentVersions.IgnoreQueryFilters()
            .CountAsync(version => version.TenantId == fixture.TenantId));
        audit.Verify(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly HttpContextAccessor _http;
        private readonly ManagementContextAccessor _management;
        private readonly string _databaseName;

        private TestFixture(Guid tenantId, Guid userId, Guid organizationId, Guid otherOrganizationId,
            HttpContextAccessor http, ManagementContextAccessor management, string databaseName, AppDbContext context)
        {
            TenantId = tenantId;
            UserId = userId;
            OrganizationId = organizationId;
            OtherOrganizationId = otherOrganizationId;
            _http = http;
            _management = management;
            _databaseName = databaseName;
            Context = context;
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid OrganizationId { get; }
        public Guid OtherOrganizationId { get; }
        public AppDbContext Context { get; }

        public static async Task<TestFixture> CreateAsync(bool allowDelete, bool denyDelete = false)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            var otherOrganizationId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("tenantId", tenantId.ToString()),
                new("permission", DocumentCategoryPermissions.View)
            };
            if (allowDelete) claims.Add(new Claim("permission", DocumentCategoryPermissions.Delete));
            if (denyDelete) claims.Add(new Claim("deny", DocumentCategoryPermissions.Delete));
            var http = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "document-category-delete-test"))
                }
            };
            AppDbContext.ConfigureTenantResolution(http);

            var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
            {
                OrganizationIds = new HashSet<Guid> { organizationId },
                OrganizationReferences = [new ManagementEntityReference(organizationId, "Authorized organization")]
            };
            var management = new ManagementContextAccessor();
            management.Set(new ManagementContext(userId, tenantId, ManagementPerspective.ProjectManager,
                ManagementViewMode.Native, null, null, null, scope));
            var databaseName = $"document-category-delete-{Guid.NewGuid():N}";
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            context.AddRange(
                new Organization { Id = organizationId, TenantId = tenantId, Name = "Authorized organization", CreatedBy = "test" },
                new Organization { Id = otherOrganizationId, TenantId = tenantId, Name = "Other organization", CreatedBy = "test" });
            await context.SaveChangesAsync();
            return new TestFixture(tenantId, userId, organizationId, otherOrganizationId, http, management, databaseName, context);
        }

        public DocumentCategory NewCategory(Guid? organizationId = null)
        {
            var name = $"Category-{Guid.NewGuid():N}";
            return new DocumentCategory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrganizationId = organizationId ?? OrganizationId,
                Name = name,
                NameAr = "فئة",
                NormalizedNameEn = name.ToUpperInvariant(),
                NormalizedNameAr = "فئة",
                IsActive = true,
                CreatedBy = UserId.ToString()
            };
        }

        public Mock<IAuditService> CreateAudit()
        {
            var audit = new Mock<IAuditService>();
            audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            return audit;
        }

        public DocumentService CreateService(IAuditService audit) => new(
            Context,
            Mock.Of<IStorageProvider>(),
            _http,
            audit,
            _management,
            documentCategoryReferenceValidator: new EfDocumentCategoryReferenceValidator(Context));

        public async Task AddReferenceAsync(DocumentCategory category, string referenceType)
        {
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var isTemplate = referenceType is "template" or "organization-phase-gate" or "project-phase-gate";
            var isProjectDocument = referenceType is "project-document" or "project-evidence";

            if (referenceType is "document" or "template" or "version" or "project-document" or "project-evidence")
            {
                var projectId = isProjectDocument ? Guid.NewGuid() : (Guid?)null;
                var document = NewDocument(documentId, category.Id, isTemplate, projectId);
                Context.Add(document);
                if (referenceType is "version" or "project-evidence")
                {
                    Context.Add(NewVersion(versionId, documentId));
                }
                if (referenceType == "project-evidence")
                {
                    var gate = AddOrganizationGateRecords(category, documentId, versionId);
                    AddProjectGateRecords(category, documentId, versionId, projectId!.Value, gate);
                }
            }
            else if (referenceType is "organization-phase-gate" or "project-phase-gate")
            {
                var document = NewDocument(documentId, null, isTemplate, null);
                var version = NewVersion(versionId, documentId);
                Context.AddRange(document, version);
                var gate = AddOrganizationGateRecords(category, documentId, versionId);
                if (referenceType == "project-phase-gate")
                    AddProjectGateRequirementSnapshot(category, documentId, versionId, Guid.NewGuid(), gate);
            }
            else if (referenceType == "phase-catalog")
            {
                Context.Add(new ProjectPhaseCatalogEntry
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, OrganizationId = OrganizationId,
                    Code = "phase-test", NormalizedCode = "PHASE-TEST", NameEn = "Phase", NameAr = "مرحلة",
                    IsActive = true, DocumentCategoryId = category.Id, CreatedBy = UserId.ToString()
                });
            }

            await Context.SaveChangesAsync();
        }

        public int ExpectedDocumentCount(string referenceType) =>
            referenceType is "document" or "template" or "version" or "project-document" or "project-evidence"
                ? 1
                : referenceType is "organization-phase-gate" or "project-phase-gate" ? 1 : 0;

        public int ExpectedVersionCount(string referenceType) =>
            referenceType is "version" or "project-evidence" or "organization-phase-gate" or "project-phase-gate" ? 1 : 0;

        private Document NewDocument(Guid id, Guid? categoryId, bool isTemplate, Guid? projectId) => new()
        {
            Id = id, TenantId = TenantId, OrganizationId = isTemplate ? OrganizationId : null,
            CategoryId = categoryId, ProjectId = projectId, IsTemplate = isTemplate,
            Name = "Test document", Status = "Active", CreatedBy = UserId.ToString()
        };

        private DocumentVersion NewVersion(Guid id, Guid documentId) => new()
        {
            Id = id, TenantId = TenantId, DocumentId = documentId, VersionNumber = 1,
            FileName = "test.pdf", FileSize = 1, IsCurrent = true, UploadedBy = UserId,
            UploadedAt = DateTime.UtcNow, CreatedBy = UserId.ToString()
        };

        private GateReferences AddOrganizationGateRecords(DocumentCategory category, Guid templateId, Guid versionId)
        {
            var configuration = new OrganizationPhaseGateConfiguration
            {
                Id = Guid.NewGuid(), TenantId = TenantId, OrganizationId = OrganizationId,
                SourcePhase = "pre-initiation", TargetPhase = "initiation", CurrentVersionNumber = 1,
                CreatedBy = UserId.ToString()
            };
            var configurationVersion = new OrganizationPhaseGateConfigurationVersion
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ConfigurationId = configuration.Id,
                VersionNumber = 1, Configuration = configuration, CreatedBy = UserId.ToString()
            };
            configuration.Versions.Add(configurationVersion);
            var requirement = new OrganizationPhaseGateRequirement
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ConfigurationVersionId = configurationVersion.Id,
                ConfigurationVersion = configurationVersion, TemplateId = templateId, TemplateVersionId = versionId,
                TemplateName = "Pinned template", Category = category.Name,
                TemplateVersionLabel = "1", RequirementType = "Mandatory", CreatedBy = UserId.ToString()
            };
            configurationVersion.Requirements.Add(requirement);
            Context.Add(configuration);
            return new GateReferences(configuration.Id, configurationVersion.Id, requirement.Id);
        }

        private Guid AddProjectGateRequirementSnapshot(
            DocumentCategory category, Guid templateId, Guid versionId, Guid projectId, GateReferences gate)
        {
            Context.Add(new Project { Id = projectId, TenantId = TenantId, Name = "Test project", Code = "TEST", CreatedBy = UserId.ToString() });
            var sourcePhaseId = Guid.NewGuid();
            var targetPhaseId = Guid.NewGuid();
            Context.AddRange(NewPhase(sourcePhaseId, "pre-initiation"), NewPhase(targetPhaseId, "initiation"));
            var snapshotId = Guid.NewGuid();
            var snapshot = new ProjectPhaseGateSnapshot
            {
                Id = snapshotId, TenantId = TenantId, ProjectId = projectId, OrganizationId = OrganizationId,
                SourcePhaseId = sourcePhaseId, TargetPhaseId = targetPhaseId,
                SourcePhase = "pre-initiation", TargetPhase = "initiation", ConfigurationId = gate.ConfigurationId,
                ConfigurationVersionId = gate.ConfigurationVersionId, ConfigurationVersionNumber = 1, CreatedBy = UserId.ToString()
            };
            Context.Add(snapshot);
            Context.Add(new ProjectPhaseGateRequirementSnapshot
            {
                Id = Guid.NewGuid(), TenantId = TenantId, SnapshotId = snapshotId, SourceRequirementId = gate.RequirementId,
                TemplateId = templateId, TemplateVersionId = versionId, TemplateName = "Pinned template",
                Category = category.Name, TemplateVersionLabel = "1", RequirementType = "Mandatory", CreatedBy = UserId.ToString()
            });
            return Context.ChangeTracker.Entries<ProjectPhaseGateRequirementSnapshot>()
                .Single(entry => entry.Entity.SnapshotId == snapshotId).Entity.Id;
        }

        private void AddProjectGateRecords(DocumentCategory category, Guid documentId, Guid versionId, Guid projectId, GateReferences gate)
        {
            var requirementId = AddProjectGateRequirementSnapshot(category, documentId, versionId, projectId, gate);
            Context.Add(new ProjectPhaseGateEvidence
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, OrganizationId = OrganizationId,
                RequirementSnapshotId = requirementId, DocumentId = documentId, DocumentVersionId = versionId,
                ComplianceStatus = "PendingValidation", CreatedBy = UserId.ToString()
            });
        }

        private ProjectPhaseCatalogEntry NewPhase(Guid id, string code) => new()
        {
            Id = id, TenantId = TenantId, OrganizationId = OrganizationId, Code = code,
            NormalizedCode = code.ToUpperInvariant(), NameEn = code, NameAr = code,
            IsActive = true, CreatedBy = UserId.ToString()
        };

        private sealed record GateReferences(Guid ConfigurationId, Guid ConfigurationVersionId, Guid RequirementId);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        }
    }
}
