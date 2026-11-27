using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NEXUS.Api.Documents;

[ApiController]
[Authorize]
[Route("api/template-deletion-requests")]
public sealed class TemplateDeletionRequestController(ITemplateDeletionWorkflowService workflow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RequestDeletion(
        [FromBody] CreateTemplateDeletionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "VALIDATION_ERROR", error = "A request body is required." });
        return ToActionResult(await workflow.RequestAsync(request, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplateHistory(
        [FromQuery] Guid templateId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => ToActionResult(
            await workflow.GetTemplateHistoryAsync(templateId, page, pageSize, cancellationToken));

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(
        [FromQuery] string status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => ToActionResult(
            await workflow.GetInboxAsync(status, page, pageSize, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] DecideTemplateDeletionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "VALIDATION_ERROR", error = "A request body is required." });
        return ToActionResult(await workflow.ApproveAsync(id, request.Comments, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] DecideTemplateDeletionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { code = "VALIDATION_ERROR", error = "A request body is required." });
        return ToActionResult(await workflow.RejectAsync(id, request.Comments, cancellationToken));
    }

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var error = result.Error;
        var status = error?.StatusCode ?? StatusCodes.Status500InternalServerError;
        return StatusCode(status, new
        {
            code = error?.Code ?? "REQUEST_FAILED",
            error = error?.Message ?? "The request failed.",
            field = error?.FieldName,
            details = error?.Details
        });
    }
}
