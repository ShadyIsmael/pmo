using FluentValidation;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application.Validators;

public class CreateProjectValidator : AbstractValidator<CreateProjectDto>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required")
            .MaximumLength(200).WithMessage("Project name cannot exceed 200 characters");

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Project code cannot exceed 50 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required")
            .Must(BeValidPriority).WithMessage("Invalid priority value");

        RuleFor(x => x.ConfidenceLevel)
            .Must(BeValidConfidenceLevel).WithMessage("Invalid confidence level value")
            .When(x => !string.IsNullOrWhiteSpace(x.ConfidenceLevel));

        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be a 3-letter code")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate).When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Start date must be before or equal to end date");

        RuleFor(x => x.Budget)
            .GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue)
            .WithMessage("Budget must be non-negative");
    }

    private static bool BeValidPriority(string? priority)
    {
        var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
        return priority != null && validPriorities.Contains(priority);
    }

    private static bool BeValidConfidenceLevel(string? level)
    {
        var validLevels = new[] { "Green", "Amber", "Red" };
        return level != null && validLevels.Contains(level);
    }
}

public class UpdateProjectValidator : AbstractValidator<UpdateProjectDto>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Project name cannot exceed 200 characters")
            .When(x => x.Name != null);

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Project code cannot exceed 50 characters")
            .When(x => x.Code != null);

        RuleFor(x => x.Priority)
            .Must(BeValidPriority).WithMessage("Invalid priority value")
            .When(x => x.Priority != null);

        RuleFor(x => x.ConfidenceLevel)
            .Must(BeValidConfidenceLevel).WithMessage("Invalid confidence level value")
            .When(x => x.ConfidenceLevel != null);

        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be a 3-letter code")
            .When(x => x.Currency != null);

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate).When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Start date must be before or equal to end date");

        RuleFor(x => x.Budget)
            .GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue)
            .WithMessage("Budget must be non-negative");

        When(x => x.Handover is not null, () =>
        {
            RuleFor(x => x.Handover!.HandoverDate)
                .NotNull().WithMessage("Handover date is required when changing the project manager");

            RuleFor(x => x.Handover!.HandoverReason)
                .NotEmpty().WithMessage("Handover reason is required when changing the project manager")
                .MaximumLength(500).WithMessage("Handover reason cannot exceed 500 characters");

            RuleFor(x => x.Handover!.HandoverNotes)
                .MaximumLength(2000).WithMessage("Handover notes cannot exceed 2000 characters");
        });
    }

    private static bool BeValidPriority(string? priority)
    {
        var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
        return priority != null && validPriorities.Contains(priority);
    }

    private static bool BeValidConfidenceLevel(string? level)
    {
        var validLevels = new[] { "Green", "Amber", "Red" };
        return level != null && validLevels.Contains(level);
    }
}

public class CreateProjectHandoverRequestValidator : AbstractValidator<CreateProjectHandoverRequestDto>
{
    public CreateProjectHandoverRequestValidator()
    {
        RuleFor(x => x.NewProjectManagerId)
            .NotEmpty().WithMessage("New project manager is required");

        RuleFor(x => x.EffectiveHandoverDate)
            .NotEmpty().WithMessage("Effective handover date is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Handover reason is required")
            .MaximumLength(500).WithMessage("Handover reason cannot exceed 500 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Handover notes cannot exceed 2000 characters");
    }
}

public class RejectProjectHandoverRequestValidator : AbstractValidator<RejectProjectHandoverRequestDto>
{
    public RejectProjectHandoverRequestValidator()
    {
        RuleFor(x => x.RejectionReason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MaximumLength(500).WithMessage("Rejection reason cannot exceed 500 characters");
    }
}

public class CreateProjectMemberValidator : AbstractValidator<CreateProjectMemberDto>
{
    public CreateProjectMemberValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required");

        RuleFor(x => x)
            .Must(x => x.ResourceId.HasValue != x.TeamId.HasValue)
            .WithMessage("Choose exactly one Resource or Team");

        RuleFor(x => x.ResourceId)
            .NotEmpty().When(x => x.ResourceId.HasValue)
            .WithMessage("Resource ID cannot be empty");

        RuleFor(x => x.TeamId)
            .NotEmpty().When(x => x.TeamId.HasValue)
            .WithMessage("Team ID cannot be empty");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .MaximumLength(100).WithMessage("Role cannot exceed 100 characters");

        RuleFor(x => x.AllocationPercentage)
            .GreaterThan(0).WithMessage("Allocation percentage must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Allocation percentage cannot exceed 100");

        RuleFor(x => x.HoursPerWeek)
            .GreaterThan(0).When(x => x.HoursPerWeek.HasValue)
            .WithMessage("Hours per week must be greater than 0")
            .LessThanOrEqualTo(168).When(x => x.HoursPerWeek.HasValue)
            .WithMessage("Hours per week cannot exceed 168");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(BeValidStatus).WithMessage("Invalid status value");
    }

    private static bool BeValidStatus(string status)
    {
        var validStatuses = new[] { "Active", "Inactive", "Pending", "Removed" };
        return validStatuses.Contains(status);
    }
}

public class UpdateProjectMemberValidator : AbstractValidator<UpdateProjectMemberDto>
{
    public UpdateProjectMemberValidator()
    {
        RuleFor(x => x.Role)
            .MaximumLength(100).WithMessage("Role cannot exceed 100 characters")
            .When(x => x.Role != null);

        RuleFor(x => x.AllocationPercentage)
            .GreaterThan(0).WithMessage("Allocation percentage must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Allocation percentage cannot exceed 100")
            .When(x => x.AllocationPercentage.HasValue);

        RuleFor(x => x.HoursPerWeek)
            .GreaterThan(0).When(x => x.HoursPerWeek.HasValue)
            .WithMessage("Hours per week must be greater than 0")
            .LessThanOrEqualTo(168).When(x => x.HoursPerWeek.HasValue)
            .WithMessage("Hours per week cannot exceed 168");

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate).When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Start date must be before end date");

        RuleFor(x => x.Status)
            .Must(BeValidStatus).WithMessage("Invalid status value")
            .When(x => x.Status != null);
    }

    private static bool BeValidStatus(string? status)
    {
        var validStatuses = new[] { "Active", "Inactive", "Pending", "Removed" };
        return status != null && validStatuses.Contains(status);
    }
}
