using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Api;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Application.Validators;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ResourceInputValidationTests : TestBase
{
    [Fact]
    public void CreateValidator_RequiresHoursPerDayAndRejectsValuesOutsideInclusiveOneToTwentyFour()
    {
        var validator = new CreateResourceValidator();
        var required = validator.Validate(new CreateResourceDto { UserId = Guid.NewGuid() });
        Assert.Contains(required.Errors, error => error.PropertyName == nameof(CreateResourceDto.HoursPerDay));

        foreach (var hours in new[] { -1m, 0m, 0.99m, 24.01m, 25m })
        {
            var result = validator.Validate(new CreateResourceDto { UserId = Guid.NewGuid(), HoursPerDay = hours });
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateResourceDto.HoursPerDay));
        }

        foreach (var hours in new[] { 1m, 8m, 24m, 1.25m })
        {
            var result = validator.Validate(new CreateResourceDto { UserId = Guid.NewGuid(), HoursPerDay = hours });
            Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(CreateResourceDto.HoursPerDay));
        }
    }

    [Fact]
    public void UpdateValidator_EnforcesInclusiveOneToTwentyFourAndDoesNotAllowClearingHoursPerDay()
    {
        var validator = new UpdateResourceValidator();
        foreach (var hours in new[] { -1m, 0m, 0.99m, 24.01m, 25m })
        {
            var result = validator.Validate(new UpdateResourceDto { HoursPerDay = hours });
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateResourceDto.HoursPerDay));
        }

        foreach (var hours in new[] { 1m, 8m, 24m, 1.25m })
        {
            var result = validator.Validate(new UpdateResourceDto { HoursPerDay = hours });
            Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(UpdateResourceDto.HoursPerDay));
        }

        var clear = validator.Validate(new UpdateResourceDto { ClearHoursPerDay = true });
        Assert.Contains(clear.Errors, error => error.PropertyName == nameof(UpdateResourceDto.ClearHoursPerDay));
    }

    [Fact]
    public async Task CreateWithoutEmployeeIdOrEmploymentType_IsAccepted()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Set<User>().Add(new User
        {
            Id = userId,
            TenantId = TenantConstants.DemoTenantId,
            FirstName = "System",
            LastName = "User",
            Email = "system.user@example.com",
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = CreateResourceService(context);

        var result = await service.CreateAsync(new CreateResourceDto
        {
            UserId = userId,
            Name = "Duplicate client name",
            Email = "duplicate@example.com",
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("System User", result.Value!.Name);
        Assert.Equal("system.user@example.com", result.Value.Email);
        Assert.Null(result.Value!.EmployeeId);
        Assert.Null(result.Value.EmploymentType);
        Assert.Contains(context.Resources, resource => resource.Name == "System User" && resource.Email == "system.user@example.com");
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(8d)]
    [InlineData(24d)]
    [InlineData(1.25d)]
    public async Task CreateStaff_WithValidDailyHours_PersistsInclusiveBoundariesAndFractions(double dailyHours)
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Set<User>().Add(new User
        {
            Id = userId,
            TenantId = TenantConstants.DemoTenantId,
            FirstName = "Capacity",
            LastName = "User",
            Email = $"capacity-{userId:N}@example.com",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            UserId = userId,
            HoursPerDay = (decimal)dailyHours
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.BaseCalendarId);
        Assert.Equal((decimal)dailyHours, context.Resources.Single(resource => resource.Id == result.Value!.Id).HoursPerDay);
    }

    [Fact]
    public async Task CreateWithConfiguredTenantDefault_ValidatesDailyAndWeeklyCapacityWithoutPersistingOverride()
    {
        using var context = CreateContext();
        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Default calendar capacity resource",
            HoursPerDay = 8m,
            HoursPerWeek = 40m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.BaseCalendarId);
        Assert.Null(context.Resources.Single(resource => resource.Id == result.Value.Id).BaseCalendarId);
    }

    [Fact]
    public async Task CreateWithNoConfiguredDefault_AcceptsDailyHoursAndLeavesCalendarUnassigned()
    {
        using var context = CreateContext();
        context.WorkingCalendars.Single(calendar => calendar.Id == DefaultCalendarId).IsDefault = false;
        await context.SaveChangesAsync();
        var before = context.Resources.Count();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Daily capacity resource",
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.BaseCalendarId);
        Assert.Equal(before + 1, context.Resources.Count());
    }

    [Fact]
    public async Task CreateWithDailyAndWeeklyHoursAndNoDefaultReturnsCalendarErrorOnCalendarField()
    {
        using var context = CreateContext();
        context.WorkingCalendars.Single(calendar => calendar.Id == DefaultCalendarId).IsDefault = false;
        await context.SaveChangesAsync();
        var before = context.Resources.Count();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Capacity validation resource",
            HoursPerDay = 8m,
            HoursPerWeek = 40m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING", result.Error!.Code);
        Assert.Equal(nameof(CreateResourceDto.BaseCalendarId), result.Error.FieldName);
        Assert.Equal(before, context.Resources.Count());
    }

    [Fact]
    public async Task CreateWithInvalidTenantDefaultReturnsCalendarConfigurationError()
    {
        using var context = CreateContext();
        var calendar = context.WorkingCalendars.Single(item => item.Id == DefaultCalendarId);
        calendar.WorkingDaysPerWeek = 0;
        calendar.HoursPerDay = 0;
        foreach (var rule in calendar.DayRules) rule.IsDeleted = true;
        await context.SaveChangesAsync();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Invalid default resource",
            HoursPerDay = 8m,
            HoursPerWeek = 40m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_WORKING_CALENDAR_DEFAULT_INVALID", result.Error!.Code);
        Assert.Equal(nameof(CreateResourceDto.BaseCalendarId), result.Error.FieldName);
    }

    [Fact]
    public async Task CreateWithUnavailableCalendarIdReturnsTenantScopedCalendarError()
    {
        using var context = CreateContext();
        var foreignCalendarId = Guid.NewGuid();
        context.WorkingCalendars.Add(new WorkingCalendar
        {
            Id = foreignCalendarId,
            TenantId = Guid.NewGuid(),
            Name = "Another tenant calendar",
            WorkingDaysPerWeek = 5,
            HoursPerDay = 8,
            IsDefault = true
        });
        await context.SaveChangesAsync();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Calendar access resource",
            BaseCalendarId = foreignCalendarId,
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_WORKING_CALENDAR_UNAVAILABLE", result.Error!.Code);
        Assert.Equal(nameof(CreateResourceDto.BaseCalendarId), result.Error.FieldName);
    }

    [Fact]
    public async Task CreateWithCapacityMismatchReturnsHoursPerWeekError()
    {
        using var context = CreateContext();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Capacity mismatch resource",
            HoursPerDay = 8m,
            HoursPerWeek = 30m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_CAPACITY_DAILY_WEEKLY_MISMATCH", result.Error!.Code);
        Assert.Equal(nameof(CreateResourceDto.HoursPerWeek), result.Error.FieldName);
    }

    [Fact]
    public async Task UpdateLegacyResourceWithoutCalendarResolvesTenantDefaultAndKeepsInheritanceAndStaffLink()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Set<User>().Add(new User
        {
            Id = userId,
            TenantId = TenantConstants.DemoTenantId,
            FirstName = "Calendar",
            LastName = "User",
            Email = "calendar.user@example.com",
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = CreateResourceService(context);
        var created = await service.CreateAsync(new CreateResourceDto
        {
            UserId = userId,
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        var persisted = context.Resources.Single(resource => resource.Id == created.Value!.Id);
        persisted.BaseCalendarId = null;
        persisted.HoursPerWeek = 40m;
        await context.SaveChangesAsync();

        var updated = await service.UpdateAsync(persisted.Id, new UpdateResourceDto
        {
            JobTitle = "Updated role"
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Null(updated.Value!.BaseCalendarId);
        Assert.Equal(userId, context.Resources.Single(resource => resource.Id == persisted.Id).UserId);
    }

    [Fact]
    public async Task UpdatePreservesAnExplicitResourceCalendarWhenTheRequestOmitsIt()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);
        var created = await service.CreateAsync(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Explicit calendar resource",
            BaseCalendarId = NonDefaultCalendarId,
            HoursPerDay = 6m,
            HoursPerWeek = 24m
        }, TenantConstants.DemoTenantId.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        var updated = await service.UpdateAsync(created.Value!.Id, new UpdateResourceDto
        {
            JobTitle = "Updated role"
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(NonDefaultCalendarId, updated.Value!.BaseCalendarId);
    }

    [Fact]
    public async Task UpdateResource_PersistsInclusiveBoundariesAndFractionalHours_AndRejectsInvalidChanges()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);

        foreach (var dailyHours in new[] { 1m, 24m, 1.25m })
        {
            var result = await service.UpdateAsync(ResourceAliceId, new UpdateResourceDto
            {
                HoursPerDay = dailyHours
            }, TenantConstants.DemoTenantId.ToString());

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(dailyHours, context.Resources.Single(resource => resource.Id == ResourceAliceId).HoursPerDay);
        }

        var rejectedRange = await service.UpdateAsync(ResourceAliceId, new UpdateResourceDto
        {
            HoursPerDay = 24.01m
        }, TenantConstants.DemoTenantId.ToString());
        Assert.False(rejectedRange.IsSuccess);
        Assert.Equal(1.25m, context.Resources.Single(resource => resource.Id == ResourceAliceId).HoursPerDay);

        var rejectedClear = await service.UpdateAsync(ResourceAliceId, new UpdateResourceDto
        {
            ClearHoursPerDay = true
        }, TenantConstants.DemoTenantId.ToString());
        Assert.False(rejectedClear.IsSuccess);
        Assert.Equal(1.25m, context.Resources.Single(resource => resource.Id == ResourceAliceId).HoursPerDay);
    }

    [Fact]
    public async Task CreateStaffWithoutLinkedUser_IsRejectedBeforePersistence()
    {
        using var context = CreateContext();
        var countBefore = context.Resources.Count();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Contains(nameof(CreateResourceDto.UserId), result.Error!.Message);
        Assert.Equal(countBefore, context.Resources.Count());
    }

    [Fact]
    public async Task CreateStaff_RejectsInactiveOrCrossTenantSystemUsers()
    {
        using var context = CreateContext();
        var foreignActiveUserId = Guid.NewGuid();
        var localInactiveUserId = Guid.NewGuid();
        context.Set<User>().AddRange(
            new User
            {
                Id = foreignActiveUserId,
                TenantId = Guid.NewGuid(),
                FirstName = "Foreign",
                LastName = "User",
                Email = "foreign@example.com",
                IsActive = true
            },
            new User
            {
                Id = localInactiveUserId,
                TenantId = TenantConstants.DemoTenantId,
                FirstName = "Inactive",
                LastName = "User",
                Email = "inactive@example.com",
                IsActive = false
            });
        await context.SaveChangesAsync();
        var service = CreateResourceService(context);
        var countBefore = context.Resources.Count();

        foreach (var userId in new[] { foreignActiveUserId, localInactiveUserId })
        {
            var result = await service.CreateAsync(new CreateResourceDto
            {
                UserId = userId,
                HoursPerDay = 8m
            }, TenantConstants.DemoTenantId.ToString());

            Assert.False(result.IsSuccess);
            Assert.Contains("active in the Resource tenant", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(countBefore, context.Resources.Count());
    }

    [Fact]
    public async Task CreateStaff_RejectsUserAlreadyLinkedToAnotherResource()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Set<User>().Add(new User
        {
            Id = userId,
            TenantId = TenantConstants.DemoTenantId,
            FirstName = "Linked",
            LastName = "User",
            Email = "linked@example.com",
            IsActive = true
        });
        context.Resources.Add(new Resource
        {
            TenantId = TenantConstants.DemoTenantId,
            UserId = userId,
            Name = "Existing Staff Resource",
            Email = "linked@example.com",
            Type = "Staff",
            Status = "Active",
            IsActive = true,
            HoursPerDay = 8m
        });
        await context.SaveChangesAsync();
        var countBefore = context.Resources.Count();

        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            UserId = userId,
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Contains("already linked", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(countBefore, context.Resources.Count());
    }

    [Fact]
    public async Task UpdateStaff_RefreshesIdentityFromUserAndDoesNotChangeUserProfile()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            TenantId = TenantConstants.DemoTenantId,
            FirstName = "Initial",
            LastName = "Profile",
            Email = "initial.profile@example.com",
            IsActive = true
        };
        context.Set<User>().Add(user);
        await context.SaveChangesAsync();
        var service = CreateResourceService(context);

        var created = await service.CreateAsync(new CreateResourceDto
        {
            UserId = userId,
            Name = "Client supplied name",
            Email = "client.supplied@example.com",
            Phone = "111-1111",
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        user.FirstName = "Updated";
        user.LastName = "Profile";
        user.Email = "updated.profile@example.com";
        await context.SaveChangesAsync();

        var updated = await service.UpdateAsync(created.Value!.Id, new UpdateResourceDto
        {
            UserId = userId,
            Name = "Stale Resource Name",
            Email = "stale.resource@example.com",
            Phone = "222-2222",
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal("Updated Profile", updated.Value!.Name);
        Assert.Equal("updated.profile@example.com", updated.Value.Email);
        Assert.Equal("222-2222", updated.Value.Phone);
        Assert.Equal("Updated", user.FirstName);
        Assert.Equal("Profile", user.LastName);
        Assert.Equal("updated.profile@example.com", user.Email);
    }

    [Fact]
    public async Task CreateWithOversizedEmploymentType_IsRejectedBeforePersistence()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);
        var countBefore = context.Resources.Count();

        var result = await service.CreateAsync(new CreateResourceDto
        {
            Name = "Oversized employment type",
            EmploymentType = new string('x', ResourceFieldLengths.EmploymentType + 1)
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
        Assert.Contains(nameof(CreateResourceDto.EmploymentType), result.Error.Message);
        Assert.Equal(countBefore, context.Resources.Count());
    }

    [Fact]
    public async Task CreateWithUndefinedResourceType_IsRejectedBeforePersistence()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);
        var countBefore = context.Resources.Count();

        var result = await service.CreateAsync(new CreateResourceDto
        {
            Name = "Invalid type",
            Type = (ResourceType)99
        }, TenantConstants.DemoTenantId.ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
        Assert.Equal(nameof(CreateResourceDto.Type), result.Error.FieldName);
        Assert.Equal(countBefore, context.Resources.Count());
    }

    [Fact]
    public async Task ResourceReferenceDataIsDerivedFromBackendEnumsAndLocalized()
    {
        using var context = CreateContext();
        var data = await CreateResourceService(context).GetReferenceDataAsync();

        Assert.Equal(new[] { "Staff", "Vendor", "Outsourced" }, data.ResourceTypes.Select(type => type.Code));
        Assert.Equal("Vendor", data.ResourceTypes[1].NameEn);
        Assert.Equal("مورد", data.ResourceTypes[1].NameAr);
        Assert.True(data.ResourceTypes[1].RequiresVendor);
        Assert.Equal("Outsourced", data.ResourceTypes[2].NameEn);
        Assert.Equal("متعاقد خارجي", data.ResourceTypes[2].NameAr);
        Assert.False(data.ResourceTypes[2].RequiresVendor);
        Assert.Equal(new[] { "Junior", "Mid", "Senior", "Lead", "Principal" }, data.SeniorityLevels.Select(level => level.Code));
        Assert.Contains(CompensationType.Hourly, data.CompensationTypes);
    }

    [Fact]
    public async Task VendorEnumRequestKeepsHistoricalStoredCodeAndReturnsEnumName()
    {
        using var context = CreateContext();
        var result = await CreateResourceService(context).CreateAsync(new CreateResourceDto
        {
            Name = "Enum vendor resource",
            Type = ResourceType.Vendor,
            VendorId = VendorAcmeId,
            HoursPerDay = 8m
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Vendor", result.Value!.Type);
        Assert.Equal(ResourceTypeConstants.VendorResource,
            context.Resources.Single(resource => resource.Id == result.Value.Id).Type);
    }

    [Fact]
    public void UnknownHistoricalResourceTypeIsReturnedWithoutReclassification()
    {
        var resource = new Resource { Name = "Historical contractor", Type = "Contractor" };

        Assert.Equal("Contractor", resource.ToDto().Type);
    }

    [Fact]
    public async Task UpdateWithoutHrFields_PreservesExistingValues()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);
        var resource = context.Resources.Single(item => item.Id == ResourceAliceId);
        resource.EmployeeId = "HR-00421";
        resource.EmploymentType = "Legacy value";
        await context.SaveChangesAsync();

        var result = await service.UpdateAsync(resource.Id, new UpdateResourceDto
        {
            Name = "Alice Johnson Updated"
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        var persisted = context.Resources.Single(item => item.Id == resource.Id);
        Assert.Equal("HR-00421", persisted.EmployeeId);
        Assert.Equal("Legacy value", persisted.EmploymentType);
    }

    [Fact]
    public async Task ControllerCreateReturnsFieldErrorsBeforeCallingService()
    {
        var service = new Mock<IResourceService>();
        var controller = CreateController(service.Object);

        var response = await controller.Create(new CreateResourceDto
        {
            Name = "Invalid resource type",
            Type = (ResourceType)99
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains(nameof(CreateResourceDto.Type), problem.Errors.Keys);
        service.Verify(mock => mock.CreateAsync(It.IsAny<CreateResourceDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ControllerUpdateReturnsFieldErrorsBeforeCallingService()
    {
        var service = new Mock<IResourceService>();
        var controller = CreateController(service.Object);

        var response = await controller.Update(ResourceAliceId, new UpdateResourceDto
        {
            EmployeeId = new string('x', ResourceFieldLengths.EmployeeId + 1)
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains(nameof(UpdateResourceDto.EmployeeId), problem.Errors.Keys);
        service.Verify(mock => mock.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateResourceDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ControllerMapsCalendarServiceErrorsToCodedProblemDetails()
    {
        var service = new Mock<IResourceService>();
        service.Setup(mock => mock.CreateAsync(It.IsAny<CreateResourceDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResourceDto>.Failure(Error.BusinessValidation(
                "RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING",
                "No valid tenant Standard working calendar is configured.",
                nameof(CreateResourceDto.BaseCalendarId))));
        var controller = CreateController(service.Object);

        var response = await controller.Create(new CreateResourceDto
        {
            Type = ResourceType.Outsourced,
            Name = "Calendar error contract",
            HoursPerDay = 8m,
            HoursPerWeek = 40m
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal("RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING", problem.Extensions["code"]);
        Assert.Contains(nameof(CreateResourceDto.BaseCalendarId), problem.Errors.Keys);
        Assert.DoesNotContain(nameof(CreateResourceDto.HoursPerDay), problem.Errors.Keys);
    }

    private static ResourceService CreateResourceService(TestDbContext context)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        return new ResourceService(
            context,
            audit.Object,
            Mock.Of<IUserReferenceLookup>(),
            CreateTenantHttpContextAccessor(),
            new CreateResourceValidator(),
            new UpdateResourceValidator());
    }

    private static IHttpContextAccessor CreateTenantHttpContextAccessor()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim("tenantId", TenantConstants.DemoTenantId.ToString()) },
            "ResourceManagementTests"));
        return new HttpContextAccessor { HttpContext = context };
    }

    private static ResourceController CreateController(IResourceService service)
    {
        var mvcServices = new ServiceCollection()
            .AddLogging()
            .AddControllers()
            .Services
            .BuildServiceProvider();
        return new ResourceController(service, new CreateResourceValidator(), new UpdateResourceValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = mvcServices }
            }
        };
    }
}
