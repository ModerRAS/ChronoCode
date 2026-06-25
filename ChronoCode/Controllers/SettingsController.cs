using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly IValidator<UpdateRuntimeSettingsDto> _validator;

    public SettingsController(ISettingsService settingsService, IValidator<UpdateRuntimeSettingsDto> validator)
    {
        _settingsService = settingsService;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<RuntimeSettingsDto>> Get()
    {
        return Ok(await _settingsService.GetAsync());
    }

    [HttpPut]
    public async Task<ActionResult<RuntimeSettingsDto>> Update([FromBody] UpdateRuntimeSettingsDto request)
    {
        var validation = await _validator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
                }
            });
        }

        return Ok(await _settingsService.UpdateAsync(request, HttpContext.RequestAborted));
    }
}
