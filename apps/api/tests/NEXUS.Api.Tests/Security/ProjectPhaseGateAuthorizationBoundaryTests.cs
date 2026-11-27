using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class ProjectPhaseGateAuthorizationBoundaryTests
{
    [Fact]
    public async Task Compliance_read_details_honor_active_deny_on_document_view()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(fixture, db,
            ["Project.View", "Document.View"], ["Document.View"]);

        var result = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(db.ProjectPhaseGateSnapshots);
    }

    [Fact]
    public async Task Evidence_link_requires_document_edit_even_when_project_and_document_are_viewable()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(fixture, db, ["Project.View", "Document.View"], []);

        var result = await service.LinkDocumentAsync(fixture.Project.Id, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), fixture.UserId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(db.ProjectPhaseGateEvidence);
    }

    [Fact]
    public async Task Evidence_link_honors_active_deny_on_document_view()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(fixture, db,
            ["Project.View", "Document.View", "Document.Edit"], ["Document.View"]);

        var result = await service.LinkDocumentAsync(fixture.Project.Id, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), fixture.UserId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(db.ProjectPhaseGateEvidence);
    }

    [Fact]
    public async Task Evidence_link_honors_active_deny_on_document_edit()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(fixture, db,
            ["Project.View", "Document.View", "Document.Edit"], ["Document.Edit"]);

        var result = await service.LinkDocumentAsync(fixture.Project.Id, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), fixture.UserId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(db.ProjectPhaseGateEvidence);
    }

    [Fact]
    public async Task Authorized_phase_change_does_not_require_document_view_or_mandatory_evidence()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(fixture, db,
            ["Project.View", "ProjectPhase.Change"], ["Document.View"]);

        var transition = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);

        Assert.True(transition.IsSuccess, transition.Error?.Message);
        Assert.Equal(fixture.TargetPhaseCode, (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase);
        Assert.Empty(db.Documents);
    }

    [Fact]
    public async Task Project_upload_persists_and_reopens_the_exact_source_template_version()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var storage = CreateMemoryStorage(files);
        await SeedTemplateAsync(fixture, db, files);
        var harness = CreateHarness(fixture, db,
            ["Project.View", "Document.View", "Document.Create"], [],
            storageProvider: storage, useDocumentService: true);
        var bytes = PdfBytes();
        var upload = await harness.Documents.CreateAsync(new DocumentMutationDto(
            "Source template upload", null, fixture.Project.Id, null,
            SourceTemplateId: fixture.TemplateId,
            SourceTemplateVersionNumber: 1,
            File: new FormFile(new MemoryStream(bytes), 0, bytes.LongLength, "File", "source-upload.pdf"),
            SourceTemplateVersionId: fixture.TemplateVersionId), fixture.UserId);

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        Assert.Equal(fixture.TemplateId, upload.Value!.Document.SourceTemplateId);
        Assert.Equal(fixture.TemplateVersionId, upload.Value.Document.SourceTemplateVersionId);
        Assert.Equal(1, upload.Value.Document.SourceTemplateVersionNumber);
        var uploadedId = upload.Value.Document.Id;

        db.ChangeTracker.Clear();
        var reopened = await harness.Documents.GetByIdAsync(uploadedId);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.Equal(fixture.TemplateId, reopened.Value!.Document.SourceTemplateId);
        Assert.Equal(fixture.TemplateVersionId, reopened.Value.Document.SourceTemplateVersionId);
        Assert.Equal(1, reopened.Value.Document.SourceTemplateVersionNumber);
        var listed = await harness.Documents.GetPagedAsync(new DocumentListQuery(ProjectId: fixture.Project.Id));
        var listItem = Assert.Single(listed.Items);
        Assert.Equal(fixture.TemplateId, listItem.SourceTemplateId);
        Assert.Equal(fixture.TemplateVersionId, listItem.SourceTemplateVersionId);
        Assert.Equal(1, listItem.SourceTemplateVersionNumber);
    }

    [Fact]
    public async Task Project_document_upload_and_replacement_honor_active_deny_before_storage_access()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var storage = CreateMemoryStorage(files);
        var uploadHarness = CreateHarness(fixture, db,
            ["Project.View", "Document.View", "Document.Create"], ["Document.Create"],
            storageProvider: storage, useDocumentService: true);
        var uploadBytes = PdfBytes();
        var upload = await uploadHarness.Documents.CreateAsync(new DocumentMutationDto(
            "Denied project upload", null, fixture.Project.Id, null,
            File: new FormFile(new MemoryStream(uploadBytes), 0, uploadBytes.LongLength, "File", "denied.pdf")),
            fixture.UserId);

        Assert.True(upload.IsFailure);
        Assert.Equal(403, upload.Error!.StatusCode);
        storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var projectDocument = await SeedProjectDocumentAsync(fixture, db, files,
            fixture.Project.Id, null, fixture.TemplateId, fixture.TemplateVersionId);
        var replaceHarness = CreateHarness(fixture, db,
            ["Project.View", "Document.View", "Document.Edit"], ["Document.Edit"],
            storageProvider: storage, useDocumentService: true);
        var replacementBytes = PdfBytes();
        var replacement = await replaceHarness.Documents.AddVersionAsync(projectDocument.DocumentId,
            new FormFile(new MemoryStream(replacementBytes), 0, replacementBytes.LongLength, "File", "replacement.pdf"),
            null, fixture.UserId);

        Assert.True(replacement.IsFailure);
        Assert.Equal(403, replacement.Error!.StatusCode);
        storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("cross-project")]
    [InlineData("cross-organization")]
    [InlineData("cross-tenant")]
    public async Task Evidence_link_rejects_cross_scope_project_document_or_template_references(string boundary)
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var storage = CreateMemoryStorage(files);
        var configuration = fixture.Configuration;
        if (boundary == "cross-tenant")
            configuration = WithTemplate(configuration, fixture.ForeignTenantTemplateId, fixture.ForeignTenantTemplateVersionId);

        var harness = CreateHarness(fixture, db,
            ["Project.View", "Document.View", "Document.Edit"], [],
            storageProvider: storage, useDocumentService: true, configuration: configuration);
        var readiness = await harness.Readiness.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        Assert.True(readiness.IsSuccess, readiness.Error?.Message);
        var requirement = Assert.Single(readiness.Value!.Requirements);

        var targetProjectId = boundary == "cross-project" ? fixture.OtherProject.Id : fixture.Project.Id;
        var documentOrganizationId = boundary == "cross-organization" ? fixture.OtherOrganizationId : (Guid?)null;
        var projectDocument = await SeedProjectDocumentAsync(fixture, db, files,
            targetProjectId, documentOrganizationId, requirement.TemplateId, requirement.TemplateVersionId);

        var result = await harness.Readiness.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, projectDocument.DocumentId, projectDocument.VersionId, fixture.UserId);

        Assert.True(result.IsFailure);
        Assert.Empty(db.ProjectPhaseGateEvidence);
        storage.Verify(provider => provider.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Successful_evidence_link_and_unlink_are_audited_with_the_pinned_references()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Db;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var storage = CreateMemoryStorage(files);
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var harness = CreateHarness(fixture, db,
            ["Project.View", "Document.View", "Document.Edit"], [],
            storageProvider: storage, auditService: audit, useDocumentService: true);
        var readiness = await harness.Readiness.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        Assert.True(readiness.IsSuccess, readiness.Error?.Message);
        var requirement = Assert.Single(readiness.Value!.Requirements);
        var template = await SeedTemplateAsync(fixture, db, files);
        var projectDocument = await SeedProjectDocumentAsync(fixture, db, files,
            fixture.Project.Id, null, template.DocumentId, template.VersionId);

        var linked = await harness.Readiness.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, projectDocument.DocumentId, projectDocument.VersionId, fixture.UserId);

        Assert.True(linked.IsSuccess, linked.Error?.Message);
        var evidence = await db.ProjectPhaseGateEvidence.SingleAsync(item => item.ProjectId == fixture.Project.Id
            && item.RequirementSnapshotId == requirement.RequirementSnapshotId);
        Assert.Equal("NotVerified", evidence.ComplianceStatus);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.ProjectId == fixture.Project.Id
                && entry.TenantId == fixture.TenantId
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.EntityId == evidence.Id
                && entry.Action == "EvidenceLinked"
                && entry.UserId == fixture.UserId
                && entry.Changes!.Contains(template.DocumentId.ToString())
                && entry.Changes.Contains(template.VersionId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);

        var unlinked = await harness.Readiness.UnlinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.UserId);

        Assert.True(unlinked.IsSuccess, unlinked.Error?.Message);
        Assert.True(evidence.IsDeleted);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.ProjectId == fixture.Project.Id
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.EntityId == evidence.Id
                && entry.Action == "EvidenceUnlinked"
                && entry.UserId == fixture.UserId
                && entry.Changes!.Contains(template.DocumentId.ToString())
                && entry.Changes.Contains(template.VersionId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ProjectPhaseGateReadinessService CreateService(
        ProjectFixture fixture,
        AppDbContext db,
        string[] permissions,
        string[] deniedPermissions) => CreateHarness(fixture, db, permissions, deniedPermissions).Readiness;

    private static ServiceHarness CreateHarness(
        ProjectFixture fixture,
        AppDbContext db,
        string[] permissions,
        string[] deniedPermissions,
        Mock<IStorageProvider>? storageProvider = null,
        Mock<IAuditService>? auditService = null,
        bool useDocumentService = false,
        ProjectPhaseGateConfigurationDto? configuration = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, fixture.UserId.ToString()),
            new("tenantId", fixture.TenantId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        claims.AddRange(deniedPermissions.Select(permission => new Claim("deny", permission)));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "phase-gate-authorization-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { fixture.Project.Id })
        {
            OrganizationIds = new HashSet<Guid> { fixture.OrganizationId },
            OrganizationReferences = [new ManagementEntityReference(fixture.OrganizationId, "Authorization test organization")],
            ProjectOrganizationIds = new Dictionary<Guid, Guid> { [fixture.Project.Id] = fixture.OrganizationId }
        };
        var managementContext = new ManagementContext(fixture.UserId, fixture.TenantId,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, fixture.Project.Id, scope);
        var managementAccessor = new Mock<IManagementContextAccessor>();
        managementAccessor.SetupGet(item => item.Current).Returns(managementContext);
        var audit = auditService ?? new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var configurationReader = new Mock<IProjectPhaseGateConfigurationReader>();
        configurationReader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration ?? fixture.Configuration);
        configurationReader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[configuration ?? fixture.Configuration]);
        var storage = storageProvider ?? new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");
        var documentService = useDocumentService
            ? new DocumentService(db, storage.Object, http, audit.Object, managementAccessor.Object)
            : new Mock<IDocumentService>().Object;

        return new ServiceHarness(new ProjectPhaseGateReadinessService(db, http, configurationReader.Object,
            documentService, storage.Object, audit.Object, new ProjectOrganizationLookup(db), managementAccessor.Object),
            audit, storage, documentService);
    }

    private static async Task<ProjectFixture> CreateFixtureAsync()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var foreignTenantOrganizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourcePhaseId = Guid.NewGuid();
        var targetPhaseId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var foreignTenantTemplateId = Guid.NewGuid();
        var foreignTenantTemplateVersionId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Phase gate authorization test",
            Code = $"P-{Guid.NewGuid():N}"[..10],
            ProjectPhaseId = sourcePhaseId,
            Phase = "source",
            Status = "On Hold",
            RowVersion = [1]
        };
        var otherProject = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Other authorization test project",
            Code = $"P-{Guid.NewGuid():N}"[..10],
            ProjectPhaseId = sourcePhaseId,
            Phase = "source",
            Status = "On Hold",
            RowVersion = [1]
        };
        var configuration = new ProjectPhaseGateConfigurationDto(
            Guid.NewGuid(), Guid.NewGuid(), 1, organizationId,
            sourcePhaseId, "source", targetPhaseId, "target",
            [new ProjectPhaseGateConfigurationRequirementDto(Guid.NewGuid(), templateId, templateVersionId,
                "Mandatory template", "Governance", "v1", "Mandatory")]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase-gate-auth-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.Organizations.AddRange(
            new Organization
            {
                Id = organizationId, TenantId = tenantId, Name = "Authorization test organization", IsActive = true
            },
            new Organization
            {
                Id = otherOrganizationId, TenantId = tenantId, Name = "Other authorization organization", IsActive = true
            });
        db.ProjectPhaseCatalogEntries.AddRange(
            new ProjectPhaseCatalogEntry
            {
                Id = sourcePhaseId, TenantId = tenantId, OrganizationId = organizationId,
                Code = "source", NormalizedCode = "SOURCE", NameEn = "Source", NameAr = "المصدر",
                DisplayOrder = 10, IsActive = true
            },
            new ProjectPhaseCatalogEntry
            {
                Id = targetPhaseId, TenantId = tenantId, OrganizationId = organizationId,
                Code = "target", NormalizedCode = "TARGET", NameEn = "Target", NameAr = "الهدف",
                DisplayOrder = 20, IsActive = true
            });
        db.Portfolios.Add(new Portfolio
        {
            Id = portfolioId, TenantId = tenantId, OrganizationId = organizationId,
            Code = "PORT-AUTH-001", Name = "Authorization test portfolio", Currency = "GBP"
        });
        db.Projects.AddRange(project, otherProject);
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));
        await db.SaveChangesAsync();

        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(foreignTenantId, userId));
        db.Organizations.Add(new Organization
        {
            Id = foreignTenantOrganizationId,
            TenantId = foreignTenantId,
            Name = "Foreign tenant organization",
            IsActive = true
        });
        db.Documents.Add(new Document
        {
            Id = foreignTenantTemplateId,
            TenantId = foreignTenantId,
            Name = "Foreign tenant template",
            FileName = "foreign-template.pdf",
            FileType = "PDF",
            FileSize = 1,
            OrganizationId = foreignTenantOrganizationId,
            IsTemplate = true,
            Status = "Active",
            IsArchived = false,
            Version = 1
        });
        db.DocumentVersions.Add(new DocumentVersion
        {
            Id = foreignTenantTemplateVersionId,
            TenantId = foreignTenantId,
            DocumentId = foreignTenantTemplateId,
            VersionNumber = 1,
            FileName = "foreign-template.pdf",
            FileSize = 1,
            StorageProviderId = "test",
            StorageKey = "foreign-template",
            IsCurrent = true
        });
        await db.SaveChangesAsync();
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));
        return new ProjectFixture(db, tenantId, organizationId, otherOrganizationId, foreignTenantId,
            foreignTenantOrganizationId, userId, sourcePhaseId, targetPhaseId, "target", project,
            otherProject, templateId, templateVersionId, foreignTenantTemplateId, foreignTenantTemplateVersionId,
            configuration);
    }

    private static HttpContextAccessor CreateTenantAccessor(Guid tenantId, Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenantId", tenantId.ToString())
            ], "phase-gate-fixture"))
        }
    };

    private static ProjectPhaseGateConfigurationDto WithTemplate(
        ProjectPhaseGateConfigurationDto configuration,
        Guid templateId,
        Guid templateVersionId)
    {
        var requirement = configuration.Requirements.Single();
        return configuration with
        {
            Requirements = [requirement with { TemplateId = templateId, TemplateVersionId = templateVersionId }]
        };
    }

    private static Mock<IStorageProvider> CreateMemoryStorage(IDictionary<string, byte[]> files)
    {
        var storage = new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");
        storage.Setup(item => item.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Stream input, string _, CancellationToken _) =>
            {
                using var output = new MemoryStream();
                input.CopyTo(output);
                var bytes = output.ToArray();
                var key = $"uploaded-{Guid.NewGuid():N}";
                files[key] = bytes;
                return Task.FromResult(new StoredFile("test", key, bytes.LongLength,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            });
        storage.Setup(item => item.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) =>
            {
                if (!files.TryGetValue(key, out var bytes))
                    return Task.FromResult<StoredFileMetadata?>(null);
                return Task.FromResult<StoredFileMetadata?>(new StoredFileMetadata("test", key, bytes.LongLength,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            });
        storage.Setup(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => files.TryGetValue(key, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
                : Task.FromException<Stream>(new FileNotFoundException()));
        storage.Setup(item => item.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => Task.FromResult(files.ContainsKey(key)));
        return storage;
    }

    private static async Task<DocumentVersionReference> SeedProjectDocumentAsync(
        ProjectFixture fixture,
        AppDbContext db,
        IDictionary<string, byte[]> files,
        Guid projectId,
        Guid? organizationId,
        Guid sourceTemplateId,
        Guid sourceTemplateVersionId)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var storageKey = $"project-{documentId:N}";
        var bytes = PdfBytes();
        files[storageKey] = bytes;
        var document = new Document
        {
            Id = documentId,
            TenantId = fixture.TenantId,
            ProjectId = projectId,
            OrganizationId = organizationId,
            Name = "Uploaded project evidence",
            FileName = "evidence.pdf",
            FileType = "PDF",
            FileSize = bytes.LongLength,
            ContentType = "application/pdf",
            SourceTemplateId = sourceTemplateId,
            SourceTemplateVersionId = sourceTemplateVersionId,
            SourceTemplateVersionNumber = 1,
            IsTemplate = false,
            IsArchived = false,
            Status = "Active",
            Version = 1,
            OwnerId = fixture.UserId
        };
        var version = new DocumentVersion
        {
            Id = versionId,
            TenantId = fixture.TenantId,
            DocumentId = documentId,
            Document = document,
            VersionNumber = 1,
            FileName = "evidence.pdf",
            FileSize = bytes.LongLength,
            ContentType = "application/pdf",
            StorageProviderId = "test",
            StorageKey = storageKey,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            IsCurrent = true
        };
        document.Versions.Add(version);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return new DocumentVersionReference(documentId, versionId);
    }

    private static async Task<DocumentVersionReference> SeedTemplateAsync(
        ProjectFixture fixture,
        AppDbContext db,
        IDictionary<string, byte[]> files)
    {
        var bytes = PdfBytes();
        const string storageKey = "approved-template";
        files[storageKey] = bytes;
        var document = new Document
        {
            Id = fixture.TemplateId,
            TenantId = fixture.TenantId,
            Name = "Approved template",
            FileName = "template.pdf",
            FileType = "PDF",
            FileSize = bytes.LongLength,
            ContentType = "application/pdf",
            OrganizationId = fixture.OrganizationId,
            IsTemplate = true,
            IsArchived = false,
            Status = "Active",
            Version = 1,
            OwnerId = fixture.UserId
        };
        var version = new DocumentVersion
        {
            Id = fixture.TemplateVersionId,
            TenantId = fixture.TenantId,
            DocumentId = document.Id,
            Document = document,
            VersionNumber = 1,
            FileName = "template.pdf",
            FileSize = bytes.LongLength,
            ContentType = "application/pdf",
            StorageProviderId = "test",
            StorageKey = storageKey,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            IsCurrent = true
        };
        document.Versions.Add(version);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return new DocumentVersionReference(document.Id, version.Id);
    }

    private static byte[] PdfBytes()
    {
        var document = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int> { 0 };
        var content = "q Q\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream"
        };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(document.Length);
            document.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = document.Length;
        document.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
            document.Append($"{offsets[index]:D10} 00000 n \n");
        document.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(document.ToString());
    }

    private sealed record ProjectFixture(
        AppDbContext Db,
        Guid TenantId,
        Guid OrganizationId,
        Guid OtherOrganizationId,
        Guid ForeignTenantId,
        Guid ForeignTenantOrganizationId,
        Guid UserId,
        Guid SourcePhaseId,
        Guid TargetPhaseId,
        string TargetPhaseCode,
        Project Project,
        Project OtherProject,
        Guid TemplateId,
        Guid TemplateVersionId,
        Guid ForeignTenantTemplateId,
        Guid ForeignTenantTemplateVersionId,
        ProjectPhaseGateConfigurationDto Configuration);

    private sealed record ServiceHarness(
        ProjectPhaseGateReadinessService Readiness,
        Mock<IAuditService> Audit,
        Mock<IStorageProvider> Storage,
        IDocumentService Documents);

    private sealed record DocumentVersionReference(Guid DocumentId, Guid VersionId);
}
