using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly ISetupService _setupService;

    public SetupController(ISetupService setupService)
    {
        _setupService = setupService;
    }

    [HttpGet("status")]
    public ActionResult<SetupStatusDto> GetStatus()
    {
        return Ok(_setupService.GetStatus());
    }

    [HttpPost("initialize")]
    public async Task<ActionResult<SetupStatusDto>> Initialize([FromBody] InitializeSetupDto request)
    {
        try
        {
            var status = await _setupService.InitializeAsync(request, HttpContext.RequestAborted);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = new { code = "ALREADY_INITIALIZED", message = ex.Message } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
    }
}
