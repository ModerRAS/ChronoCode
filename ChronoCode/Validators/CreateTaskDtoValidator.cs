using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using FluentValidation;

namespace ChronoCode.Validators;

public class CreateTaskDtoValidator : AbstractValidator<CreateTaskDto>
{
    public CreateTaskDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("CronExpression is required.")
            .Must(BeValidCronExpression).WithMessage("CronExpression must be a valid cron expression with 5 parts separated by spaces.");

        RuleFor(x => x.RepositoryUrl)
            .NotEmpty().WithMessage("RepositoryUrl is required.")
            .Must(BeValidUrl).WithMessage("RepositoryUrl must be a valid URL.");

        RuleFor(x => x.WorkflowDefinitionJson)
            .NotEmpty().WithMessage("WorkflowDefinitionJson is required.")
            .Must(BeValidWorkflow).WithMessage("WorkflowDefinitionJson must be a valid workflow definition.");

        RuleFor(x => x.RuntimeBackend)
            .Must(value => string.IsNullOrWhiteSpace(value) || value == WorkflowBackend.Pi)
            .WithMessage("RuntimeBackend must be null or 'pi'.");

        RuleFor(x => x.MaxConcurrentRuns)
            .GreaterThanOrEqualTo(1).WithMessage("MaxConcurrentRuns must be greater than or equal to 1.");

        RuleFor(x => x.MaxRuntimeSeconds)
            .GreaterThan(0).WithMessage("MaxRuntimeSeconds must be greater than 0.");

        RuleFor(x => x.MaxFileChanges)
            .GreaterThan(0).WithMessage("MaxFileChanges must be greater than 0.");

        RuleFor(x => x.NodeFailurePolicyJson)
            .Must(BeValidFailurePolicy).WithMessage("NodeFailurePolicyJson must be a valid failure policy JSON or '{}'.");
    }

    private static bool BeValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 5;
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeValidWorkflow(string? json)
    {
        try
        {
            return WorkflowDefinitionValidator.IsValid(json, out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidFailurePolicy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            return WorkflowDefinitionSerializer.DeserializeFailurePolicy(json) != null;
        }
        catch
        {
            return false;
        }
    }
}
