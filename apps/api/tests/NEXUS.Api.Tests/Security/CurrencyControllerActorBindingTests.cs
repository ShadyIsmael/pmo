using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class CurrencyControllerActorBindingTests
{
    [Fact]
    public async Task Currency_mutations_use_the_authenticated_subject_when_name_identifier_is_absent()
    {
        var actorId = Guid.NewGuid();
        var request = new SaveCurrencyRequest("USD", "US Dollar", "$", true);
        var currency = new CurrencyDto(Guid.NewGuid(), "USD", "US Dollar", "$", 1m, true);
        var service = new Mock<ICurrencyService>(MockBehavior.Strict);
        service
            .Setup(item => item.CreateAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CurrencyDto>.Success(currency));
        var controller = new CurrencyController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", actorId.ToString("D"))], "authenticated-test"))
                }
            }
        };

        Assert.IsType<CreatedAtActionResult>(await controller.Create(request, CancellationToken.None));
        service.Verify(
            item => item.CreateAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Currency_mutations_forbid_missing_actor_claim_instead_of_using_system_identity()
    {
        var service = new Mock<ICurrencyService>(MockBehavior.Strict);
        var controller = new CurrencyController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("tenantId", Guid.NewGuid().ToString("D"))], "authenticated-test"))
                }
            }
        };
        var request = new SaveCurrencyRequest("USD", "US Dollar", "$", true);

        Assert.IsType<ForbidResult>(await controller.Create(request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Update(Guid.NewGuid(), request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Delete(Guid.NewGuid(), CancellationToken.None));

        service.VerifyNoOtherCalls();
    }
}
