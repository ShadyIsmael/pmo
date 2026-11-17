using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IAuthorizationService _authorization;

    public DepartmentController(IDepartmentService departmentService, IAuthorizationService authorization)
    {
        _departmentService = departmentService;
        _authorization = authorization;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanReadDepartmentLookupAsync()) return Forbid();
        var result = await _departmentService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken cancellationToken = default)
    {
        if (!await CanReadDepartmentLookupAsync()) return Forbid();
        var result = await _departmentService.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    private async Task<bool> CanReadDepartmentLookupAsync()
    {
        foreach (var policy in new[] { "Settings.View", "Team.View", "Resources.View" })
        {
            if ((await _authorization.AuthorizeAsync(User, policy)).Succeeded)
                return true;
        }
        return false;
    }
}
