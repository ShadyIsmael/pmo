using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Application.Validators;

public class CreateResourceValidator : AbstractValidator<CreateResourceDto>
{
    public CreateResourceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EmploymentType).MaximumLength(50);
        RuleFor(x => x.Seniority).MaximumLength(50);
        RuleFor(x => x.UtilizationTarget).InclusiveBetween(0, 100).When(x => x.UtilizationTarget.HasValue);
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
        RuleFor(x => x.Name).MaximumLength(200).When(x => x.Name != null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Type).MaximumLength(50).When(x => x.Type != null);
        RuleFor(x => x.Status).MaximumLength(50).When(x => x.Status != null);
        RuleFor(x => x.EmploymentType).MaximumLength(50);
        RuleFor(x => x.Seniority).MaximumLength(50);
    }
}
