using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using FluentValidation;

namespace ChronoCode.Validators;

public class UpdateTaskDtoValidator : AbstractValidator<UpdateTaskDto>
{
    public UpdateTaskDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
            .When(x => x.Name != null);

        RuleFor(x => x.CronExpression)
            .Must(BeValidCronExpression).WithMessage("CronExpression must be a valid cron expression with 5 parts separated by spaces.")
            .When(x => !string.IsNullOrEmpty(x.CronExpression));

        RuleFor(x => x.RepositoryUrl)
            .Must(BeValidUrl).WithMessage("RepositoryUrl must be a valid URL.")
            .When(x => !string.IsNullOrEmpty(x.RepositoryUrl));

        RuleFor(x => x.WorkflowDefinitionJson)
            .Must(BeValidWorkflow).WithMessage("WorkflowDefinitionJson must be a valid workflow definition.")
            .When(x => !string.IsNullOrEmpty(x.WorkflowDefinitionJson));

        RuleFor(x => x.RuntimeBackend)
            .Must(value => string.IsNullOrWhiteSpace(value) || value == WorkflowBackend.Pi)
            .WithMessage("RuntimeBackend must be null or 'pi'.")
            .When(x => x.RuntimeBackend != null);

        RuleFor(x => x.MaxConcurrentRuns)
            .GreaterThanOrEqualTo(1).WithMessage("MaxConcurrentRuns must be greater than or equal to 1.")
            .When(x => x.MaxConcurrentRuns.HasValue);

        RuleFor(x => x.MaxRuntimeSeconds)
            .GreaterThan(0).WithMessage("MaxRuntimeSeconds must be greater than 0.")
            .When(x => x.MaxRuntimeSeconds.HasValue);

        RuleFor(x => x.MaxFileChanges)
            .GreaterThan(0).WithMessage("MaxFileChanges must be greater than 0.")
            .When(x => x.MaxFileChanges.HasValue);

        RuleFor(x => x.NodeFailurePolicyJson)
            .Must(BeValidFailurePolicy).WithMessage("NodeFailurePolicyJson must be a valid failure policy JSON or '{}'.")
            .When(x => !string.IsNullOrEmpty(x.NodeFailurePolicyJson));
    }

    private static bool BeValidCronExpression(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return true;

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 5;
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

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
