using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Approvals.Application;

namespace NEXUS.Modules.Approvals.Api;

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.GetByIdAsync(id, callerId, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.GetPagedAsync(page, pageSize, callerId, status, userId, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitApprovalDto dto, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.SubmitAsync(id, callerId, dto.Comments, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRejectApprovalDto dto, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.ApproveAsync(id, callerId, dto.Comments, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApproveRejectApprovalDto dto, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.RejectAsync(id, callerId, dto.Comments ?? string.Empty, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/delegate")]
    public async Task<IActionResult> Delegate(Guid id, [FromBody] DelegateApprovalDto dto, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        if (dto.ToUserId == Guid.Empty) return BadRequest("ToUserId is required.");
        var result = await _approvalService.DelegateAsync(id, callerId, dto.ToUserId, dto.Reason, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var callerId = CallerId();
        if (callerId == Guid.Empty) return Unauthorized();
        var result = await _approvalService.CancelAsync(id, callerId, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    private Guid CallerId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>Maps a service error to the matching HTTP status without leaking record existence.</summary>
    private IActionResult Failure(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch
    {
        403 => Forbid(),
        404 => NotFound(),
        _ => BadRequest(error?.Message)
    };
}

public record SubmitApprovalDto(string? Comments = null);
public record ApproveRejectApprovalDto(string? Comments = null);
public record DelegateApprovalDto(Guid ToUserId, string? Reason = null);
