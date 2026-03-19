using ChronoCode.Models.DTOs;
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

        RuleFor(x => x.Prompt)
            .MaximumLength(5000).WithMessage("Prompt must not exceed 5000 characters.")
            .When(x => x.Prompt != null);
    }

    private static bool BeValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return true;

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 5;
    }

    private static bool BeValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}