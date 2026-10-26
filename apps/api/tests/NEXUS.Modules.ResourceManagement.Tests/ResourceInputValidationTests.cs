using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
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
    public async Task CreateWithoutEmployeeIdOrEmploymentType_IsAccepted()
    {
        using var context = CreateContext();
        var service = CreateResourceService(context);

        var result = await service.CreateAsync(new CreateResourceDto
        {
            Name = "Resource without HR identifiers"
        }, TenantConstants.DemoTenantId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.EmployeeId);
        Assert.Null(result.Value.EmploymentType);
        Assert.Contains(context.Resources, resource => resource.Name == "Resource without HR identifiers");
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

        Assert.Equal(new[] { "Staff", "Vendor" }, data.ResourceTypes.Select(type => type.Code));
        Assert.Equal("Vendor", data.ResourceTypes[1].NameEn);
        Assert.Equal("مورد", data.ResourceTypes[1].NameAr);
        Assert.True(data.ResourceTypes[1].RequiresVendor);
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
            VendorId = VendorAcmeId
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

    private static ResourceService CreateResourceService(TestDbContext context)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(item => item.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        return new ResourceService(
            context,
            audit.Object,
            Mock.Of<IUserReferenceLookup>(),
            Mock.Of<IHttpContextAccessor>(),
            new CreateResourceValidator(),
            new UpdateResourceValidator());
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
