using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using TenantEntity = NEXUS.Modules.Organization.Domain.Tenant;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class DocumentTenantBoundaryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _http = new();
    private readonly Mock<IStorageProvider> _storage = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly ConcurrentDictionary<string, byte[]> _storedFiles = new(StringComparer.Ordinal);
    private long _nextStorageKey;
    private readonly AppDbContext _db;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _projectOtherOrg = Guid.NewGuid();
    private readonly Guid _projectB = Guid.NewGuid();
    private readonly Guid _documentA = Guid.NewGuid();
    private readonly Guid _documentOtherOrg = Guid.NewGuid();
    private readonly Guid _documentB = Guid.NewGuid();
    private readonly Guid _unlinkedDocumentA = Guid.NewGuid();
    private readonly Guid _linkedTemplateOtherScope = Guid.NewGuid();
    private readonly Guid _linkedUnclassifiedTemplateInScope = Guid.NewGuid();
    private readonly Guid _unlinkedSharedTemplate = Guid.NewGuid();
    private readonly Guid _categoryA = Guid.NewGuid();
    private readonly Guid _categoryB = Guid.NewGuid();
    private readonly Guid _categoryOther = Guid.NewGuid();
    private readonly Guid _legacyCategory = Guid.NewGuid();
    private readonly Guid _organizationA = Guid.NewGuid();
    private readonly Guid _organizationOther = Guid.NewGuid();
    private readonly Guid _organizationTemplateA = Guid.NewGuid();
    private readonly Guid _organizationTemplateOther = Guid.NewGuid();

    public DocumentTenantBoundaryTests()
    {
        _connection.Open();
        _http.HttpContext = RequestContext(_tenantA, _userA);
        AppDbContext.ConfigureTenantResolution(_http);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        _db.Tenants.Add(new TenantEntity
        {
            Id = _tenantA, Code = "TENANT-A", DisplayName = "Tenant A",
            Status = TenantLifecycleStatus.Active, CreatedBy = "test"
        });
        _db.Projects.AddRange(
            NewProject(_projectA, _tenantA, "TENANT-A-PROJECT"),
            NewProject(_projectOtherOrg, _tenantA, "TENANT-A-OTHER-ORG-PROJECT"));
        _db.DocumentCategories.AddRange(
            new DocumentCategory
            {
                Id = _categoryA, TenantId = _tenantA, OrganizationId = _organizationA, Name = "Organization A",
                NameAr = "المؤسسة أ", NormalizedNameEn = "organization a", NormalizedNameAr = "المؤسسة أ",
                CreatedBy = "test"
            },
            new DocumentCategory
            {
                Id = _categoryOther, TenantId = _tenantA, OrganizationId = _organizationOther, Name = "Organization Other",
                NameAr = "المؤسسة الأخرى", NormalizedNameEn = "organization other", NormalizedNameAr = "المؤسسة الأخرى",
                CreatedBy = "test"
            },
            new DocumentCategory
            {
                Id = _legacyCategory, TenantId = _tenantA, Name = "Legacy Tenant Category", CreatedBy = "test"
            });
        _db.Organizations.AddRange(
            new Organization { Id = _organizationA, TenantId = _tenantA, Name = "Organization A", IsActive = true, CreatedBy = "test" },
            new Organization { Id = _organizationOther, TenantId = _tenantA, Name = "Organization Other", IsActive = true, CreatedBy = "test" });
        _db.Documents.AddRange(
            NewDocument(_documentA, _tenantA, _projectA, _userA, "Project A document", _categoryA),
            NewDocument(_documentOtherOrg, _tenantA, _projectOtherOrg, _userA, "Other organization document", _categoryA),
            NewDocument(_unlinkedDocumentA, _tenantA, null, _userA, "Unlinked owner document", _categoryA),
            NewTemplate(_linkedUnclassifiedTemplateInScope, _tenantA, _projectA, _userA, "Unclassified project template", _categoryA),
            NewTemplate(_linkedTemplateOtherScope, _tenantA, _projectOtherOrg, _userA, "Legacy project template", _categoryA),
            NewTemplate(_unlinkedSharedTemplate, _tenantA, null, _userB, "Unclassified legacy template", _categoryA),
            NewOrganizationTemplate(_organizationTemplateA, _tenantA, _userA, "Organization A template", _categoryA, _organizationA),
            NewOrganizationTemplate(_organizationTemplateOther, _tenantA, _userA, "Organization Other template", _categoryA, _organizationOther));
        _db.SaveChanges();

        _http.HttpContext = RequestContext(_tenantB, _userB);
        AppDbContext.ConfigureTenantResolution(_http);
        using (var tenantBDb = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options))
        {
            tenantBDb.Tenants.Add(new TenantEntity
            {
                Id = _tenantB, Code = "TENANT-B", DisplayName = "Tenant B",
                Status = TenantLifecycleStatus.Active, CreatedBy = "test"
            });
            tenantBDb.Projects.Add(NewProject(_projectB, _tenantB, "TENANT-B-PROJECT"));
            tenantBDb.DocumentCategories.Add(new DocumentCategory
            {
                Id = _categoryB, TenantId = _tenantB, Name = "Tenant B", CreatedBy = "test"
            });
            tenantBDb.Documents.Add(NewDocument(_documentB, _tenantB, null, _userB, "Tenant B document", _categoryB));
            tenantBDb.SaveChanges();
        }

        _http.HttpContext = RequestContext(_tenantA, _userA);
        AppDbContext.ConfigureTenantResolution(_http);

        _storage.SetupGet(provider => provider.ProviderId).Returns("test");
        _storage.Setup(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream content, string extension, CancellationToken cancellationToken) =>
            {
                await using var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellationToken);
                var bytes = copy.ToArray();
                var storageKey = $"{Interlocked.Increment(ref _nextStorageKey):x32}{extension}";
                _storedFiles[storageKey] = bytes;
                return new StoredFile("test", storageKey, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            });
        _storage.Setup(provider => provider.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string storageKey, CancellationToken _) => Task.FromResult(_storedFiles.ContainsKey(storageKey)));
        _storage.Setup(provider => provider.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string storageKey, CancellationToken _) => Task.FromResult<Stream>(new MemoryStream(_storedFiles[storageKey], writable: false)));
        _storage.Setup(provider => provider.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string storageKey, CancellationToken cancellationToken) =>
            {
                _storedFiles.TryRemove(storageKey, out _);
                return Task.CompletedTask;
            });
        _audit.Setup(audit => audit.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task Null_management_context_denies_document_records_and_project_linked_create()
    {
        var service = CreateService(null);

        var detail = await service.GetByIdAsync(_documentA);
        var paged = await service.GetPagedAsync(new DocumentListQuery());
        var download = await service.GetDownloadAsync(_documentA, null);
        var created = await service.CreateAsync(NewMutation(_projectA, _categoryA), _userA);

        Assert.True(detail.IsFailure);
        Assert.True(download.IsFailure);
        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.TotalCount);
        Assert.True(created.IsFailure);
        Assert.Equal(403, created.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Project_scope_preserves_project_documents_and_hides_unclassified_templates()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var projectDocument = await service.GetByIdAsync(_documentA);
        var unlinkedOwnerDocument = await service.GetByIdAsync(_unlinkedDocumentA);
        var paged = await service.GetPagedAsync(new DocumentListQuery());
        var legacyTemplate = await service.GetByIdAsync(_unlinkedSharedTemplate);
        var linkedLegacyTemplate = await service.GetByIdAsync(_linkedUnclassifiedTemplateInScope);

        Assert.True(projectDocument.IsSuccess);
        Assert.Equal(_documentA, projectDocument.Value!.Document.Id);
        Assert.True(unlinkedOwnerDocument.IsSuccess);
        Assert.Equal(_unlinkedDocumentA, unlinkedOwnerDocument.Value!.Document.Id);
        Assert.Contains(paged.Items, item => item.Id == _documentA);
        Assert.Contains(paged.Items, item => item.Id == _unlinkedDocumentA);
        Assert.DoesNotContain(paged.Items, item => item.Id == _documentOtherOrg || item.Id == _documentB);
        Assert.DoesNotContain(paged.Items, item => item.Id == _unlinkedSharedTemplate);
        Assert.DoesNotContain(paged.Items, item => item.Id == _linkedUnclassifiedTemplateInScope);
        Assert.True(legacyTemplate.IsFailure);
        Assert.True(linkedLegacyTemplate.IsFailure);
    }

    [Fact]
    public async Task Templates_are_isolated_by_authorized_organization_and_project_lookup_is_active_only()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var templateDetail = await service.GetByIdAsync(_organizationTemplateA);
        var otherOrganizationTemplate = await service.GetByIdAsync(_organizationTemplateOther);
        var projectTemplates = await service.GetPagedAsync(new DocumentListQuery(TemplatesOnly: true, ForProjectId: _projectA));
        var outsideOrganizationFilter = await service.GetPagedAsync(new DocumentListQuery(TemplatesOnly: true, OrganizationId: _organizationOther));
        var organizations = await service.GetOrganizationsAsync();

        Assert.True(templateDetail.IsSuccess);
        Assert.Equal("Organization A", templateDetail.Value!.Document.OrganizationName);
        Assert.Equal("Organization A", templateDetail.Value.Document.CategoryName);
        Assert.True(otherOrganizationTemplate.IsFailure);
        Assert.Contains(projectTemplates.Items, item => item.Id == _organizationTemplateA);
        Assert.DoesNotContain(projectTemplates.Items, item => item.Id == _organizationTemplateOther || item.Id == _linkedTemplateOtherScope);
        Assert.Empty(outsideOrganizationFilter.Items);
        var permittedOrganization = Assert.Single(organizations.Value!);
        Assert.Equal(_organizationA, permittedOrganization.Id);
    }

    [Fact]
    public async Task Organization_template_create_and_versioning_keep_stable_document_and_version_ids()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var create = await service.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, PdfBytes()), _userA);

        Assert.True(create.IsSuccess, create.Error?.Message);
        var createdId = create.Value!.Document.Id;
        var firstVersion = Assert.Single(create.Value.Versions);
        Assert.NotEqual(Guid.Empty, firstVersion.Id);
        var databaseDocument = await _db.Documents.Include(document => document.Versions)
            .SingleAsync(document => document.Id == createdId);
        Assert.True(databaseDocument.IsTemplate);
        Assert.Equal(_organizationA, databaseDocument.OrganizationId);
        Assert.Equal(_categoryA, databaseDocument.CategoryId);
        Assert.Null(databaseDocument.StoragePath);
        Assert.Null(databaseDocument.Versions.Single().StoragePath);
        Assert.Equal("test", databaseDocument.Versions.Single().StorageProviderId);
        Assert.NotEmpty(databaseDocument.Versions.Single().StorageKey!);
        Assert.Equal(64, databaseDocument.Versions.Single().Sha256!.Length);

        var updated = await service.AddVersionAsync(createdId, MakeFile(PdfBytes(), "policy-v2.pdf", "application/pdf"), "Second version", _userA);

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(createdId, updated.Value!.Document.Id);
        Assert.Equal(2, updated.Value.Document.Version);
        Assert.Equal(2, updated.Value.Versions.Count);
        Assert.Contains(updated.Value.Versions, version => version.Id == firstVersion.Id && version.VersionNumber == 1);
        Assert.Single(updated.Value.Versions, version => version.IsCurrent && version.VersionNumber == 2);
    }

    [Fact]
    public async Task Project_document_records_the_selected_template_and_stable_version_reference()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var template = await service.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, PdfBytes()), _userA);
        Assert.True(template.IsSuccess, template.Error?.Message);
        var selectedVersion = Assert.Single(template.Value!.Versions);

        var projectCopy = NewMutation(_projectA, _categoryA) with
        {
            SourceTemplateId = template.Value.Document.Id,
            SourceTemplateVersionId = selectedVersion.Id,
            SourceTemplateVersionNumber = selectedVersion.VersionNumber
        };
        var createdCopy = await service.CreateAsync(projectCopy, _userA);

        Assert.True(createdCopy.IsSuccess, createdCopy.Error?.Message);
        Assert.Equal(template.Value.Document.Id, createdCopy.Value!.Document.SourceTemplateId);
        Assert.Equal(selectedVersion.Id, createdCopy.Value.Document.SourceTemplateVersionId);
        Assert.Equal(selectedVersion.VersionNumber, createdCopy.Value.Document.SourceTemplateVersionNumber);

        var mismatchedVersion = projectCopy with { SourceTemplateVersionNumber = selectedVersion.VersionNumber + 1 };
        var rejected = await service.CreateAsync(mismatchedVersion, _userA);
        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);
    }

    [Fact]
    public async Task Template_archive_and_activate_are_audited_and_preserve_the_record()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var archived = await service.ArchiveAsync(_organizationTemplateA, _userA);
        var activeAfterArchive = await service.GetPagedAsync(new DocumentListQuery(TemplatesOnly: true, ForProjectId: _projectA));
        var historyAfterArchive = await service.GetPagedAsync(new DocumentListQuery(TemplatesOnly: true, OrganizationId: _organizationA, IncludeArchived: true));
        var activated = await service.ActivateAsync(_organizationTemplateA, _userA);
        var activeAfterActivate = await service.GetPagedAsync(new DocumentListQuery(TemplatesOnly: true, ForProjectId: _projectA));
        var persisted = await _db.Documents.SingleAsync(document => document.Id == _organizationTemplateA);

        Assert.True(archived.IsSuccess);
        Assert.DoesNotContain(activeAfterArchive.Items, item => item.Id == _organizationTemplateA);
        Assert.Contains(historyAfterArchive.Items, item => item.Id == _organizationTemplateA && item.IsArchived);
        Assert.True(activated.IsSuccess);
        Assert.Contains(activeAfterActivate.Items, item => item.Id == _organizationTemplateA && !item.IsArchived);
        Assert.False(persisted.IsDeleted);
        _audit.Verify(audit => audit.LogAsync(It.Is<AuditEntry>(entry => entry.EntityType == "DocumentTemplate" && entry.EntityId == _organizationTemplateA), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Cross_organization_template_create_missing_category_and_missing_template_manage_permission_are_denied_before_storage()
    {
        var scopedService = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var crossOrganization = await scopedService.CreateAsync(NewTemplateMutation(_organizationOther, _categoryA, PdfBytes()), _userA);
        var missingCategory = await scopedService.CreateAsync(
            NewTemplateMutation(_organizationA, _categoryA, PdfBytes()) with { CategoryId = null }, _userA);

        _http.HttpContext!.User = RequestContext(_tenantA, _userA, includeTemplateManage: false).User;
        var withoutManage = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var missingPermission = await withoutManage.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, PdfBytes()), _userA);

        Assert.True(crossOrganization.IsFailure);
        Assert.Equal(403, crossOrganization.Error?.StatusCode);
        Assert.True(missingCategory.IsFailure);
        Assert.Equal(400, missingCategory.Error?.StatusCode);
        Assert.True(missingPermission.IsFailure);
        Assert.Equal(403, missingPermission.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Service_operations_enforce_canonical_permissions_and_active_deny_before_storage_access()
    {
        _http.HttpContext!.User = RequestContext(_tenantA, _userA, permissions: [DocumentPermissions.View]).User;
        var viewOnly = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var createDenied = await viewOnly.CreateAsync(NewMutation(_projectA, _categoryA), _userA);
        var versionDenied = await viewOnly.AddVersionAsync(_documentA, MakeFile(PdfBytes(), "version.pdf", "application/pdf"), null, _userA);
        var archiveDenied = await viewOnly.ArchiveAsync(_documentA, _userA);
        var activateDenied = await viewOnly.ActivateAsync(_documentA, _userA);

        Assert.Equal(403, createDenied.Error?.StatusCode);
        Assert.Equal(403, versionDenied.Error?.StatusCode);
        Assert.Equal(403, archiveDenied.Error?.StatusCode);
        Assert.Equal(403, activateDenied.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        _http.HttpContext.User = RequestContext(_tenantA, _userA, deniedPermissions: [DocumentPermissions.View]).User;
        var deniedView = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var downloadDenied = await deniedView.GetDownloadAsync(_organizationTemplateA, null);
        var listDenied = await deniedView.GetPagedAsync(new DocumentListQuery());
        Assert.Equal(403, downloadDenied.Error?.StatusCode);
        Assert.Empty(listDenied.Items);
        _storage.Verify(provider => provider.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        _http.HttpContext.User = RequestContext(_tenantA, _userA, deniedPermissions: [DocumentPermissions.TemplateManage]).User;
        var deniedTemplateManage = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var templateCreateDenied = await deniedTemplateManage.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, PdfBytes()), _userA);
        Assert.Equal(403, templateCreateDenied.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Project_document_outside_organization_scope_is_denied_even_when_owned_by_current_user()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var outsideOrganization = await service.GetByIdAsync(_documentOtherOrg);
        var outsideOrganizationDownload = await service.GetDownloadAsync(_documentOtherOrg, null);

        Assert.True(outsideOrganization.IsFailure);
        Assert.True(outsideOrganizationDownload.IsFailure);
        _storage.Verify(provider => provider.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Tenant_mismatch_and_foreign_tenant_category_are_rejected()
    {
        var matchingHttpTenantButForeignManagementScope = CreateService(ScopedContext(_tenantB, _userA, _projectA));
        var deniedByContextMismatch = await matchingHttpTenantButForeignManagementScope.GetByIdAsync(_documentA);
        Assert.True(deniedByContextMismatch.IsFailure);

        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var createWithForeignCategory = await service.CreateAsync(NewMutation(_projectA, _categoryB), _userA);

        Assert.True(createWithForeignCategory.IsFailure);
        Assert.Equal(400, createWithForeignCategory.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Project_linked_upload_outside_management_scope_is_denied_before_storage_write()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var result = await service.CreateAsync(NewMutation(_projectOtherOrg, _categoryA), _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(_db.ChangeTracker.Entries<Document>(), entry => entry.State == EntityState.Added);
    }

    [Theory]
    [InlineData("document.txt", "text/plain")]
    [InlineData("document.csv", "text/csv")]
    [InlineData("document.png", "image/png")]
    [InlineData("document.jpeg", "image/jpeg")]
    public async Task Unsupported_extensions_are_rejected(string fileName, string contentType)
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var mutation = NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(Encoding.UTF8.GetBytes("anything"), fileName, contentType)
        };

        var result = await service.CreateAsync(mutation, _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("../document.pdf")]
    [InlineData("..\\document.pdf")]
    [InlineData("C:\\document.pdf")]
    [InlineData("bad\u0001name.pdf")]
    public async Task Invalid_filenames_are_rejected_before_storage(string fileName)
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var mutation = NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(PdfBytes(), fileName, "application/pdf")
        };

        var result = await service.CreateAsync(mutation, _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalid_pdf_and_legacy_office_signatures_are_rejected()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var badPdf = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(Encoding.UTF8.GetBytes("not a PDF"), "document.pdf", "application/pdf")
        }, _userA);
        var truncatedPdf = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(Encoding.ASCII.GetBytes("%PDF-1.7"), "truncated.pdf", "application/pdf")
        }, _userA);
        var badDoc = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(Encoding.UTF8.GetBytes("not an OLE file"), "document.doc", "application/msword")
        }, _userA);

        Assert.True(badPdf.IsFailure);
        Assert.True(truncatedPdf.IsFailure);
        Assert.True(badDoc.IsFailure);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("legacy.doc", "application/msword", true)]
    [InlineData("legacy.xls", "application/vnd.ms-excel", false)]
    public async Task Legacy_office_files_with_valid_compound_headers_are_accepted(string fileName, string contentType, bool isWord)
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var mutation = NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(CompoundOfficeFile(isWord), fileName, contentType)
        };

        var result = await service.CreateAsync(mutation, _userA);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Legacy_office_extension_spoofing_and_unknown_ole_content_are_rejected()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var template = await service.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, CompoundOfficeFile(isWord: true)) with
        {
            File = MakeFile(CompoundOfficeFile(isWord: true), "approved-template.doc", "application/msword")
        }, _userA);
        Assert.True(template.IsSuccess, template.Error?.Message);
        var templateVersion = Assert.Single(template.Value!.Versions);

        var xlsContentNamedDoc = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(CompoundOfficeFile(isWord: false), "spoofed.doc", "application/msword")
        }, _userA);
        var unsupportedOleContent = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(CompoundOfficeFile(isWord: false, mainStreamName: "PowerPoint Document"), "unsupported.doc", "application/msword"),
            SourceTemplateId = template.Value.Document.Id,
            SourceTemplateVersionId = templateVersion.Id,
            SourceTemplateVersionNumber = templateVersion.VersionNumber
        }, _userA);

        Assert.True(xlsContentNamedDoc.IsFailure);
        Assert.Equal("VALIDATION_ERROR", xlsContentNamedDoc.Error?.Code);
        Assert.Contains("selected file extension", xlsContentNamedDoc.Error?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(unsupportedOleContent.IsFailure);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", unsupportedOleContent.Error?.Code);
        Assert.NotEqual("DOCUMENT_TEMPLATE_FILE_TYPE_MISMATCH", unsupportedOleContent.Error?.Code);
        Assert.Contains("could not be verified or is unsupported", unsupportedOleContent.Error?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("تعذّر التحقق", unsupportedOleContent.Error?.Message);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Valid_detected_template_mismatch_reports_expected_and_detected_types_in_english_and_arabic()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var template = await service.CreateAsync(NewTemplateMutation(_organizationA, _categoryA, CompoundOfficeFile(isWord: true)) with
        {
            File = MakeFile(CompoundOfficeFile(isWord: true), "approved-template.doc", "application/msword")
        }, _userA);
        Assert.True(template.IsSuccess, template.Error?.Message);
        var templateVersion = Assert.Single(template.Value!.Versions);

        var projectUpload = NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(CompoundOfficeFile(isWord: false), "project-document.xls", "application/vnd.ms-excel"),
            SourceTemplateId = template.Value.Document.Id,
            SourceTemplateVersionId = templateVersion.Id,
            SourceTemplateVersionNumber = templateVersion.VersionNumber
        };
        var result = await service.CreateAsync(projectUpload, _userA);

        Assert.True(result.IsFailure);
        Assert.Equal("DOCUMENT_TEMPLATE_FILE_TYPE_MISMATCH", result.Error?.Code);
        Assert.Contains("Word 97–2003 (.doc)", result.Error?.Message);
        Assert.Contains("Excel 97–2003 (.xls)", result.Error?.Message);
        Assert.Contains("مستند Word 97–2003 (.doc)", result.Error?.Message);
        Assert.Contains("جدول Excel 97–2003 (.xls)", result.Error?.Message);
        Assert.Equal("Word 97–2003 (.doc)", result.Error?.Details?["expectedType"]);
        Assert.Equal("Excel 97–2003 (.xls)", result.Error?.Details?["detectedType"]);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("legacy.doc", "application/msword")]
    [InlineData("legacy.xls", "application/vnd.ms-excel")]
    public async Task Legacy_office_signature_without_a_compound_header_is_rejected(string fileName, string contentType)
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var result = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile([0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1], fileName, contentType)
        }, _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, "malformed.doc", "application/msword")]
    [InlineData(false, "malformed.xls", "application/vnd.ms-excel")]
    public async Task Legacy_office_named_compound_stream_without_a_plausible_payload_is_rejected(
        bool isWord,
        string fileName,
        string contentType)
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var result = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(CompoundOfficeFile(isWord, validOfficePayload: false), fileName, contentType)
        }, _userA);

        Assert.True(result.IsFailure);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", result.Error?.Code);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Valid_ooxml_packages_are_accepted_and_mismatched_or_fake_zip_packages_are_rejected()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var validDocx = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: true), "document.docx", DocumentContentTypes.ForFileName("document.docx"))
        }, _userA);
        var validXlsx = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: false), "sheet.xlsx", DocumentContentTypes.ForFileName("sheet.xlsx"))
        }, _userA);
        var mismatchedPackage = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: false), "document.docx", DocumentContentTypes.ForFileName("document.docx"))
        }, _userA);
        var fakeZip = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile([0x50, 0x4b, 0x03, 0x04, 1, 2, 3, 4], "fake.xlsx", DocumentContentTypes.ForFileName("fake.xlsx"))
        }, _userA);
        var invalidMainXml = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: true, validMainXml: false), "invalid.docx", DocumentContentTypes.ForFileName("invalid.docx"))
        }, _userA);

        Assert.True(validDocx.IsSuccess, validDocx.Error?.Message);
        Assert.True(validXlsx.IsSuccess, validXlsx.Error?.Message);
        Assert.True(mismatchedPackage.IsFailure);
        Assert.True(fakeZip.IsFailure);
        Assert.True(invalidMainXml.IsFailure);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Strict_ooxml_docx_and_xlsx_packages_are_accepted()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var strictDocx = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: true, strict: true), "strict.docx", DocumentContentTypes.ForFileName("strict.docx"))
        }, _userA);
        var strictXlsx = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(OpenXmlPackage(isWord: false, strict: true), "strict.xlsx", DocumentContentTypes.ForFileName("strict.xlsx"))
        }, _userA);

        Assert.True(strictDocx.IsSuccess, strictDocx.Error?.Message);
        Assert.True(strictXlsx.IsSuccess, strictXlsx.Error?.Message);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Upload_size_limit_accepts_exact_boundary_and_rejects_over_limit()
    {
        var validPdf = PdfBytes();
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA), maxUploadBytes: validPdf.Length);
        var exactLimit = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile(validPdf, "exact.pdf", "application/pdf")
        }, _userA);
        var overLimit = await service.CreateAsync(NewMutation(_projectA, _categoryA) with
        {
            File = MakeFile([.. validPdf, 0], "over.pdf", "application/pdf")
        }, _userA);

        Assert.True(exactLimit.IsSuccess, exactLimit.Error?.Message);
        Assert.True(overLimit.IsFailure);
        Assert.Equal(400, overLimit.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failed_metadata_and_audit_persistence_delete_new_file_bytes()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        _audit.Setup(audit => audit.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict("Audit persistence failed.")));
        var documentCountBefore = await _db.Documents.CountAsync();

        var result = await service.CreateAsync(NewMutation(_projectA, _categoryA), _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Empty(_storedFiles);
        Assert.Equal(documentCountBefore, await _db.Documents.CountAsync());
    }

    [Fact]
    public async Task Managed_categories_support_bilingual_crud_and_organization_local_duplicates_without_changing_references()
    {
        _http.HttpContext = CategoryAdminRequestContext(_tenantA, _userA);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var created = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "  Site   Safety ", "\u062e\u0637\u0629 \u0627\u0644\u0633\u0644\u0627\u0645\u0629"),
            _userA);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal("Site Safety", created.Value!.NameEn);
        Assert.Equal("\u062e\u0637\u0629 \u0627\u0644\u0633\u0644\u0627\u0645\u0629", created.Value.NameAr);
        Assert.Equal(_organizationA, created.Value.OrganizationId);
        Assert.True(created.Value.IsActive);
        var arabicSearch = await service.GetManagedCategoriesAsync(
            _organizationA, "\u062e\u0637\u0629 \u0627\u0644\u0633\u0644\u0627\u0645\u0629", includeInactive: false);
        Assert.Contains(arabicSearch.Value!, category => category.Id == created.Value.Id);
        var missingEnglish = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, " ", "\u0627\u0633\u0645"), _userA);
        var missingArabic = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "Name", " "), _userA);
        Assert.True(missingEnglish.IsFailure);
        Assert.Equal(400, missingEnglish.Error?.StatusCode);
        Assert.True(missingArabic.IsFailure);
        Assert.Equal(400, missingArabic.Error?.StatusCode);

        var duplicate = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "site safety", "\u062e\u0637\u0629 \u062c\u062f\u064a\u062f\u0629"),
            _userA);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(409, duplicate.Error?.StatusCode);
        var duplicateArabic = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "Different English", "\u062e\u0637\u0629 \u0627\u0644\u0633\u0644\u0627\u0645\u0629"),
            _userA);
        Assert.True(duplicateArabic.IsFailure);
        Assert.Equal(409, duplicateArabic.Error?.StatusCode);

        var secondOrganizationService = CreateService(ScopedContext(_tenantA, _userA, _projectOtherOrg));
        var sameNameInOtherOrganization = await secondOrganizationService.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationOther, "Site Safety", "\u062e\u0637\u0629 \u0627\u0644\u0633\u0644\u0627\u0645\u0629"),
            _userA);
        Assert.True(sameNameInOtherOrganization.IsSuccess, sameNameInOtherOrganization.Error?.Message);

        var referencedDocument = await _db.Documents.SingleAsync(document => document.Id == _documentA);
        referencedDocument.CategoryId = created.Value.Id;
        await _db.SaveChangesAsync();
        var edited = await service.UpdateCategoryAsync(created.Value.Id,
            new DocumentCategoryUpdateRequest("Site Safety Standard", "\u0645\u0639\u064a\u0627\u0631 \u0627\u0644\u0633\u0644\u0627\u0645\u0629", "Updated"), _userA);

        Assert.True(edited.IsSuccess, edited.Error?.Message);
        Assert.Equal(created.Value.Id, edited.Value!.Id);
        Assert.Equal("Site Safety Standard", edited.Value.NameEn);
        Assert.Equal(created.Value.Id, (await _db.Documents.SingleAsync(document => document.Id == _documentA)).CategoryId);
        _audit.Verify(audit => audit.LogAsync(It.Is<AuditEntry>(entry =>
            entry.EntityType == "DocumentCategory" && entry.EntityId == created.Value.Id && entry.Action == "Update"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivating_category_hides_it_from_template_picker_and_preserves_historical_references()
    {
        _http.HttpContext = CategoryAdminRequestContext(_tenantA, _userA);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var deactivated = await service.SetCategoryStatusAsync(_categoryA, false, _userA);

        Assert.True(deactivated.IsSuccess, deactivated.Error?.Message);
        Assert.False(deactivated.Value!.IsActive);
        Assert.Equal(_categoryA, (await _db.Documents.SingleAsync(document => document.Id == _organizationTemplateA)).CategoryId);

        var picker = await service.GetCategoriesAsync(_organizationA);
        Assert.True(picker.IsSuccess);
        Assert.DoesNotContain(picker.Value!, category => category.Id == _categoryA);
        var activeManagementList = await service.GetManagedCategoriesAsync(_organizationA, null, includeInactive: false);
        Assert.True(activeManagementList.IsSuccess);
        Assert.DoesNotContain(activeManagementList.Value!, category => category.Id == _categoryA);

        var allIncludingInactive = await service.GetManagedCategoriesAsync(_organizationA, "Organization A", includeInactive: true);
        Assert.True(allIncludingInactive.IsSuccess);
        var historicalCategory = Assert.Single(allIncludingInactive.Value!, category => category.Id == _categoryA);
        Assert.False(historicalCategory.IsActive);
        Assert.True(historicalCategory.UsageCount > 0);

        var inactiveTemplate = await service.CreateAsync(
            NewTemplateMutation(_organizationA, _categoryA, PdfBytes()), _userA);
        Assert.True(inactiveTemplate.IsFailure);
        Assert.Equal(400, inactiveTemplate.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var reactivated = await service.SetCategoryStatusAsync(_categoryA, true, _userA);
        Assert.True(reactivated.IsSuccess, reactivated.Error?.Message);
        Assert.Contains((await service.GetCategoriesAsync(_organizationA)).Value!, category => category.Id == _categoryA);
    }

    [Fact]
    public async Task Category_management_requires_explicit_organization_scope_and_rejects_cross_organization_template_links()
    {
        _http.HttpContext = CategoryAdminRequestContext(_tenantA, _userA);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var foreignList = await service.GetManagedCategoriesAsync(_organizationOther, null, includeInactive: true);
        Assert.True(foreignList.IsFailure);
        Assert.Equal(404, foreignList.Error?.StatusCode);

        var foreignCreate = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationOther, "Foreign", "\u062e\u0627\u0631\u062c\u064a"), _userA);
        Assert.True(foreignCreate.IsFailure);
        Assert.Equal(404, foreignCreate.Error?.StatusCode);

        var foreignCategoryTemplate = await service.CreateAsync(
            NewTemplateMutation(_organizationA, _categoryOther, PdfBytes()), _userA);
        var foreignTenantCategoryTemplate = await service.CreateAsync(
            NewTemplateMutation(_organizationA, _categoryB, PdfBytes()), _userA);
        var foreignProjectDocument = await service.CreateAsync(NewMutation(_projectA, _categoryOther), _userA);

        Assert.True(foreignCategoryTemplate.IsFailure);
        Assert.Equal(400, foreignCategoryTemplate.Error?.StatusCode);
        Assert.True(foreignTenantCategoryTemplate.IsFailure);
        Assert.Equal(400, foreignTenantCategoryTemplate.Error?.StatusCode);
        Assert.True(foreignProjectDocument.IsFailure);
        Assert.Equal(400, foreignProjectDocument.Error?.StatusCode);
        _storage.Verify(provider => provider.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Legacy_tenant_category_is_visible_for_history_but_cannot_be_edited_or_deactivated()
    {
        _http.HttpContext = CategoryAdminRequestContext(_tenantA, _userA);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var legacy = await service.GetManagedCategoryAsync(_legacyCategory);
        Assert.True(legacy.IsSuccess, legacy.Error?.Message);
        Assert.Null(legacy.Value!.OrganizationId);
        Assert.False(legacy.Value.CanManage);
        var visibleCategories = await service.GetManagedCategoriesAsync(_organizationA, null, includeInactive: true);
        Assert.True(visibleCategories.IsSuccess);
        Assert.Contains(visibleCategories.Value!, category => category.Id == _legacyCategory && !category.CanManage);
        Assert.DoesNotContain(visibleCategories.Value!, category => category.Id == _categoryOther);

        var edit = await service.UpdateCategoryAsync(_legacyCategory,
            new DocumentCategoryUpdateRequest("Legacy updated", "\u0645\u062d\u062f\u062b"), _userA);
        var deactivate = await service.SetCategoryStatusAsync(_legacyCategory, false, _userA);

        Assert.True(edit.IsFailure);
        Assert.Equal(409, edit.Error?.StatusCode);
        Assert.True(deactivate.IsFailure);
        Assert.Equal(409, deactivate.Error?.StatusCode);
    }

    [Fact]
    public async Task Category_operations_enforce_their_exact_permission_and_active_deny()
    {
        _http.HttpContext = RequestContext(_tenantA, _userA);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var missingPermission = await service.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "No access", "\u0645\u0645\u0646\u0648\u0639"), _userA);

        Assert.True(missingPermission.IsFailure);
        Assert.Equal(403, missingPermission.Error?.StatusCode);

        _http.HttpContext = RequestContext(_tenantA, _userA, permissions: [DocumentCategoryPermissions.View]);
        var viewOnlyService = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var missingEdit = await viewOnlyService.UpdateCategoryAsync(_categoryA,
            new DocumentCategoryUpdateRequest("Changed", "\u0645\u063a\u064a\u0631"), _userA);
        var missingStatus = await viewOnlyService.SetCategoryStatusAsync(_categoryA, false, _userA);
        Assert.True(missingEdit.IsFailure);
        Assert.Equal(403, missingEdit.Error?.StatusCode);
        Assert.True(missingStatus.IsFailure);
        Assert.Equal(403, missingStatus.Error?.StatusCode);

        _http.HttpContext = CategoryAdminRequestContext(_tenantA, _userA, deniedPermissions: [DocumentCategoryPermissions.Create]);
        var deniedService = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var denied = await deniedService.CreateCategoryAsync(
            new DocumentCategoryCreateRequest(_organizationA, "Denied", "\u0645\u0631\u0641\u0648\u0636"), _userA);

        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);
        Assert.DoesNotContain(await _db.DocumentCategories.ToListAsync(), category => category.Name == "Denied");
    }

    [Fact]
    public async Task Template_category_dropdown_returns_active_categories_for_selected_organization_only()
    {
        var result = await CreateService(ScopedContext(_tenantA, _userA, _projectA))
            .GetCategoriesAsync(_organizationA);

        Assert.True(result.IsSuccess);
        var category = Assert.Single(result.Value!);
        Assert.Equal(_categoryA, category.Id);
        Assert.Equal(_organizationA, category.OrganizationId);
        Assert.Equal("Organization A", category.NameEn);
        Assert.Equal("\u0627\u0644\u0645\u0624\u0633\u0633\u0629 \u0623", category.NameAr);
        Assert.DoesNotContain(result.Value!, item => item.Id == _categoryB);
        Assert.DoesNotContain(result.Value!, item => item.Id == _categoryOther);
    }

    [Fact]
    public async Task Template_category_option_lookup_uses_only_template_permissions_and_includes_selected_organization_and_tenant_shared_categories()
    {
        _http.HttpContext = RequestContext(_tenantA, _userA, permissions:
        [
            DocumentPermissions.Create,
            DocumentPermissions.TemplateManage
        ]);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var result = await service.GetTemplateCategoryOptionsAsync(_organizationA);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!, category => category.Id == _categoryA
            && category.NameEn == "Organization A"
            && category.NameAr == "\u0627\u0644\u0645\u0624\u0633\u0633\u0629 \u0623");
        Assert.Contains(result.Value!, category => category.Id == _legacyCategory
            && category.NameEn == "Legacy Tenant Category");
        Assert.DoesNotContain(result.Value!, item => item.Id == _categoryOther || item.Id == _categoryB);

        var crossOrganization = await service.GetTemplateCategoryOptionsAsync(_organizationOther);
        Assert.True(crossOrganization.IsFailure);
        Assert.Equal(404, crossOrganization.Error?.StatusCode);

        var categoryEntity = await _db.DocumentCategories.SingleAsync(item => item.Id == _categoryA);
        categoryEntity.IsActive = false;
        await _db.SaveChangesAsync();
        var afterDeactivation = await service.GetTemplateCategoryOptionsAsync(_organizationA);
        Assert.True(afterDeactivation.IsSuccess);
        Assert.DoesNotContain(afterDeactivation.Value!, item => item.Id == _categoryA);
        Assert.Contains(afterDeactivation.Value!, item => item.Id == _legacyCategory);

        var sharedCategory = await _db.DocumentCategories.SingleAsync(item => item.Id == _legacyCategory);
        sharedCategory.IsActive = false;
        await _db.SaveChangesAsync();
        var afterSharedDeactivation = await service.GetTemplateCategoryOptionsAsync(_organizationA);
        Assert.True(afterSharedDeactivation.IsSuccess);
        Assert.Empty(afterSharedDeactivation.Value!);
    }

    [Fact]
    public async Task Project_upload_category_lookup_and_upload_need_create_permission_only_and_stay_project_scoped()
    {
        _http.HttpContext = RequestContext(_tenantA, _userA, permissions: [DocumentPermissions.Create]);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var options = await service.GetProjectUploadCategoryOptionsAsync(_projectA);

        Assert.True(options.IsSuccess, options.Error?.Message);
        Assert.Contains(options.Value!, category => category.Id == _categoryA);
        Assert.Contains(options.Value!, category => category.Id == _legacyCategory);
        Assert.DoesNotContain(options.Value!, category => category.Id == _categoryOther || category.Id == _categoryB);
        var outOfScope = await service.GetProjectUploadCategoryOptionsAsync(_projectOtherOrg);
        Assert.True(outOfScope.IsFailure);
        Assert.Equal(404, outOfScope.Error?.StatusCode);

        var uploaded = await service.CreateAsync(NewMutation(_projectA, _legacyCategory), _userA);
        Assert.True(uploaded.IsSuccess, uploaded.Error?.Message);
        Assert.Equal(_legacyCategory, uploaded.Value!.Document.CategoryId);

        _http.HttpContext = RequestContext(_tenantA, _userA, permissions: [DocumentPermissions.Create],
            deniedPermissions: [DocumentPermissions.Create]);
        var denied = await CreateService(ScopedContext(_tenantA, _userA, _projectA))
            .GetProjectUploadCategoryOptionsAsync(_projectA);
        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);
    }

    [Fact]
    public async Task Template_upload_accepts_an_active_tenant_shared_category()
    {
        _http.HttpContext = RequestContext(_tenantA, _userA, permissions:
        [
            DocumentPermissions.Create,
            DocumentPermissions.TemplateManage
        ]);
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var uploaded = await service.CreateAsync(
            NewTemplateMutation(_organizationA, _legacyCategory, PdfBytes()), _userA);

        Assert.True(uploaded.IsSuccess, uploaded.Error?.Message);
        Assert.Equal(_organizationA, uploaded.Value!.Document.OrganizationId);
        Assert.Equal(_legacyCategory, uploaded.Value.Document.CategoryId);
    }

    [Theory]
    [InlineData(DocumentPermissions.Create)]
    [InlineData(DocumentPermissions.TemplateManage)]
    public async Task Template_category_option_lookup_rejects_missing_or_denied_operation_permissions(string permission)
    {
        _http.HttpContext = RequestContext(_tenantA, _userA, permissions: [DocumentPermissions.Create]);
        var missingTemplateManage = await CreateService(ScopedContext(_tenantA, _userA, _projectA))
            .GetTemplateCategoryOptionsAsync(_organizationA);
        Assert.True(missingTemplateManage.IsFailure);
        Assert.Equal(403, missingTemplateManage.Error?.StatusCode);

        _http.HttpContext = RequestContext(_tenantA, _userA, permissions:
        [
            DocumentPermissions.Create,
            DocumentPermissions.TemplateManage
        ], deniedPermissions: [permission]);
        var denied = await CreateService(ScopedContext(_tenantA, _userA, _projectA))
            .GetTemplateCategoryOptionsAsync(_organizationA);
        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);
    }

    [Fact]
    public async Task Existing_document_category_labels_never_resolve_across_tenants()
    {
        var document = await _db.Documents.SingleAsync(item => item.Id == _documentA);
        document.CategoryId = _categoryB;
        await _db.SaveChangesAsync();

        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));
        var detail = await service.GetByIdAsync(_documentA);
        var page = await service.GetPagedAsync(new DocumentListQuery(ProjectId: _projectA));

        Assert.True(detail.IsSuccess);
        Assert.Null(detail.Value!.Document.CategoryName);
        var listItem = Assert.Single(page.Items, item => item.Id == _documentA);
        Assert.Null(listItem.CategoryName);
    }

    private DocumentService CreateService(ManagementContext? context, long? maxUploadBytes = null)
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(context);
        IConfiguration? configuration = maxUploadBytes.HasValue
            ? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:MaxUploadBytes"] = maxUploadBytes.Value.ToString()
            }).Build()
            : null;
        return new DocumentService(_db, _storage.Object, _http, _audit.Object, management.Object, configuration);
    }

    private ManagementContext ScopedContext(Guid tenantId, Guid userId, Guid projectId)
    {
        var organizationId = projectId == _projectOtherOrg ? _organizationOther : _organizationA;
        var organizationName = organizationId == _organizationOther ? "Organization Other" : "Organization A";
        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })
        {
            OrganizationIds = new HashSet<Guid> { organizationId },
            OrganizationReferences = [new ManagementEntityReference(organizationId, organizationName)],
            ProjectOrganizationIds = new Dictionary<Guid, Guid> { [projectId] = organizationId }
        };
        return new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            projectId,
            scope);
    }

    private static DefaultHttpContext RequestContext(
        Guid tenantId,
        Guid userId,
        bool includeTemplateManage = true,
        IReadOnlyCollection<string>? permissions = null,
        IReadOnlyCollection<string>? deniedPermissions = null)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var effectivePermissions = permissions?.ToList() ?? new List<string>
        {
            DocumentPermissions.View,
            DocumentPermissions.Create,
            DocumentPermissions.Edit,
            DocumentPermissions.Archive,
            DocumentPermissions.TemplateManage
        };
        if (!includeTemplateManage)
            effectivePermissions.RemoveAll(permission => permission == DocumentPermissions.TemplateManage);
        foreach (var permission in effectivePermissions.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim("permission", permission));
        foreach (var deniedPermission in deniedPermissions ?? Array.Empty<string>())
            claims.Add(new Claim("deny", deniedPermission));
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }

    private static DefaultHttpContext CategoryAdminRequestContext(
        Guid tenantId,
        Guid userId,
        IReadOnlyCollection<string>? deniedPermissions = null) => RequestContext(
            tenantId,
            userId,
            permissions:
            [
                DocumentPermissions.View,
                DocumentPermissions.Create,
                DocumentPermissions.Edit,
                DocumentPermissions.Archive,
                DocumentPermissions.TemplateManage,
                DocumentCategoryPermissions.View,
                DocumentCategoryPermissions.Create,
                DocumentCategoryPermissions.Edit,
                DocumentCategoryPermissions.ActivateDeactivate
            ],
            deniedPermissions: deniedPermissions);

    private static Project NewProject(Guid id, Guid tenantId, string code) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = code,
        Code = code,
        Description = "Document scope fixture",
        Status = "Active",
        Currency = "USD",
        CreatedBy = "test",
        RowVersion = new byte[8]
    };

    private static Document NewDocument(Guid id, Guid tenantId, Guid? projectId, Guid ownerId, string name, Guid categoryId) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        ProjectId = projectId,
        OwnerId = ownerId,
        CategoryId = categoryId,
        FileName = "document.pdf",
        FileType = "PDF",
        FileSize = 8,
        ContentType = "application/pdf",
        StoragePath = $"{id:N}/document.pdf",
        Status = "Active",
        CreatedBy = "test"
    };

    private static Document NewTemplate(Guid id, Guid tenantId, Guid? projectId, Guid ownerId, string name, Guid categoryId)
    {
        var document = NewDocument(id, tenantId, projectId, ownerId, name, categoryId);
        document.IsTemplate = true;
        return document;
    }

    private static Document NewOrganizationTemplate(Guid id, Guid tenantId, Guid ownerId, string name, Guid categoryId, Guid organizationId)
    {
        var document = NewTemplate(id, tenantId, null, ownerId, name, categoryId);
        document.OrganizationId = organizationId;
        return document;
    }

    private DocumentMutationDto NewMutation(Guid? projectId, Guid categoryId) => new(
        "Uploaded document",
        null,
        projectId,
        categoryId,
        File: MakeFile(PdfBytes(), "document.pdf", "application/pdf"));

    private DocumentMutationDto NewTemplateMutation(Guid organizationId, Guid categoryId, byte[] bytes) => new(
        "Approved template",
        "Organization standard",
        null,
        categoryId,
        IsTemplate: true,
        File: MakeFile(bytes, "approved-template.pdf", "application/pdf"),
        OrganizationId: organizationId);

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

    private static byte[] CompoundOfficeFile(bool isWord, string? mainStreamName = null, bool validOfficePayload = true)
    {
        const int sectorSize = 512;
        const int directorySector = 1;
        const int mainStreamStart = 2;
        const int mainStreamSectors = 8;
        var companionStreamStart = mainStreamStart + mainStreamSectors;
        const int companionStreamSectors = 8;
        var sectorCount = isWord ? companionStreamStart + companionStreamSectors : companionStreamStart;
        var bytes = new byte[sectorSize + sectorCount * sectorSize];
        var header = bytes.AsSpan(0, sectorSize);
        new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[24..], 0x003e);
        BinaryPrimitives.WriteUInt16LittleEndian(header[26..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..], 0xfffe);
        BinaryPrimitives.WriteUInt16LittleEndian(header[30..], 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], directorySector);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(header[60..], 0xfffffffe);
        BinaryPrimitives.WriteUInt32LittleEndian(header[68..], 0xfffffffe);
        for (var index = 0; index < 109; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(header[(76 + index * sizeof(uint))..], 0xffffffff);
        BinaryPrimitives.WriteUInt32LittleEndian(header[76..], 0);

        var fat = new uint[sectorSize / sizeof(uint)];
        Array.Fill(fat, 0xffffffff);
        fat[0] = 0xfffffffd;
        fat[directorySector] = 0xfffffffe;
        SetChain(fat, mainStreamStart, mainStreamSectors);
        if (isWord) SetChain(fat, companionStreamStart, companionStreamSectors);
        var fatBytes = bytes.AsSpan(sectorSize, sectorSize);
        for (var index = 0; index < fat.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(fatBytes[(index * sizeof(uint))..], fat[index]);

        var directory = bytes.AsSpan(sectorSize + directorySector * sectorSize, sectorSize);
        WriteDirectoryEntry(directory, 0, "Root Entry", 5, 0xffffffff, 0UL, child: 1);
        var resolvedMainStreamName = mainStreamName ?? (isWord ? "WordDocument" : "Workbook");
        WriteDirectoryEntry(directory, 1, resolvedMainStreamName, 2,
            (uint)mainStreamStart, (ulong)(mainStreamSectors * sectorSize), right: isWord ? 2u : 0xffffffff);
        if (isWord)
            WriteDirectoryEntry(directory, 2, "0Table", 2, (uint)companionStreamStart, (ulong)(companionStreamSectors * sectorSize));

        if (validOfficePayload)
        {
            var payload = bytes.AsSpan(sectorSize + mainStreamStart * sectorSize, 32);
            if (string.Equals(resolvedMainStreamName, "WordDocument", StringComparison.OrdinalIgnoreCase))
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload, 0xA5EC);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(2), 0x00C1);
            }
            else if (string.Equals(resolvedMainStreamName, "Workbook", StringComparison.OrdinalIgnoreCase)
                || string.Equals(resolvedMainStreamName, "Book", StringComparison.OrdinalIgnoreCase))
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x0809);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(2), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(4), 0x0600);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(6), 0x0005);
            }
        }
        return bytes;
    }

    private static void SetChain(uint[] fat, int startSector, int sectorCount)
    {
        for (var offset = 0; offset < sectorCount; offset++)
            fat[startSector + offset] = offset == sectorCount - 1 ? 0xfffffffe : (uint)(startSector + offset + 1);
    }

    private static void WriteDirectoryEntry(
        Span<byte> directory,
        int entryIndex,
        string name,
        byte entryType,
        uint startSector,
        ulong size,
        uint left = 0xffffffff,
        uint right = 0xffffffff,
        uint child = 0xffffffff)
    {
        var entry = directory.Slice(entryIndex * 128, 128);
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        nameBytes.CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], (ushort)nameBytes.Length);
        entry[66] = entryType;
        BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], left);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], right);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], child);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], startSector);
        BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], size);
    }

    private static FormFile MakeFile(byte[] bytes, string fileName, string contentType) => new(
        new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };

    private static byte[] OpenXmlPackage(bool isWord, bool validMainXml = true, bool strict = false)
    {
        using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var partName = isWord ? "word/document.xml" : "xl/workbook.xml";
            var contentType = isWord
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
            var contentTypes = archive.CreateEntry("[Content_Types].xml");
            using (var writer = new StreamWriter(contentTypes.Open(), Encoding.UTF8))
                writer.Write($"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{partName}\" ContentType=\"{contentType}\" /></Types>");
            var mainPart = archive.CreateEntry(partName);
            using var mainWriter = new StreamWriter(mainPart.Open(), Encoding.UTF8);
            var rootName = isWord ? "document" : "workbook";
            var rootNamespace = (isWord, strict) switch
            {
                (true, false) => "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
                (true, true) => "http://purl.oclc.org/ooxml/wordprocessingml/main",
                (false, false) => "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
                _ => "http://purl.oclc.org/ooxml/spreadsheetml/main"
            };
            mainWriter.Write(validMainXml ? $"<{rootName} xmlns=\"{rootNamespace}\" />" : $"<{rootName}>");
        }
        return package.ToArray();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
