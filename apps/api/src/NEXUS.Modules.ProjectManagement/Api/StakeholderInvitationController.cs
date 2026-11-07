using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/stakeholders/{stakeholderId:guid}/invitation")]
[Authorize(Policy = "Stakeholders.Manage")]
public sealed class StakeholderInvitationController : ControllerBase
{
    private readonly IStakeholderInvitationService _service;

    public StakeholderInvitationController(IStakeholderInvitationService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Send(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(projectId, stakeholderId, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(projectId, stakeholderId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken)
    {
        var result = await _service.RevokeAsync(projectId, stakeholderId, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

    private static IActionResult Map(NEXUS.SharedKernel.Error error) => error.StatusCode switch
    {
        404 => new NotFoundObjectResult(new { error = error.Message }),
        409 => new ConflictObjectResult(new { error = error.Message }),
        403 => new ObjectResult(new { error = error.Message }) { StatusCode = 403 },
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}

[ApiController]
[Route("api/stakeholder-invitations")]
public sealed class StakeholderInvitationAcceptanceController : ControllerBase
{
    private readonly IStakeholderInvitationService _service;

    public StakeholderInvitationAcceptanceController(IStakeholderInvitationService service) => _service = service;

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] StakeholderInvitationTokenDto request, CancellationToken cancellationToken)
    {
        var result = await _service.ValidateAsync(request.Token, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptStakeholderInvitationDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.AcceptAsync(dto.Token, dto, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("decline")]
    public async Task<IActionResult> Decline([FromBody] StakeholderInvitationTokenDto request, CancellationToken cancellationToken)
    {
        var result = await _service.DeclineAsync(request.Token, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("withdraw-consent")]
    public async Task<IActionResult> WithdrawConsent([FromBody] WithdrawStakeholderConsentDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.WithdrawConsentAsync(dto.Token, dto, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    private static IActionResult Map(NEXUS.SharedKernel.Error error) => error.StatusCode switch
    {
        404 => new NotFoundObjectResult(new { error = error.Message }),
        409 => new ConflictObjectResult(new { error = error.Message }),
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}
