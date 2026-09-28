using FluentValidation;

namespace NEXUS.Modules.Meetings.Application;

internal static class MeetingEmailRules
{
    private static readonly IValidator<EmailValue> Validator = CreateValidator();

    public static bool IsUsable(string? email) =>
        email is not null && Validator.Validate(new EmailValue(email.Trim())).IsValid;

    public static string Normalize(string email) => email.Trim();

    private static IValidator<EmailValue> CreateValidator()
    {
        var validator = new InlineValidator<EmailValue>();
        validator.RuleFor(value => value.Value)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();
        return validator;
    }

    private sealed record EmailValue(string? Value);
}
