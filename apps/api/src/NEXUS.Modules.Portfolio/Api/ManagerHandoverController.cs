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
    [Authorize(Policy = "Handover.Manage")]
    [HttpGet("programs/{id:guid}/eligible-managers")]
    public async Task<IActionResult> EligibleProgramManagers(Guid id, CancellationToken ct) => FromResult(await _service.GetEligibleProgramManagersAsync(id, ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpGet("portfolios/{id:guid}/eligible-managers")]
    public async Task<IActionResult> EligiblePortfolioManagers(Guid id, CancellationToken ct) => FromResult(await _service.GetEligiblePortfolioManagersAsync(id, ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/requests")]
    public async Task<IActionResult> ProgramRequests(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramRequestsAsync(id, ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/requests")]
    public async Task<IActionResult> PortfolioRequests(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioRequestsAsync(id, ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("programs/{id:guid}/requests")]
    public async Task<IActionResult> CreateProgram(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreateProgramAsync(id, dto, UserId(), ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("portfolios/{id:guid}/requests")]
    public async Task<IActionResult> CreatePortfolio(Guid id, CreateManagerHandoverRequestDto dto, CancellationToken ct) => FromResult(await _service.CreatePortfolioAsync(id, dto, UserId(), ct));
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
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("programs/requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelProgram(Guid id, CancellationToken ct) => FromResult(await _service.CancelProgramAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.Manage")]
    [HttpPost("portfolios/requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPortfolio(Guid id, CancellationToken ct) => FromResult(await _service.CancelPortfolioAsync(id, UserId(), ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/history")]
    public async Task<IActionResult> ProgramHistory(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramHistoryAsync(id, ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/history")]
    public async Task<IActionResult> PortfolioHistory(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioHistoryAsync(id, ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("programs/{id:guid}/snapshots")]
    public async Task<IActionResult> ProgramSnapshots(Guid id, CancellationToken ct) => FromResult(await _service.GetProgramSnapshotsAsync(id, ct));
    [Authorize(Policy = "Handover.View")]
    [HttpGet("portfolios/{id:guid}/snapshots")]
    public async Task<IActionResult> PortfolioSnapshots(Guid id, CancellationToken ct) => FromResult(await _service.GetPortfolioSnapshotsAsync(id, ct));

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private IActionResult FromResult<T>(NEXUS.SharedKernel.Result<T> result) => result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error?.StatusCode ?? 400, new { error = result.Error?.Message });
}
