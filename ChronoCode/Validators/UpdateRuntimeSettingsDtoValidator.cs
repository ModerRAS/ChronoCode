using ChronoCode.Models.DTOs;
using FluentValidation;

namespace ChronoCode.Validators;

public class UpdateRuntimeSettingsDtoValidator : AbstractValidator<UpdateRuntimeSettingsDto>
{
    public UpdateRuntimeSettingsDtoValidator()
    {
        RuleFor(x => x.AgentRuntime.Backend)
            .Must(backend => backend is "pi" or "opencode")
            .WithMessage("AgentRuntime.Backend must be 'pi' or 'opencode'.");

        RuleFor(x => x.Opencode.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Opencode.Port must be between 1 and 65535.");
    }
}
