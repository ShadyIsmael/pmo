using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
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
public sealed class ProjectDocumentTemplateAssociationHistoryTests : IDisposable
{
    [Fact]
    public async Task Relinking_same_document_version_revalidates_pending_evidence_and_persists_comparison_details()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Activate();
        await using var db = fixture.Db;
        var service = fixture.Service;
        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        Assert.True(readiness.IsSuccess, readiness.Error?.Message);
        var requirement = Assert.Single(readiness.Value!.Requirements);

        fixture.ProjectDocument.SourceTemplateId = fixture.Template.Id;
        fixture.ProjectDocument.SourceTemplateVersionId = fixture.TemplateVersion.Id;
        fixture.ProjectDocument.SourceTemplateVersionNumber = fixture.TemplateVersion.VersionNumber;
        var pendingEvidence = new ProjectPhaseGateEvidence
        {
            TenantId = fixture.TenantId,
            ProjectId = fixture.Project.Id,
            OrganizationId = fixture.OrganizationId,
            RequirementSnapshotId = requirement.RequirementSnapshotId,
            DocumentId = fixture.ProjectDocument.Id,
            DocumentVersionId = fixture.ProjectDocumentVersion1.Id,
            ComplianceStatus = ProjectDocumentComplianceStatuses.PendingValidation,
            ExpectedFileType = "PDF",
            CreatedBy = fixture.UserId.ToString()
        };
        db.ProjectPhaseGateEvidence.Add(pendingEvidence);
        await db.SaveChangesAsync();

        var refreshed = await service.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.ProjectDocument.Id,
            fixture.ProjectDocumentVersion1.Id, fixture.UserId);

        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        var refreshedRequirement = Assert.Single(refreshed.Value!.Requirements);
        Assert.Equal(ProjectDocumentComplianceStatuses.NotVerified, refreshedRequirement.ComplianceStatus);
        Assert.NotNull(refreshedRequirement.ComparedAt);
        Assert.Equal(DocumentTemplateComparison.ValidatorVersion, refreshedRequirement.ValidatorVersion);
        var refreshedFinding = Assert.Single(refreshedRequirement.ComplianceFindings!);
        Assert.Equal("StructureNotSupported", refreshedFinding.Code);
        Assert.False(string.IsNullOrWhiteSpace(refreshedFinding.MessageEn));
        Assert.False(string.IsNullOrWhiteSpace(refreshedFinding.MessageAr));

        var persisted = await db.ProjectPhaseGateEvidence.SingleAsync(item => item.Id == pendingEvidence.Id);
        Assert.NotEqual(ProjectDocumentComplianceStatuses.PendingValidation, persisted.ComplianceStatus);
        Assert.Equal(refreshedRequirement.ComplianceStatus, persisted.ComplianceStatus);
        Assert.Equal(refreshedRequirement.ExpectedFileType, persisted.ExpectedFileType);
        Assert.Equal(refreshedRequirement.DetectedFileType, persisted.DetectedFileType);
        Assert.Equal(refreshedRequirement.ComparedAt, persisted.ComparedAt);
        Assert.Equal(DocumentTemplateComparison.ValidatorVersion, persisted.ValidatorVersion);
        Assert.NotNull(persisted.ComplianceFindingsJson);
        var persistedFindings = JsonSerializer.Deserialize<ProjectDocumentComplianceFindingDto[]>(persisted.ComplianceFindingsJson);
        Assert.NotNull(persistedFindings);
        Assert.Equal(refreshedRequirement.ComplianceFindings, persistedFindings);

        fixture.Audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry =>
                entry.ProjectId == fixture.Project.Id
                && entry.EntityId == pendingEvidence.Id
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.Action == "EvidenceComplianceValidated"
                && entry.Changes!.Contains(ProjectDocumentComplianceStatuses.NotVerified)
                && entry.Changes.Contains(DocumentTemplateComparison.ValidatorVersion)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Matching_document_structure_without_source_template_identity_is_not_reported_compliant()
    {
        var fixture = await CreateFixtureAsync(useDocx: true);
        fixture.Activate();
        await using var db = fixture.Db;
        var readiness = await fixture.Service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        Assert.True(readiness.IsSuccess, readiness.Error?.Message);
        var requirement = Assert.Single(readiness.Value!.Requirements);

        var linked = await fixture.Service.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.ProjectDocument.Id,
            fixture.ProjectDocumentVersion1.Id, fixture.UserId);

        Assert.True(linked.IsSuccess, linked.Error?.Message);
        var linkedRequirement = Assert.Single(linked.Value!.Requirements);
        Assert.Equal(ProjectDocumentComplianceStatuses.NotVerified, linkedRequirement.ComplianceStatus);
        var finding = Assert.Single(linkedRequirement.ComplianceFindings!);
        Assert.Equal("TemplateIdentityNotVerified", finding.Code);
        Assert.False(string.IsNullOrWhiteSpace(finding.MessageEn));
        Assert.False(string.IsNullOrWhiteSpace(finding.MessageAr));
    }

    [Fact]
    public async Task Linking_unassociated_project_document_preserves_requirement_and_version_history_without_claiming_template_origin()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Activate();
        await using var db = fixture.Db;
        var service = fixture.Service;
        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        Assert.True(readiness.IsSuccess, readiness.Error?.Message);
        var requirement = Assert.Single(readiness.Value!.Requirements);
        Assert.Equal(fixture.Template.Id, requirement.TemplateId);
        Assert.Equal(fixture.TemplateVersion.Id, requirement.TemplateVersionId);

        var initialLink = await service.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.ProjectDocument.Id,
            fixture.ProjectDocumentVersion1.Id, fixture.UserId);

        Assert.True(initialLink.IsSuccess, initialLink.Error?.Message);
        db.ChangeTracker.Clear();

        var linkedDocument = await db.Documents.SingleAsync(item => item.Id == fixture.ProjectDocument.Id);
        Assert.Equal(fixture.Project.Id, linkedDocument.ProjectId);
        Assert.Null(linkedDocument.SourceTemplateId);
        Assert.Null(linkedDocument.SourceTemplateVersionId);
        Assert.Null(linkedDocument.SourceTemplateVersionNumber);

        var firstEvidence = await db.ProjectPhaseGateEvidence.SingleAsync(item =>
            item.ProjectId == fixture.Project.Id && item.RequirementSnapshotId == requirement.RequirementSnapshotId);
        Assert.Equal(fixture.ProjectDocument.Id, firstEvidence.DocumentId);
        Assert.Equal(fixture.ProjectDocumentVersion1.Id, firstEvidence.DocumentVersionId);
        Assert.Equal(fixture.OrganizationId, firstEvidence.OrganizationId);
        Assert.Equal(fixture.TenantId, firstEvidence.TenantId);

        var replacementWhileLinked = await service.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.ProjectDocument.Id,
            fixture.ProjectDocumentVersion2.Id, fixture.UserId);
        Assert.True(replacementWhileLinked.IsFailure);
        Assert.Equal(409, replacementWhileLinked.Error!.StatusCode);

        var unlinked = await service.UnlinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.UserId);
        Assert.True(unlinked.IsSuccess, unlinked.Error?.Message);

        var replacementLink = await service.LinkDocumentAsync(fixture.Project.Id,
            requirement.RequirementSnapshotId, fixture.ProjectDocument.Id,
            fixture.ProjectDocumentVersion2.Id, fixture.UserId);
        Assert.True(replacementLink.IsSuccess, replacementLink.Error?.Message);
        db.ChangeTracker.Clear();

        var history = await db.ProjectPhaseGateEvidence
            .IgnoreQueryFilters()
            .Where(item => item.ProjectId == fixture.Project.Id
                && item.RequirementSnapshotId == requirement.RequirementSnapshotId)
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        var historicalEvidence = Assert.Single(history, item => item.IsDeleted);
        Assert.Equal(fixture.ProjectDocumentVersion1.Id, historicalEvidence.DocumentVersionId);
        var activeEvidence = Assert.Single(history, item => !item.IsDeleted);
        Assert.Equal(fixture.ProjectDocumentVersion2.Id, activeEvidence.DocumentVersionId);
        Assert.All(history, item =>
        {
            Assert.Equal(fixture.Project.Id, item.ProjectId);
            Assert.Equal(fixture.OrganizationId, item.OrganizationId);
            Assert.Equal(fixture.TenantId, item.TenantId);
            Assert.Equal(fixture.ProjectDocument.Id, item.DocumentId);
        });

        var versions = await db.DocumentVersions
            .IgnoreQueryFilters()
            .Where(item => item.DocumentId == fixture.ProjectDocument.Id)
            .OrderBy(item => item.VersionNumber)
            .ToArrayAsync();
        Assert.Equal(2, versions.Length);
        Assert.Equal(fixture.ProjectDocumentVersion1.Id, versions[0].Id);
        Assert.False(versions[0].IsDeleted);
        Assert.False(versions[0].IsCurrent);
        Assert.Equal(fixture.ProjectDocumentVersion2.Id, versions[1].Id);
        Assert.False(versions[1].IsDeleted);
        Assert.True(versions[1].IsCurrent);

        var templateAfterReplacement = await db.Documents.SingleAsync(item => item.Id == fixture.Template.Id);
        Assert.True(templateAfterReplacement.IsTemplate);
        Assert.Null(templateAfterReplacement.ProjectId);
        Assert.Null(templateAfterReplacement.SourceTemplateId);
        var pinnedRequirement = await db.ProjectPhaseGateRequirementSnapshots.SingleAsync(item =>
            item.Id == requirement.RequirementSnapshotId);
        Assert.Equal(fixture.Template.Id, pinnedRequirement.TemplateId);
        Assert.Equal(fixture.TemplateVersion.Id, pinnedRequirement.TemplateVersionId);
        var projectSnapshot = await db.ProjectPhaseGateSnapshots.SingleAsync(item => item.Id == pinnedRequirement.SnapshotId);
        Assert.Equal(fixture.Project.Id, projectSnapshot.ProjectId);
        Assert.Equal(fixture.OrganizationId, projectSnapshot.OrganizationId);
        Assert.Equal(fixture.TargetPhaseId, projectSnapshot.TargetPhaseId);

        fixture.Audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry =>
                entry.ProjectId == fixture.Project.Id
                && entry.TenantId == fixture.TenantId
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.Action == "EvidenceLinked"
                && entry.Changes!.Contains(fixture.ProjectDocumentVersion1.Id.ToString())
                && entry.Changes!.Contains(fixture.Template.Id.ToString())
                && entry.Changes.Contains(fixture.TemplateVersion.Id.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
        var linkedAudit = Assert.Single(fixture.Audit.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IAuditService.AppendAsync))
            .Select(invocation => invocation.Arguments[0])
            .OfType<AuditEntry>()
            .Where(entry => entry.Action == "EvidenceLinked"
                && entry.Changes?.Contains(fixture.ProjectDocumentVersion1.Id.ToString(), StringComparison.Ordinal) == true));
        Assert.Equal("Project document version linked to a pinned Phase Gate requirement; compliance is evaluated separately.", linkedAudit.Description);
        foreach (var json in new[] { linkedAudit.Changes, linkedAudit.NewValues })
        {
            Assert.False(string.IsNullOrWhiteSpace(json));
            using var auditData = JsonDocument.Parse(json!);
            if (auditData.RootElement.TryGetProperty("externalApprovalAssumption", out var assumption))
                Assert.Equal(JsonValueKind.False, assumption.ValueKind);
        }
        fixture.Audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry =>
                entry.ProjectId == fixture.Project.Id
                && entry.TenantId == fixture.TenantId
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.Action == "EvidenceLinked"
                && entry.Changes!.Contains(fixture.ProjectDocumentVersion2.Id.ToString())
                && entry.Changes.Contains(fixture.Template.Id.ToString())
                && entry.Changes.Contains(fixture.TemplateVersion.Id.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry =>
                entry.ProjectId == fixture.Project.Id
                && entry.EntityType == "ProjectPhaseGateEvidence"
                && entry.Action == "EvidenceUnlinked"
                && entry.Changes!.Contains(fixture.ProjectDocumentVersion1.Id.ToString())
                && entry.Changes.Contains(fixture.TemplateVersion.Id.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static async Task<Fixture> CreateFixtureAsync(bool useDocx = false)
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var sourcePhaseId = Guid.NewGuid();
        var targetPhaseId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var projectDocumentId = Guid.NewGuid();
        var projectDocumentVersion1Id = Guid.NewGuid();
        var projectDocumentVersion2Id = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenantId", tenantId.ToString()),
            new("permission", "Project.View"),
            new("permission", "Document.View"),
            new("permission", "Document.Edit")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "project-document-association-test"));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        AppDbContext.ConfigureTenantResolution(http);

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Project document association test",
            Code = $"P-{Guid.NewGuid():N}"[..10],
            ProjectPhaseId = sourcePhaseId,
            Phase = "source",
            Status = "Active",
            CreatedBy = userId.ToString(),
            RowVersion = [1]
        };
        var fileExtension = useDocx ? ".docx" : ".pdf";
        var fileType = useDocx ? "DOCX" : "PDF";
        var contentType = useDocx
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/pdf";
        var template = new Document
        {
            Id = templateId,
            TenantId = tenantId,
            Name = "Pinned template",
            FileName = $"template{fileExtension}",
            FileType = fileType,
            OrganizationId = organizationId,
            IsTemplate = true,
            Status = "Active",
            Version = 1,
            CreatedBy = userId.ToString()
        };
        var templateBytes = useDocx ? DocxBytes("Template content.") : PdfBytes("approved template");
        const string templateStorageKey = "association-template-v1";
        var templateVersion = new DocumentVersion
        {
            Id = templateVersionId,
            TenantId = tenantId,
            DocumentId = templateId,
            Document = template,
            VersionNumber = 1,
            FileName = "template.pdf",
            FileSize = templateBytes.LongLength,
            StorageProviderId = "association-test",
            StorageKey = templateStorageKey,
            Sha256 = Sha256(templateBytes),
            IsCurrent = true,
            CreatedBy = userId.ToString()
        };
        template.Versions.Add(templateVersion);

        var projectDocument = new Document
        {
            Id = projectDocumentId,
            TenantId = tenantId,
            ProjectId = projectId,
            Name = "Completed project document",
            FileName = $"completed{fileExtension}",
            FileType = fileType,
            ContentType = contentType,
            Status = "Active",
            Version = 2,
            IsTemplate = false,
            IsArchived = false,
            CreatedBy = userId.ToString()
        };
        var projectVersion1Bytes = useDocx ? DocxBytes("Completed project-specific content.") : PdfBytes("uploaded version one");
        var projectVersion2Bytes = useDocx ? DocxBytes("Replacement project-specific content.") : PdfBytes("uploaded version two");
        const string projectVersion1StorageKey = "association-project-v1";
        const string projectVersion2StorageKey = "association-project-v2";
        var projectVersion1 = new DocumentVersion
        {
            Id = projectDocumentVersion1Id,
            TenantId = tenantId,
            DocumentId = projectDocumentId,
            Document = projectDocument,
            VersionNumber = 1,
            FileName = $"completed-v1{fileExtension}",
            FileSize = projectVersion1Bytes.LongLength,
            StorageProviderId = "association-test",
            StorageKey = projectVersion1StorageKey,
            Sha256 = Sha256(projectVersion1Bytes),
            IsCurrent = false,
            CreatedBy = userId.ToString()
        };
        var projectVersion2 = new DocumentVersion
        {
            Id = projectDocumentVersion2Id,
            TenantId = tenantId,
            DocumentId = projectDocumentId,
            Document = projectDocument,
            VersionNumber = 2,
            FileName = $"completed-v2{fileExtension}",
            FileSize = projectVersion2Bytes.LongLength,
            StorageProviderId = "association-test",
            StorageKey = projectVersion2StorageKey,
            Sha256 = Sha256(projectVersion2Bytes),
            IsCurrent = true,
            CreatedBy = userId.ToString()
        };
        projectDocument.Versions.Add(projectVersion1);
        projectDocument.Versions.Add(projectVersion2);

        var requirements = new ProjectPhaseGateConfigurationDto(
            Guid.NewGuid(), Guid.NewGuid(), 1, organizationId,
            sourcePhaseId, "source", targetPhaseId, "target",
            [new ProjectPhaseGateConfigurationRequirementDto(
                Guid.NewGuid(), templateId, templateVersionId, "Pinned template", "Governance", "v1", "Mandatory")]);
        var configurationReader = new Mock<IProjectPhaseGateConfigurationReader>();
        configurationReader.Setup(item => item.GetCurrentAsync(tenantId, organizationId,
                sourcePhaseId, targetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);
        configurationReader.Setup(item => item.GetCurrentBySourceAsync(tenantId, organizationId,
                sourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[requirements]);

        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })
        {
            OrganizationIds = new HashSet<Guid> { organizationId },
            OrganizationReferences = [new ManagementEntityReference(organizationId, "Association test organization")],
            ProjectOrganizationIds = new Dictionary<Guid, Guid> { [projectId] = organizationId }
        };
        var managementContext = new ManagementContext(userId, tenantId,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, projectId, scope);
        var managementAccessor = new Mock<IManagementContextAccessor>();
        managementAccessor.SetupGet(item => item.Current).Returns(managementContext);

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [templateStorageKey] = templateBytes,
            [projectVersion1StorageKey] = projectVersion1Bytes,
            [projectVersion2StorageKey] = projectVersion2Bytes
        };
        var storage = new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("association-test");
        storage.Setup(item => item.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) =>
            {
                if (!files.TryGetValue(key, out var bytes))
                    return Task.FromResult<StoredFileMetadata?>(null);
                return Task.FromResult<StoredFileMetadata?>(new StoredFileMetadata(
                    "association-test", key, bytes.LongLength, Sha256(bytes)));
            });
        storage.Setup(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => files.TryGetValue(key, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
                : Task.FromException<Stream>(new FileNotFoundException()));
        storage.Setup(item => item.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => Task.FromResult(files.ContainsKey(key)));

        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-document-association-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.Organizations.Add(new Organization
        {
            Id = organizationId,
            TenantId = tenantId,
            Name = "Association test organization",
            IsActive = true,
            CreatedBy = userId.ToString()
        });
        db.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = "PORT-ASSOC-001",
            Name = "Association test portfolio",
            Currency = "GBP",
            CreatedBy = userId.ToString()
        });
        db.ProjectPhaseCatalogEntries.AddRange(
            new ProjectPhaseCatalogEntry
            {
                Id = sourcePhaseId,
                TenantId = tenantId,
                OrganizationId = organizationId,
                Code = "source",
                NormalizedCode = "SOURCE",
                NameEn = "Source",
                NameAr = "المصدر",
                DisplayOrder = 10,
                IsActive = true,
                CreatedBy = userId.ToString()
            },
            new ProjectPhaseCatalogEntry
            {
                Id = targetPhaseId,
                TenantId = tenantId,
                OrganizationId = organizationId,
                Code = "target",
                NormalizedCode = "TARGET",
                NameEn = "Target",
                NameAr = "الهدف",
                DisplayOrder = 20,
                IsActive = true,
                CreatedBy = userId.ToString()
            });
        db.Projects.Add(project);
        db.Documents.AddRange(template, projectDocument);
        await db.SaveChangesAsync();

        var documentService = new DocumentService(db, storage.Object, http, audit.Object, managementAccessor.Object);
        var phaseGateService = new ProjectPhaseGateReadinessService(db, http,
            configurationReader.Object, documentService, storage.Object, audit.Object,
            new ProjectOrganizationLookup(db), managementAccessor.Object);
        return new Fixture(db, phaseGateService, audit, tenantId, organizationId, userId,
            project, targetPhaseId, template, templateVersion, projectDocument, projectVersion1, projectVersion2,
            http, principal);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] DocxBytes(string bodyText)
    {
        var escapedText = bodyText.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        var document = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>Overview</w:t></w:r></w:p>" +
            "<w:p><w:r><w:t>" + escapedText + "</w:t></w:r></w:p></w:body></w:document>";
        const string contentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
            "</Types>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WritePackageEntry(archive, "[Content_Types].xml", contentTypes);
            WritePackageEntry(archive, "word/document.xml", document);
        }
        return stream.ToArray();
    }

    private static void WritePackageEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static byte[] PdfBytes(string text)
    {
        var escapedText = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 12 Tf 40 700 Td ({escapedText}) Tj ET\n";
        var contentLength = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {contentLength} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var document = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(document.Length);
            document.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var crossReferenceOffset = document.Length;
        document.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
            document.Append($"{offsets[index]:D10} 00000 n \n");
        document.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{crossReferenceOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(document.ToString());
    }

    private sealed record Fixture(
        AppDbContext Db,
        ProjectPhaseGateReadinessService Service,
        Mock<IAuditService> Audit,
        Guid TenantId,
        Guid OrganizationId,
        Guid UserId,
        Project Project,
        Guid TargetPhaseId,
        Document Template,
        DocumentVersion TemplateVersion,
        Document ProjectDocument,
        DocumentVersion ProjectDocumentVersion1,
        DocumentVersion ProjectDocumentVersion2,
        HttpContextAccessor Http,
        ClaimsPrincipal Principal)
    {
        // HttpContextAccessor stores the context in an AsyncLocal. The fixture factory is an
        // async method, so a value set there is not visible to the awaiting test. Re-applying
        // the principal in the caller's context keeps authenticated service calls working.
        public void Activate() => Http.HttpContext = new DefaultHttpContext { User = Principal };
    }
}
