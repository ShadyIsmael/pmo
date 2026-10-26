using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ResourceCostProfileServiceTests : TestBase
{
    private const string TestUser = "11111111-1111-1111-1111-111111111111";

    private static Mock<IAuditService> CreateAuditService()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return audit;
    }

    private static CreateResourceCostProfileDto NewProfile(Guid resourceId, DateTime start, DateTime? end = null) => new()
    {
        ResourceId = resourceId,
        EffectiveFrom = start,
        EffectiveTo = end,
        CompensationType = CompensationType.Hourly,
        Amount = 100m,
        Currency = "gbp"
    };

    private static async Task SetDemoTenantAsync(TestDbContext context, Guid resourceId)
    {
        var resource = await context.Resources.SingleAsync(item => item.Id == resourceId);
        resource.TenantId = TenantConstants.DemoTenantId;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_StoresEffectiveEndAndCopiesTenantFromResource()
    {
        using var context = CreateContext();
        await SetDemoTenantAsync(context, ResourceAliceId);
        var service = new ResourceCostProfileService(context, CreateAuditService().Object);
        var dto = NewProfile(ResourceAliceId, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        var result = await service.CreateAsync(dto, TestUser);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Currency.Should().Be("GBP");
        result.Value.EffectiveTo.Should().Be(new DateTime(2026, 10, 31));
        var persisted = await context.ResourceCostProfiles.SingleAsync(profile => profile.Id == result.Value.Id);
        persisted.TenantId.Should().Be(TenantConstants.DemoTenantId);
        persisted.EffectiveTo.Should().Be(new DateTime(2026, 10, 31));
    }

    [Fact]
    public async Task Create_RejectsEndDateBeforeStartDate()
    {
        using var context = CreateContext();
        await SetDemoTenantAsync(context, ResourceAliceId);
        var service = new ResourceCostProfileService(context, CreateAuditService().Object);

        var result = await service.CreateAsync(
            NewProfile(ResourceAliceId, new DateTime(2026, 10, 31), new DateTime(2026, 10, 1)),
            TestUser);

        result.IsFailure.Should().BeTrue();
        (await context.ResourceCostProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_RejectsCurrencyNotConfiguredForTenant()
    {
        using var context = CreateContext();
        await SetDemoTenantAsync(context, ResourceAliceId);
        var service = new ResourceCostProfileService(context, CreateAuditService().Object);
        var dto = NewProfile(ResourceAliceId, new DateTime(2026, 10, 1));
        dto.Currency = "ZZZ";

        var result = await service.CreateAsync(dto, TestUser);

        result.IsFailure.Should().BeTrue();
        result.Error!.FieldName.Should().Be(nameof(CreateResourceCostProfileDto.Currency));
        (await context.ResourceCostProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_RejectsOverlappingEffectiveRanges()
    {
        using var context = CreateContext();
        await SetDemoTenantAsync(context, ResourceAliceId);
        var service = new ResourceCostProfileService(context, CreateAuditService().Object);
        (await service.CreateAsync(NewProfile(ResourceAliceId, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)), TestUser))
            .IsSuccess.Should().BeTrue();

        var overlapping = await service.CreateAsync(
            NewProfile(ResourceAliceId, new DateTime(2026, 10, 20), new DateTime(2026, 11, 10)),
            TestUser);

        overlapping.IsFailure.Should().BeTrue();
        (await context.ResourceCostProfiles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAndDeleteRequireTheRouteResourceToOwnTheProfile()
    {
        using var context = CreateContext();
        await SetDemoTenantAsync(context, ResourceAliceId);
        var service = new ResourceCostProfileService(context, CreateAuditService().Object);
        var created = await service.CreateAsync(NewProfile(ResourceAliceId, new DateTime(2026, 10, 1)), TestUser);
        created.IsSuccess.Should().BeTrue();

        var mismatchedRead = await service.GetByIdAsync(ResourceBobId, created.Value!.Id);
        var mismatchedDelete = await service.DeleteAsync(ResourceBobId, created.Value.Id, TestUser);

        mismatchedRead.IsFailure.Should().BeTrue();
        mismatchedDelete.IsFailure.Should().BeTrue();
        (await context.ResourceCostProfiles.SingleAsync()).IsDeleted.Should().BeFalse();
    }
}
