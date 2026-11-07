using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Api.Account;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task Get_is_bound_to_the_authenticated_user_tenant_and_session_claims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var service = new Mock<IAccountService>();
        service.Setup(item => item.GetAsync(userId, tenantId, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccountResponseDto>.Failure(Error.NotFound()));
        var controller = CreateController(service, userId, tenantId, sessionId);

        var response = await controller.Get(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
        service.Verify(item => item.GetAsync(userId, tenantId, sessionId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_fails_closed_when_the_trusted_current_session_claim_is_missing()
    {
        var service = new Mock<IAccountService>();
        var controller = CreateController(service, Guid.NewGuid(), Guid.NewGuid(), null);

        var response = await controller.Get(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Profile_update_passes_only_the_authenticated_owner_and_approved_fields()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var request = new UpdateAccountProfileRequest("Ada", "Lovelace");
        var service = new Mock<IAccountService>();
        service.Setup(item => item.UpdateProfileAsync(userId, tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccountProfileDto>.Success(new AccountProfileDto(userId, "Ada", "Lovelace", "Ada Lovelace", "ada@example.test", AuthenticationSource.Local, null, null)));
        var controller = CreateController(service, userId, tenantId, sessionId);

        var response = await controller.UpdateProfile(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        service.Verify(item => item.UpdateProfileAsync(userId, tenantId, request, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public void Public_account_dtos_do_not_expose_password_or_session_credentials()
    {
        var accountProperties = typeof(AccountResponseDto).GetProperties().Select(property => property.Name);
        var sessionProperties = typeof(AccountSessionDto).GetProperties().Select(property => property.Name);
        var profileProperties = typeof(AccountProfileDto).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(accountProperties, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sessionProperties, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(profileProperties, name => name.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
    }

    private static AccountController CreateController(Mock<IAccountService> service, Guid userId, Guid tenantId, Guid? sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenantId", tenantId.ToString())
        };
        if (sessionId.HasValue) claims.Add(new Claim("sid", sessionId.Value.ToString()));
        var controller = new AccountController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return controller;
    }
}
