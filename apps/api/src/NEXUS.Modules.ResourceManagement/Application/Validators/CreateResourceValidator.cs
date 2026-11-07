using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.Validators;

public class CreateResourceValidator : AbstractValidator<CreateResourceDto>
{
    public CreateResourceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ResourceFieldLengths.Name)
            .When(x => x.Type != ResourceType.Staff);
        RuleFor(x => x.UserId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("Select an active system user for Staff resources.")
            .When(x => x.Type == ResourceType.Staff);
        RuleFor(x => x.EmployeeId).MaximumLength(ResourceFieldLengths.EmployeeId);
        RuleFor(x => x.JobTitle).MaximumLength(ResourceFieldLengths.JobTitle);
        RuleFor(x => x.Email).MaximumLength(ResourceFieldLengths.Email).EmailAddress()
            .When(x => x.Type != ResourceType.Staff && !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(ResourceFieldLengths.Phone);
        RuleFor(x => x.AvatarUrl).MaximumLength(ResourceFieldLengths.AvatarUrl);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(ResourceFieldLengths.Status);
        RuleFor(x => x.EmploymentType).MaximumLength(ResourceFieldLengths.EmploymentType);
        RuleFor(x => x.Seniority).MaximumLength(ResourceFieldLengths.Seniority);
        RuleFor(x => x.Timezone).MaximumLength(ResourceFieldLengths.Timezone);
        RuleFor(x => x.Notes).MaximumLength(ResourceFieldLengths.Notes);
        RuleFor(x => x.UtilizationTarget).InclusiveBetween(0, 100).When(x => x.UtilizationTarget.HasValue);
        RuleFor(x => x.HoursPerDay)
            .NotNull().WithMessage("Hours per day is required.")
            .InclusiveBetween(1m, 24m).WithMessage("Hours per day must be between 1 and 24 hours.");
        RuleFor(x => x.HoursPerWeek).InclusiveBetween(0.01m, 168m).When(x => x.HoursPerWeek.HasValue);
        RuleForEach(x => x.Skills).ChildRules(s =>
        {
            s.RuleFor(x => x.SkillId).NotEmpty();
            s.RuleFor(x => x.Proficiency).NotEmpty().MaximumLength(50);
            s.RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0);
        });
    }
}

public class UpdateResourceValidator : AbstractValidator<UpdateResourceDto>
{
    public UpdateResourceValidator()
    {
        RuleFor(x => x.Name).MaximumLength(ResourceFieldLengths.Name).When(x => x.Name != null);
        RuleFor(x => x.EmployeeId).MaximumLength(ResourceFieldLengths.EmployeeId);
        RuleFor(x => x.JobTitle).MaximumLength(ResourceFieldLengths.JobTitle);
        RuleFor(x => x.Email).MaximumLength(ResourceFieldLengths.Email).EmailAddress()
            .When(x => x.Type != ResourceType.Staff && !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(ResourceFieldLengths.Phone);
        RuleFor(x => x.AvatarUrl).MaximumLength(ResourceFieldLengths.AvatarUrl);
        RuleFor(x => x.Type)
            .Must(type => !type.HasValue || Enum.IsDefined(type.Value))
            .WithMessage("Type must be a supported resource type.");
        RuleFor(x => x.Status).MaximumLength(ResourceFieldLengths.Status).When(x => x.Status != null);
        RuleFor(x => x.EmploymentType).MaximumLength(ResourceFieldLengths.EmploymentType);
        RuleFor(x => x.Seniority).MaximumLength(ResourceFieldLengths.Seniority);
        RuleFor(x => x.Timezone).MaximumLength(ResourceFieldLengths.Timezone);
        RuleFor(x => x.Notes).MaximumLength(ResourceFieldLengths.Notes);
        RuleFor(x => x.UtilizationTarget).InclusiveBetween(0, 100).When(x => x.UtilizationTarget.HasValue);
        RuleFor(x => x.HoursPerDay).InclusiveBetween(1m, 24m).When(x => x.HoursPerDay.HasValue)
            .WithMessage("Hours per day must be between 1 and 24 hours.");
        RuleFor(x => x.HoursPerWeek).InclusiveBetween(0.01m, 168m).When(x => x.HoursPerWeek.HasValue);
        RuleFor(x => x.ClearHoursPerDay).Equal(false)
            .WithMessage("Hours per day is required and cannot be cleared.");
        RuleFor(x => x.ClearHoursPerWeek).Must((dto, clear) => !clear || !dto.HoursPerWeek.HasValue)
            .WithMessage("HoursPerWeek cannot be set and cleared in the same update.");
    }
}
