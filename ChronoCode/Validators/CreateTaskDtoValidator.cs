using ChronoCode.Models.DTOs;
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

        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.")
            .MaximumLength(5000).WithMessage("Prompt must not exceed 5000 characters.");
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
}