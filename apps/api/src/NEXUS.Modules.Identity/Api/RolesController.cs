using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IUserAdministrationService _userAdministrationService;

    public RolesController(IUserAdministrationService userAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
    }

    /// <summary>
    /// Lists all roles available in the system for assignment to users.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _userAdministrationService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }
}
