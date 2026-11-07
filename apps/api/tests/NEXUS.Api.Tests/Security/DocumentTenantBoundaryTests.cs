using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class DocumentTenantBoundaryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _http = new();
    private readonly Mock<IStorageProvider> _storage = new();
    private readonly AppDbContext _db;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _projectOtherOrg = Guid.NewGuid();
    private readonly Guid _documentA = Guid.NewGuid();
    private readonly Guid _documentOtherOrg = Guid.NewGuid();
    private readonly Guid _documentB = Guid.NewGuid();
    private readonly Guid _unlinkedDocumentA = Guid.NewGuid();
    private readonly Guid _categoryA = Guid.NewGuid();
    private readonly Guid _categoryB = Guid.NewGuid();

    public DocumentTenantBoundaryTests()
    {
        _connection.Open();
        _http.HttpContext = RequestContext(_tenantA, _userA);
        AppDbContext.ConfigureTenantResolution(_http);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        _db.Projects.AddRange(
            NewProject(_projectA, _tenantA, "TENANT-A-PROJECT"),
            NewProject(_projectOtherOrg, _tenantA, "TENANT-A-OTHER-ORG-PROJECT"),
            NewProject(Guid.NewGuid(), _tenantB, "TENANT-B-PROJECT"));
        _db.DocumentCategories.AddRange(
            new DocumentCategory { Id = _categoryA, TenantId = _tenantA, Name = "Tenant A", CreatedBy = "test" },
            new DocumentCategory { Id = _categoryB, TenantId = _tenantB, Name = "Tenant B", CreatedBy = "test" });
        _db.Documents.AddRange(
            NewDocument(_documentA, _tenantA, _projectA, _userA, "Project A document", _categoryA),
            NewDocument(_documentOtherOrg, _tenantA, _projectOtherOrg, _userA, "Other organization document", _categoryA),
            NewDocument(_documentB, _tenantB, null, _userB, "Tenant B document", _categoryB),
            NewDocument(_unlinkedDocumentA, _tenantA, null, _userA, "Unlinked owner document", _categoryA));
        _db.SaveChanges();

        _storage.Setup(provider => provider.SaveAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
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
        _storage.Verify(provider => provider.SaveAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Trusted_same_tenant_project_scope_allows_its_documents_and_unlinked_owner_records()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var projectDocument = await service.GetByIdAsync(_documentA);
        var unlinkedOwnerDocument = await service.GetByIdAsync(_unlinkedDocumentA);
        var paged = await service.GetPagedAsync(new DocumentListQuery());

        Assert.True(projectDocument.IsSuccess);
        Assert.Equal(_documentA, projectDocument.Value!.Document.Id);
        Assert.True(unlinkedOwnerDocument.IsSuccess);
        Assert.Equal(_unlinkedDocumentA, unlinkedOwnerDocument.Value!.Document.Id);
        Assert.Contains(paged.Items, item => item.Id == _documentA);
        Assert.Contains(paged.Items, item => item.Id == _unlinkedDocumentA);
        Assert.DoesNotContain(paged.Items, item => item.Id == _documentOtherOrg);
        Assert.DoesNotContain(paged.Items, item => item.Id == _documentB);
    }

    [Fact]
    public async Task Project_document_outside_organization_scope_is_denied_even_when_owned_by_current_user()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var outsideOrganization = await service.GetByIdAsync(_documentOtherOrg);
        var outsideOrganizationDownload = await service.GetDownloadAsync(_documentOtherOrg, null);

        Assert.True(outsideOrganization.IsFailure);
        Assert.True(outsideOrganizationDownload.IsFailure);
        _storage.Verify(provider => provider.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _storage.Verify(provider => provider.SaveAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Project_linked_upload_outside_management_scope_is_denied_before_storage_write()
    {
        var service = CreateService(ScopedContext(_tenantA, _userA, _projectA));

        var result = await service.CreateAsync(NewMutation(_projectOtherOrg, _categoryA), _userA);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        _storage.Verify(provider => provider.SaveAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(_db.ChangeTracker.Entries<Document>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task Tenant_categories_remain_available_without_project_management_context()
    {
        var result = await CreateService(null).GetCategoriesAsync();

        Assert.True(result.IsSuccess);
        var category = Assert.Single(result.Value!);
        Assert.Equal(_categoryA, category.Id);
        Assert.DoesNotContain(result.Value!, item => item.Id == _categoryB);
    }

    private DocumentService CreateService(ManagementContext? context)
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(context);
        return new DocumentService(_db, _storage.Object, _http, management.Object);
    }

    private ManagementContext ScopedContext(Guid tenantId, Guid userId, Guid projectId) => new(
        userId,
        tenantId,
        ManagementPerspective.ProjectManager,
        ManagementViewMode.Native,
        null,
        null,
        projectId,
        new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId }));

    private static DefaultHttpContext RequestContext(Guid tenantId, Guid userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "test"))
    };

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
        FileName = "document.txt",
        FileType = "TXT",
        FileSize = 8,
        ContentType = "text/plain",
        StoragePath = $"{id:N}/document.txt",
        Status = "Active",
        CreatedBy = "test"
    };

    private static DocumentMutationDto NewMutation(Guid projectId, Guid categoryId) => new(
        "Uploaded document",
        null,
        projectId,
        categoryId,
        File: new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("fixture")), 0, 7, "file", "document.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        });

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
