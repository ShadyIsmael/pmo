using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.SharedKernel.Helpers;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ProjectRoleServiceTests : TestBase
{
    private const string TestUser = "11111111-1111-1111-1111-111111111111";

    private static ProjectRoleService CreateService(TestDbContext context)
        => new(context, Mock.Of<IAuditService>());

    private static CreateLookupDto NewRole(string? code, string nameEn = "Delivery Manager")
        => new()
        {
            Code = code ?? string.Empty,
            NameEn = nameEn,
            NameAr = "مدير تسليم",
            Description = null,
            DisplayOrder = 30,
            IsActive = true
        };

    private static Lookup SeedGeneratedCode(TestDbContext context, string code, int displayOrder = 40)
    {
        var lookup = new Lookup
        {
            Id = Guid.NewGuid(),
            TenantId = TenantConstants.DemoTenantId,
            Category = ProjectRoleService.Category,
            Code = code,
            NameEn = code,
            NameAr = code,
            DisplayOrder = displayOrder,
            IsActive = true
        };
        context.Lookups.Add(lookup);
        context.SaveChanges();
        return lookup;
    }

    [Fact]
    public async Task GetNextCode_StartsAtFirstCodeWhenNoGeneratedCodesExist()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        // The harness seeds only hand-entered codes (BackendDeveloper, SolutionArchitect),
        // which must not affect the sequence.
        var code = await service.GetNextCodeAsync();

        code.Should().Be("PROJROLE-000001");
    }

    [Fact]
    public async Task GetNextCode_ContinuesAfterHighestExistingGeneratedCode()
    {
        using var context = CreateContext();
        SeedGeneratedCode(context, "PROJROLE-000001");
        SeedGeneratedCode(context, "PROJROLE-000002");
        SeedGeneratedCode(context, "PROJROLE-000007");
        var service = CreateService(context);

        var code = await service.GetNextCodeAsync();

        code.Should().Be("PROJROLE-000008");
    }

    [Fact]
    public async Task GetNextCode_IgnoresOtherCategoriesAndNonNumericCodes()
    {
        using var context = CreateContext();
        context.Lookups.Add(new Lookup
        {
            Id = Guid.NewGuid(),
            TenantId = TenantConstants.DemoTenantId,
            Category = "VendorCategory",
            Code = "PROJROLE-000099",
            NameEn = "Other category",
            NameAr = "Other category",
            DisplayOrder = 5,
            IsActive = true
        });
        SeedGeneratedCode(context, "PROJROLE-ABC");
        context.SaveChanges();
        var service = CreateService(context);

        var code = await service.GetNextCodeAsync();

        code.Should().Be("PROJROLE-000001");
    }

    [Fact]
    public async Task GetNextCode_SkipsSoftDeletedGeneratedCodes()
    {
        using var context = CreateContext();
        var deleted = SeedGeneratedCode(context, "PROJROLE-000001");
        deleted.IsDeleted = true;
        context.SaveChanges();
        var service = CreateService(context);

        var code = await service.GetNextCodeAsync();

        code.Should().Be("PROJROLE-000001");
    }

    [Fact]
    public async Task Create_StampsCurrentTenant_SoTheNewRoleIsImmediatelyReadable()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole(null), TestUser);

        created.IsSuccess.Should().BeTrue();
        // Lookups are tenant-scoped with a global query filter, so a row written without the
        // caller's tenant would never show up in the list or in the next-code sequence.
        created.Value.Should().NotBeNull();

        var reloaded = await context.Lookups.SingleAsync(l => l.Id == created.Value!.Id);
        reloaded.TenantId.Should().Be(TenantConstants.DemoTenantId);
        reloaded.Code.Should().Be("PROJROLE-000001");
    }

    [Fact]
    public async Task Create_ThenNextCode_AdvancesPastTheCreatedRole()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateAsync(NewRole(null, "First"), TestUser);
        var next = await service.GetNextCodeAsync();

        next.Should().Be("PROJROLE-000002");
    }

    [Fact]
    public async Task Create_WithBlankCode_AssignsNextGenericCode()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole(code: null), TestUser);

        created.IsSuccess.Should().BeTrue();
        created.Value!.Code.Should().Be("PROJROLE-000001");
    }

    [Fact]
    public async Task Create_WithBlankCode_AssignsIncrementedCodeOnEachCreate()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var first = await service.CreateAsync(NewRole(null, "First"), TestUser);
        var second = await service.CreateAsync(NewRole(null, "Second"), TestUser);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value!.Code.Should().Be("PROJROLE-000001");
        second.Value!.Code.Should().Be("PROJROLE-000002");
    }

    [Fact]
    public async Task Create_WithBlankCode_DoesNotCollideWithHandEnteredCode()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole(null, "Backend Developer 2"), TestUser);

        created.IsSuccess.Should().BeTrue();
        created.Value!.Code.Should().Be("PROJROLE-000001");
        created.Value.Code.Should().NotBe("BackendDeveloper");
    }

    [Fact]
    public async Task Create_WithExplicitCode_KeepsTheSuppliedCode()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole("BusinessAnalyst"), TestUser);

        created.IsSuccess.Should().BeTrue();
        created.Value!.Code.Should().Be("BusinessAnalyst");
    }

    [Fact]
    public async Task Create_WithExplicitCode_TrimsAndPreservesHandEnteredStyle()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole("  QA-Lead  "), TestUser);

        created.IsSuccess.Should().BeTrue();
        created.Value!.Code.Should().Be("QA-Lead");
    }

    [Fact]
    public async Task Create_WithDuplicateCode_ReturnsConflict()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRole("BackendDeveloper"), TestUser);

        created.IsSuccess.Should().BeFalse();
        created.Error!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Create_WithBlankCode_WhenNextCodeAlreadyUsed_StillPersistsSuccessfully()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.CreateAsync(NewRole(null, "First"), TestUser);

        var second = await service.CreateAsync(NewRole(null, "Second"), TestUser);

        second.IsSuccess.Should().BeTrue();
        second.Value!.Code.Should().NotBe("PROJROLE-000001");
    }
}
