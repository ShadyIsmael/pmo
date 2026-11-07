using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Portfolio.Application;

namespace NEXUS.Modules.Portfolio.Api;

[ApiController]
[Route("api/manager-handovers")]
[Authorize]
public sealed class ManagerHandoverController : ControllerBase
{
    private readonly IManagerHandoverService _service;
    public ManagerHandoverController(IManagerHandoverService service) => _service = service;

    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("pending-for-me")]
    public async Task<IActionResult> Pending(CancellationToken ct) => FromResult(await _service.GetPendingForMeAsync(UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("programs/requests/{id:guid}/review")]
    public async Task<IActionResult> ReviewProgram(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramApprovalReviewAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("portfolios/requests/{id:guid}/review")]
    public async Task<IActionResult> ReviewPortfolio(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioApprovalReviewAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("programs/{id:guid}/pending-for-me")]
    public async Task<IActionResult> PendingProgramForMe(Guid id, CancellationToken ct) => FromResult(await _service.GetPendingProgramForMeAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpGet("portfolios/{id:guid}/pending-for-me")]
    public async Task<IActionResult> PendingPortfolioForMe(Guid id, CancellationToken ct) => FromResult(await _service.GetPendingPortfolioForMeAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/eligible-managers")]
    public async Task<IActionResult> EligibleProgramManagers(Guid id, CancellationToken ct) => FromResult(await _service.GetEligibleProgramManagersAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/eligible-managers")]
    public async Task<IActionResult> EligiblePortfolioManagers(Guid id, CancellationToken ct) => FromResult(await _service.GetEligiblePortfolioManagersAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/capabilities")]
    public async Task<IActionResult> ProgramCapabilities(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramCapabilitiesAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/capabilities")]
    public async Task<IActionResult> PortfolioCapabilities(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioCapabilitiesAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/requests")]
    public async Task<IActionResult> ProgramRequests(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramRequestsAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/requests")]
    public async Task<IActionResult> PortfolioRequests(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioRequestsAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("programs/{id:guid}/requests")]
    public async Task<IActionResult> CreateProgram(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateProgramAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("portfolios/{id:guid}/requests")]
    public async Task<IActionResult> CreatePortfolio(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreatePortfolioAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("programs/{id:guid}/admin-requests")]
    public async Task<IActionResult> AdminCreateProgram(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateAdminProgramAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("portfolios/{id:guid}/admin-requests")]
    public async Task<IActionResult> AdminCreatePortfolio(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateAdminPortfolioAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("programs/{id:guid}/initial-assignment-requests")]
    public async Task<IActionResult> InitialProgramAssignment(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateInitialProgramAssignmentAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.AdminInitiate")]
    [HttpPost("portfolios/{id:guid}/initial-assignment-requests")]
    public async Task<IActionResult> InitialPortfolioAssignment(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateInitialPortfolioAssignmentAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("programs/requests/{id:guid}/accept")]
    public async Task<IActionResult> AcceptProgram(Guid id, CancellationToken ct) => FromResult(await _service.AcceptProgramAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("portfolios/requests/{id:guid}/accept")]
    public async Task<IActionResult> AcceptPortfolio(Guid id, CancellationToken ct) => FromResult(await _service.AcceptPortfolioAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("programs/requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectProgram(Guid id, RejectManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.RejectProgramAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.Approve")]
    [HttpPost("portfolios/requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectPortfolio(Guid id, RejectManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.RejectPortfolioAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpPost("programs/requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelProgram(Guid id, CancellationToken ct) => FromResult(await _service.CancelProgramAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpPost("portfolios/requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPortfolio(Guid id, CancellationToken ct) => FromResult(await _service.CancelPortfolioAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/history")]
    public async Task<IActionResult> ProgramHistory(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramHistoryAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/history")]
    public async Task<IActionResult> PortfolioHistory(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioHistoryAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/snapshots")]
    public async Task<IActionResult> ProgramSnapshots(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramSnapshotsAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/snapshots")]
    public async Task<IActionResult> PortfolioSnapshots(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioSnapshotsAsync(id, UserId(), ct));

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private IActionResult FromResult<T>(NEXUS.SharedKernel.Result<T> result) => result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error?.StatusCode ?? 400, new { error = result.Error?.Message });
}
