using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class DocumentMandatoryPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SqliteTestAppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DocumentMandatoryPersistenceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new SqliteTestAppDbContext(options);
        _context.Database.EnsureCreated();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", _tenantId.ToString())],
                "document-mandatory-persistence-test"))
        };
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Mandatory_designation_survives_database_reload(bool isMandatory)
    {
        var document = new Document
        {
            TenantId = _tenantId,
            Name = $"Template mandatory persistence {isMandatory}",
            Status = "Active",
            IsTemplate = true,
            IsMandatory = isMandatory,
            CreatedBy = "document-mandatory-persistence-test"
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        var documentId = document.Id;

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Documents
            .AsNoTracking()
            .SingleAsync(item => item.Id == documentId);

        Assert.Equal(isMandatory, reloaded.IsMandatory);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Updated_mandatory_designation_survives_database_reload(bool initialValue, bool updatedValue)
    {
        var document = new Document
        {
            TenantId = _tenantId,
            Name = "Template mandatory update persistence",
            Status = "Active",
            IsTemplate = true,
            IsMandatory = initialValue,
            CreatedBy = "document-mandatory-persistence-test"
        };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var editable = await _context.Documents.SingleAsync(item => item.Id == document.Id);
        editable.IsMandatory = updatedValue;
        editable.UpdatedBy = "document-mandatory-persistence-test";
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Documents
            .AsNoTracking()
            .SingleAsync(item => item.Id == document.Id);

        Assert.Equal(updatedValue, reloaded.IsMandatory);
    }

    [Fact]
    public async Task Unspecified_mandatory_designation_defaults_to_optional_and_survives_reload()
    {
        var document = new Document
        {
            TenantId = _tenantId,
            Name = "Legacy optional template",
            Status = "Active",
            IsTemplate = true,
            CreatedBy = "document-mandatory-persistence-test"
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        var documentId = document.Id;

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Documents
            .AsNoTracking()
            .SingleAsync(item => item.Id == documentId);

        Assert.False(reloaded.IsMandatory);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
