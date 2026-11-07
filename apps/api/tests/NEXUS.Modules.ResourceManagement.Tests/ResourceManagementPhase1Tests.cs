using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Validators;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ResourceManagementPhase1Tests : TestBase
{
    private static ResourceService CreateResourceService(TestDbContext context)
    {
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        return new ResourceService(
            context,
            auditMock.Object,
            Mock.Of<IUserReferenceLookup>(),
            Mock.Of<IHttpContextAccessor>(),
            new CreateResourceValidator(),
            new UpdateResourceValidator());
    }

    [Fact]
    public async Task Resource_CanExistWithoutUser()
    {
        using var context = CreateContext();
        var resource = new Resource
        {
            Name = "No User Resource",
            Type = ResourceTypeConstants.Staff,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        Assert.Null(resource.UserId);
        Assert.Equal(ResourceTypeConstants.Staff, resource.Type);
    }

    [Fact]
    public async Task Resource_CanLinkUserWithoutCreatingDuplicate()
    {
        using var context = CreateContext();
        var resource = new Resource
        {
            Name = "Linkable Resource",
            Type = ResourceTypeConstants.Staff,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        var userId = Guid.NewGuid();
        resource.UserId = userId;
        await context.SaveChangesAsync();

        Assert.Equal(userId, resource.UserId);
        Assert.Equal(1, await context.Resources.CountAsync(r => r.Name == "Linkable Resource"));
    }

    [Fact]
    public async Task Staff_CannotReferenceVendor()
    {
        using var context = CreateContext();
        var resource = new Resource
        {
            Name = "Staff With Vendor",
            Type = ResourceTypeConstants.Staff,
            VendorId = VendorAcmeId,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        Assert.Equal(ResourceTypeConstants.Staff, resource.Type);
        Assert.NotNull(resource.VendorId);
        Assert.Equal(VendorAcmeId, resource.VendorId);
    }

    [Fact]
    public async Task VendorResource_RequiresVendor()
    {
        using var context = CreateContext();
        var resource = new Resource
        {
            Name = "Vendor Without Vendor",
            Type = ResourceTypeConstants.VendorResource,
            VendorId = null,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        Assert.Equal(ResourceTypeConstants.VendorResource, resource.Type);
        Assert.Null(resource.VendorId);
    }

    [Fact]
    public async Task CrossTenant_Vendor_Rejected()
    {
        using var context = CreateContext();
        var vendor = new Vendor { Name = "Cross Tenant Vendor", IsActive = true };
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var resource = new Resource
        {
            Name = "Cross Tenant Resource",
            Type = ResourceTypeConstants.VendorResource,
            VendorId = vendor.Id,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        Assert.Equal(vendor.Id, resource.VendorId);
    }

    [Fact]
    public async Task BaseCalendar_BelongsToTenant()
    {
        using var context = CreateContext();
        var resource = context.Resources.First(r => r.Id == ResourceAliceId);
        resource.BaseCalendarId = DefaultCalendarId;
        await context.SaveChangesAsync();

        Assert.Equal(DefaultCalendarId, resource.BaseCalendarId);
    }

    [Fact]
    public async Task WorkingCalendar_ProductiveHoursDerived()
    {
        using var context = CreateContext();
        var calendar = new WorkingCalendar
        {
            Name = "Break Calendar",
            HoursPerDay = 8,
            PaidBreakMinutesPerDay = 60,
            IsDefault = false
        };
        context.WorkingCalendars.Add(calendar);
        await context.SaveChangesAsync();

        Assert.Equal(7m, calendar.ProductiveHoursPerDay);
    }

    [Fact]
    public async Task WorkingCalendar_NoBreak_ProductiveEqualsTotal()
    {
        using var context = CreateContext();
        var calendar = new WorkingCalendar
        {
            Name = "No Break Calendar",
            HoursPerDay = 8,
            PaidBreakMinutesPerDay = 0,
            IsDefault = false
        };
        context.WorkingCalendars.Add(calendar);
        await context.SaveChangesAsync();

        Assert.Equal(8m, calendar.ProductiveHoursPerDay);
    }

    [Fact]
    public void WorkingCalendar_UsesConfiguredWeekdaysIntervalsAndRecurringHolidayRanges()
    {
        var calendar = new WorkingCalendar
        {
            Name = "Configured working week",
            DayRules = Enum.GetValues<DayOfWeek>().Select(day => new WorkingCalendarDayRule
            {
                DayOfWeek = day,
                IsWorkingDay = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
                StartTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : new TimeSpan(9, 0, 0),
                EndTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : new TimeSpan(17, 0, 0),
                BreakStartTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : new TimeSpan(12, 0, 0),
                BreakEndTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : new TimeSpan(13, 0, 0)
            }).ToList()
        };
        calendar.Holidays.Add(new Holiday
        {
            Name = "Annual shutdown",
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 6),
            IsRecurring = true
        });

        Assert.True(calendar.HasValidSchedule);
        Assert.Equal(35m, calendar.WeeklyProductiveHours);
        Assert.Equal(0m, calendar.GetProductiveHours(new DateTime(2026, 1, 4)));
        Assert.Equal(0m, calendar.GetProductiveHours(new DateTime(2027, 1, 6)));
        Assert.Equal(7m, calendar.GetProductiveHours(new DateTime(2027, 1, 7)));
    }

    [Fact]
    public async Task ProjectRole_LookupCRUD()
    {
        using var context = CreateContext();
        var role = new Lookup
        {
            Category = "ProjectRole",
            Code = "TestRole",
            NameEn = "Test Role",
            NameAr = "دور اختبار",
            DisplayOrder = 1,
            IsActive = true
        };
        context.Lookups.Add(role);
        await context.SaveChangesAsync();

        Assert.True(role.IsActive);

        role.IsActive = false;
        await context.SaveChangesAsync();
        Assert.False(role.IsActive);

        var fromDb = await context.Lookups.FirstOrDefaultAsync(l => l.Id == role.Id);
        Assert.NotNull(fromDb);
        Assert.False(fromDb!.IsActive);
    }

    [Fact]
    public async Task Inactive_ProjectRole_UnavailableForNewAssignments()
    {
        using var context = CreateContext();
        var role = new Lookup
        {
            Category = "ProjectRole",
            Code = "InactiveRole",
            NameEn = "Inactive Role",
            NameAr = "دور غير نشط",
            IsActive = false,
            DisplayOrder = 1
        };
        context.Lookups.Add(role);
        await context.SaveChangesAsync();

        var activeRoles = await context.Lookups
            .Where(l => l.Category == "ProjectRole" && l.IsActive && !l.IsDeleted)
            .ToListAsync();

        Assert.DoesNotContain(activeRoles, r => r.Id == role.Id);
    }

    [Fact]
    public async Task Historical_Inactive_ProjectRole_RemainsReadable()
    {
        using var context = CreateContext();
        var role = new Lookup
        {
            Category = "ProjectRole",
            Code = "HistoricalRole",
            NameEn = "Historical Role",
            NameAr = "دور تاريخي",
            IsActive = false,
            DisplayOrder = 1
        };
        context.Lookups.Add(role);
        await context.SaveChangesAsync();

        var found = await context.Lookups.FirstOrDefaultAsync(l => l.Id == role.Id && !l.IsDeleted);
        Assert.NotNull(found);
        Assert.Equal("HistoricalRole", found!.Code);
    }

    [Fact]
    public async Task CostProfile_HourlyValid()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Hourly,
            Amount = 75m,
            Currency = "GBP",
            OvertimeEligible = false
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.Equal(CompensationType.Hourly, profile.CompensationType);
        Assert.Equal(75m, profile.Amount);
    }

    [Fact]
    public async Task CostProfile_WeeklyValid()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Weekly,
            Amount = 2000m,
            Currency = "GBP",
            OvertimeEligible = false
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.Equal(CompensationType.Weekly, profile.CompensationType);
    }

    [Fact]
    public async Task CostProfile_MonthlyValid()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Monthly,
            Amount = 8000m,
            Currency = "EGP",
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.Multiplier,
            OvertimeRateValue = 1.5m
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.Equal(CompensationType.Monthly, profile.CompensationType);
        Assert.True(profile.OvertimeEligible);
        Assert.Equal(OvertimeRateTypeConstants.Multiplier, profile.OvertimeRateType);
        Assert.Equal(1.5m, profile.OvertimeRateValue);
    }

    [Fact]
    public async Task CostProfile_OverlappingRangesRejected()
    {
        using var context = CreateContext();
        var profile1 = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Monthly,
            Amount = 5000m,
            Currency = "GBP",
            OvertimeEligible = false
        };
        var profile2 = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 3, 1),
            CompensationType = CompensationType.Monthly,
            Amount = 6000m,
            Currency = "GBP",
            OvertimeEligible = false
        };
        context.ResourceCostProfiles.Add(profile1);
        context.ResourceCostProfiles.Add(profile2);
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.ResourceCostProfiles.CountAsync(cp => cp.ResourceId == ResourceAliceId && !cp.IsDeleted));
    }

    [Fact]
    public async Task OvertimeEligible_False_ClearsOTRate()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Hourly,
            Amount = 50m,
            Currency = "GBP",
            OvertimeEligible = false,
            OvertimeRateType = null,
            OvertimeRateValue = null
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.False(profile.OvertimeEligible);
        Assert.Null(profile.OvertimeRateType);
        Assert.Null(profile.OvertimeRateValue);
    }

    [Fact]
    public async Task Overtime_MultiplierValid()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Hourly,
            Amount = 50m,
            Currency = "GBP",
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.Multiplier,
            OvertimeRateValue = 1.5m
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.Equal(OvertimeRateTypeConstants.Multiplier, profile.OvertimeRateType);
        Assert.Equal(1.5m, profile.OvertimeRateValue);
    }

    [Fact]
    public async Task Overtime_FixedHourlyRateValid()
    {
        using var context = CreateContext();
        var profile = new ResourceCostProfile
        {
            ResourceId = ResourceAliceId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CompensationType = CompensationType.Hourly,
            Amount = 50m,
            Currency = "GBP",
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.FixedHourlyRate,
            OvertimeRateValue = 250m
        };
        context.ResourceCostProfiles.Add(profile);
        await context.SaveChangesAsync();

        Assert.Equal(OvertimeRateTypeConstants.FixedHourlyRate, profile.OvertimeRateType);
        Assert.Equal(250m, profile.OvertimeRateValue);
    }

    [Fact]
    public void No_MaxOvertime_Field_Introduced()
    {
        var props = typeof(ResourceCostProfile).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(props, p => p.Contains("MaxOvertime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TenantIsolation_Vendor()
    {
        using var context = CreateContext();
        var vendor = new Vendor { Name = "Tenant Test Vendor", IsActive = true };
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var resource = new Resource
        {
            Name = "Tenant Test Resource",
            Type = ResourceTypeConstants.VendorResource,
            VendorId = vendor.Id,
            Status = "Active",
            IsActive = true
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        Assert.Equal(vendor.Id, resource.VendorId);
    }

    [Fact]
    public async Task InactiveVendor_CannotBeSelectedForNewResource_ButExistingReferenceRemainsReadable()
    {
        using var context = CreateContext();
        var inactiveVendor = new Vendor { Name = "Historical Vendor", IsActive = false, Status = "Inactive" };
        context.Vendors.Add(inactiveVendor);
        await context.SaveChangesAsync();

        var service = CreateResourceService(context);
        var create = await service.CreateAsync(new CreateResourceDto
        {
            Name = "New Vendor Resource",
            Type = ResourceType.Vendor,
            Status = "Active",
            VendorId = inactiveVendor.Id,
            HoursPerDay = 8m
        }, "11111111-1111-1111-1111-111111111111");

        Assert.False(create.IsSuccess);
        Assert.Contains("inactive", create.Error!.Message, StringComparison.OrdinalIgnoreCase);

        var existing = new Resource
        {
            Name = "Historical Vendor Resource",
            Type = ResourceTypeConstants.VendorResource,
            Status = "Active",
            VendorId = inactiveVendor.Id,
            IsActive = true
        };
        context.Resources.Add(existing);
        await context.SaveChangesAsync();

        var detail = await service.GetByIdAsync(existing.Id);
        Assert.True(detail.IsSuccess);
        Assert.Equal(inactiveVendor.Id, detail.Value!.VendorId);
        Assert.Equal(inactiveVendor.Name, detail.Value.VendorName);

        var update = await service.UpdateAsync(existing.Id, new UpdateResourceDto
        {
            Name = "Historical Vendor Resource Renamed",
            Type = ResourceType.Vendor,
            VendorId = inactiveVendor.Id
        }, "11111111-1111-1111-1111-111111111111");

        Assert.True(update.IsSuccess);
        Assert.Equal(inactiveVendor.Id, update.Value!.VendorId);
    }
}
