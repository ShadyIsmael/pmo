using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Application.Validators;

public class CreateSkillValidator : AbstractValidator<CreateSkillDto>
{
    public CreateSkillValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class CreateVendorValidator : AbstractValidator<CreateVendorDto>
{
    public CreateVendorValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrEmpty(x.ContactEmail));
        RuleFor(x => x.ContactPhone).MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0).When(x => x.ContractValue.HasValue);
    }
}
