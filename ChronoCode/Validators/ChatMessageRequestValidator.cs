using ChronoCode.Models;
using FluentValidation;

namespace ChronoCode.Validators;

public class ChatMessageRequestValidator : AbstractValidator<ChatMessageRequest>
{
    public ChatMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .Must(message => !string.IsNullOrWhiteSpace(message))
            .WithMessage("Message is required.");
    }
}
