using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.PluginManagement.Application;

namespace NEXUS.Modules.PluginManagement.Api;

[ApiController]
[Route("api/plugins")]
[Authorize(Policy = "Plugin.View")]
public class PluginController : ControllerBase
{
    private readonly IPluginRegistry _pluginRegistry;

    public PluginController(IPluginRegistry pluginRegistry)
    {
        _pluginRegistry = pluginRegistry;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pluginRegistry.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _pluginRegistry.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }
}
