using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Identity.Api;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IResourceReferenceLookup? _resourceLookup;
    private readonly IProjectAudienceLookup? _projectAudience;
    private readonly IManagementContextAccessor? _managementContext;

    public UsersController(
        IUserRepository userRepository,
        IUserAdministrationService userAdministrationService,
        IUserEffectivePermissionService effectivePermissions,
        IResourceReferenceLookup? resourceLookup = null,
        IProjectAudienceLookup? projectAudience = null,
        IManagementContextAccessor? managementContext = null)
    {
        _userRepository = userRepository;
        _userAdministrationService = userAdministrationService;
        _effectivePermissions = effectivePermissions;
        _resourceLookup = resourceLookup;
        _projectAudience = projectAudience;
        _managementContext = managementContext;
    }

    /// <summary>
    /// Returns minimal active-user options for Project workflows without requiring user-administration access.
    /// </summary>
    [Authorize(Policy = "WorkflowLookup.Project.Users")]
    [HttpGet("workflow-lookups/projects")]
    public async Task<IActionResult> GetProjectWorkflowUsers(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        return Ok(await GetWorkflowUserDtosAsync(null, tenantId, cancellationToken));
    }

    /// <summary>Returns active Management-role candidates for initial Project Manager assignments.</summary>
    [Authorize(Policy = "WorkflowLookup.Project.Users")]
    [HttpGet("workflow-lookups/projects/managers")]
    public async Task<IActionResult> GetProjectManagerCandidates(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        var users = await _userRepository.GetByRoleCategoryInTenantAsync("Management", tenantId, cancellationToken);
        return Ok(await FilterEligibleUsersAsync(
            users,
            tenantId,
            new[] { "Handover.Approve" },
            cancellationToken));
    }

    /// <summary>Returns minimal active-user options for Meeting workflows.</summary>
    [Authorize(Policy = "WorkflowLookup.Meeting.Users")]
    [HttpGet("workflow-lookups/meetings")]
    public Task<IActionResult> GetMeetingWorkflowUsers(CancellationToken cancellationToken = default) =>
        GetWorkflowUsersResponseAsync(null, cancellationToken);

    /// <summary>Returns minimal active-user options for Work Item workflows.</summary>
    [Authorize(Policy = "WorkflowLookup.WorkItem.Users")]
    [HttpGet("workflow-lookups/work-items")]
    public Task<IActionResult> GetWorkItemWorkflowUsers(CancellationToken cancellationToken = default) =>
        GetWorkflowUsersResponseAsync(null, cancellationToken);

    /// <summary>Returns active, project-associated users for Risk and Risk Action owner selectors.</summary>
    [Authorize(Policy = "WorkflowLookup.Risk.Users")]
    [HttpGet("workflow-lookups/risks")]
    public async Task<IActionResult> GetRiskWorkflowUsers(
        [FromQuery] Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();
        if (!projectId.HasValue || projectId.Value == Guid.Empty)
            return BadRequest(new { error = "A project is required to look up Risk owners." });
        if (_managementContext?.Current?.Scope.ProjectIds.Contains(projectId.Value) != true)
            return NotFound();
        if (_projectAudience is null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable);

        var users = await _projectAudience.SearchProjectUsersAsync(tenantId, projectId.Value, null, 50, cancellationToken);
        return Ok(users.Select(user => new WorkflowUserLookupDto(user.UserId, user.FullName)).ToList());
    }

    /// <summary>Returns minimal active-user options for resource workflows.</summary>
    [Authorize(Policy = "WorkflowLookup.Resource.Users")]
    [HttpGet("workflow-lookups/resources")]
    public async Task<IActionResult> GetResourceWorkflowUsers(
        [FromQuery] Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        if (_resourceLookup is null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable);

        ResourceReference? currentResource = null;
        if (resourceId.HasValue)
        {
            if (resourceId.Value == Guid.Empty)
                return BadRequest();

            currentResource = await _resourceLookup.FindInTenantAsync(resourceId.Value, tenantId, cancellationToken);
            if (currentResource is null)
                return NotFound();
        }

        var users = await _userRepository.GetActiveInTenantAsync(tenantId, cancellationToken);
        var resourcesByUserId = await _resourceLookup.GetDetailsByUserIdsInTenantAsync(
            users.Select(user => user.Id), tenantId, cancellationToken);
        var eligibleUsers = users
            .Where(user => !resourcesByUserId.TryGetValue(user.Id, out var linkedResource)
                || (currentResource is not null && linkedResource.Id == currentResource.Id))
            .Select(user => new ResourceWorkflowUserLookupDto(user.Id, user.FullName, user.Email))
            .ToList();

        return Ok(eligibleUsers);
    }

    /// <summary>Returns minimal active-user options for authorized manager assignments.</summary>
    [Authorize(Policy = "WorkflowLookup.Handover.ManagerCandidates")]
    [HttpGet("workflow-lookups/manager-assignments")]
    public async Task<IActionResult> GetManagerAssignmentUsers(
        [FromQuery] string? role = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        var users = await _userRepository.GetByRoleCategoryInTenantAsync("Management", tenantId, cancellationToken);
        return Ok(await FilterEligibleUsersAsync(
            users,
            tenantId,
            new[] { "Handover.Approve" },
            cancellationToken));
    }

    /// <summary>Returns active Portfolio Manager candidates for Portfolio workflows.</summary>
    [Authorize(Policy = "WorkflowLookup.Portfolio.Managers")]
    [HttpGet("workflow-lookups/portfolios/managers")]
    public async Task<IActionResult> GetPortfolioManagerCandidates(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        var users = await _userRepository.GetByRoleCategoryInTenantAsync("Management", tenantId, cancellationToken);
        return Ok(await FilterEligibleUsersAsync(
            users,
            tenantId,
            new[] { "Handover.Approve", "Portfolio.View" },
            cancellationToken));
    }

    /// <summary>Returns active Program Manager candidates for Program workflows.</summary>
    [Authorize(Policy = "WorkflowLookup.Program.Managers")]
    [HttpGet("workflow-lookups/programs/managers")]
    public async Task<IActionResult> GetProgramManagerCandidates(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        var users = await _userRepository.GetByRoleCategoryInTenantAsync("Management", tenantId, cancellationToken);
        return Ok(await FilterEligibleUsersAsync(
            users,
            tenantId,
            new[] { "Program.View", "Handover.Approve" },
            cancellationToken));
    }

    private async Task<IActionResult> GetWorkflowUsersResponseAsync(
        string? role,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        return Ok(await GetWorkflowUserDtosAsync(role, tenantId, cancellationToken));
    }

    private async Task<IReadOnlyList<WorkflowUserLookupDto>> GetWorkflowUserDtosAsync(
        string? role,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var users = await GetWorkflowUsersAsync(role, tenantId, cancellationToken);
        return users.Select(user => new WorkflowUserLookupDto(user.Id, user.FullName)).ToList();
    }

    private async Task<IReadOnlyList<User>> GetWorkflowUsersAsync(
        string? role,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(role)
            ? await _userRepository.GetActiveInTenantAsync(tenantId, cancellationToken)
            : await _userRepository.GetByRoleNameInTenantAsync(role, tenantId, cancellationToken);

    private async Task<IReadOnlyList<WorkflowUserLookupDto>> FilterEligibleUsersAsync(
        IReadOnlyList<User> users,
        Guid tenantId,
        IReadOnlyCollection<string> requiredPermissions,
        CancellationToken cancellationToken)
    {
        var candidates = new List<WorkflowUserLookupDto>(users.Count);
        foreach (var user in users)
        {
            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                user.Id,
                tenantId,
                cancellationToken);
            if (requiredPermissions.All(effectivePermissions.Contains))
                candidates.Add(new WorkflowUserLookupDto(user.Id, user.FullName));
        }

        return candidates;
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId) && tenantId != Guid.Empty;
    }

    /// <summary>
    /// Lightweight user lookup used by pickers/selectors across the UI.
    /// Optionally filter by role name.
    /// </summary>
    [Authorize(Policy = "Users.View")]
    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup([FromQuery] string? role = null, CancellationToken cancellationToken = default)
    {
        var users = string.IsNullOrWhiteSpace(role)
            ? await _userRepository.GetActiveAsync(cancellationToken)
            : await _userRepository.GetByRoleNameAsync(role, cancellationToken);
        var resourcesByUserId = _resourceLookup is null
            ? new Dictionary<Guid, ResourceReference>()
            : await _resourceLookup.GetDetailsByUserIdsAsync(users.Select(user => user.Id), cancellationToken);

        var dtos = users.Select(u => new UserLookupDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Paginated user administration list with assigned roles.
    /// Requires the Users.View permission.
    /// </summary>
    [Authorize(Policy = "Users.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _userAdministrationService.GetPagedAsync(page, pageSize, search, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(result.Error?.StatusCode ?? StatusCodes.Status403Forbidden, new { error = result.Error?.Message });
    }

    /// <summary>
    /// Creates a user and assigns roles (including the Project Manager role).
    /// Account creation requires Users.ManageAccounts; optional role assignment and inactive status also require their matching permissions.
    /// </summary>
    [Authorize(Policy = "Users.ManageAccounts")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetMutationActor(out var actorId, out _))
            return Forbid();

        var result = await _userAdministrationService.CreateAsync(dto, actorId, cancellationToken);
        if (result.IsFailure)
            return ToUserAdministrationError(result.Error);
        return CreatedAtAction(nameof(GetPaged), new { page = 1, pageSize = 20 }, result.Value);
    }

    [Authorize(Policy = "Users.ResetPassword")]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetMutationActor(out var actorId, out _))
            return Forbid();

        var result = await _userAdministrationService.ResetPasswordAsync(id, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message, code = result.Error.Code }),
                404 => NotFound(new { error = result.Error.Message, code = result.Error.Code }),
                409 => Conflict(new { error = result.Error.Message, code = result.Error.Code }),
                _ => BadRequest(new { error = result.Error?.Message, code = result.Error?.Code })
            };
        return Ok(result.Value);
    }

    /// <summary>
    /// Updates account details, role assignments, or account status when the caller holds the matching action permission.
    /// Each field group is independently authorized by the application service.
    /// </summary>
    [Authorize(Policy = "Users.View")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetMutationActor(out var actorId, out _))
            return Forbid();

        var result = await _userAdministrationService.UpdateAsync(id, dto, actorId, cancellationToken);
        if (result.IsFailure)
            return ToUserAdministrationError(result.Error);
        return Ok(result.Value);
    }

    [Authorize(Policy = "UserPermissions.Manage")]
    [HttpGet("{id:guid}/permissions")]
    public async Task<IActionResult> GetPermissions(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var tenantId))
            return Forbid();

        var result = await _userAdministrationService.GetUserPermissionsAsync(id, tenantId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : BadRequest(new { error = result.Error?.Message });

        return Ok(result.Value);
    }

    [Authorize(Policy = "UserPermissions.Manage")]
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateUserPermissionsDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetMutationActor(out var actorId, out var tenantId))
            return Forbid();

        var result = await _userAdministrationService.UpdateUserPermissionsAsync(id, tenantId, dto, actorId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                403 => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error.Message }),
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };

        return Ok(result.Value);
    }

    private bool TryGetActor(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        var actorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actorClaim, out actorId) || actorId == Guid.Empty
            || !TryGetTenantId(out tenantId))
        {
            return false;
        }

        var context = _managementContext?.Current;
        return context is null || (context.UserId == actorId && context.TenantId == tenantId);
    }

    private bool TryGetMutationActor(out string actorId, out Guid tenantId)
    {
        actorId = string.Empty;
        if (!TryGetActor(out var actorUserId, out tenantId))
            return false;

        var context = _managementContext?.Current;
        if (context is null || context.EffectiveReadOnly)
            return false;

        actorId = actorUserId.ToString("D");
        return true;
    }

    private IActionResult ToUserAdministrationError(Error? error)
    {
        if (error is null)
            return Problem(statusCode: StatusCodes.Status500InternalServerError);

        var response = new Dictionary<string, object?>
        {
            ["error"] = error.Message,
            ["code"] = error.Code,
            ["fieldName"] = error.FieldName,
            ["details"] = error.Details
        };
        if (!string.IsNullOrWhiteSpace(error.FieldName))
            response["errors"] = new Dictionary<string, string[]>
            {
                [error.FieldName] = [error.Message]
            };

        return StatusCode(error.StatusCode, response);
    }

}
