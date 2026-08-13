using FluentValidation;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.Validators;

public class CreateProjectChangeRequestValidator : AbstractValidator<CreateProjectChangeRequestDto>
{
    public CreateProjectChangeRequestValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid change type.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason for the change is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");

        RuleFor(x => x.Justification)
            .MaximumLength(2000).WithMessage("Justification cannot exceed 2000 characters.");

        RuleFor(x => x.Impact)
            .MaximumLength(2000).WithMessage("Impact description cannot exceed 2000 characters");

        RuleFor(x => x.Attachments)
            .MaximumLength(4000).WithMessage("Attachments cannot exceed 4000 characters");
    }
}

public class ApproveProjectChangeRequestValidator : AbstractValidator<ApproveProjectChangeRequestDto>
{
    public ApproveProjectChangeRequestValidator()
    {
        RuleFor(x => x.DecisionNotes)
            .MaximumLength(2000).WithMessage("Decision notes cannot exceed 2000 characters");
    }
}

public class RejectProjectChangeRequestValidator : AbstractValidator<RejectProjectChangeRequestDto>
{
    public RejectProjectChangeRequestValidator()
    {
        RuleFor(x => x.DecisionNotes)
            .NotEmpty().WithMessage("Decision notes are required when rejecting a change request.")
            .MaximumLength(2000).WithMessage("Decision notes cannot exceed 2000 characters");
    }
}