namespace NEXUS.Modules.AIPlanning.Application;

public record PlanGenerationCommand(
    string Title,
    string Context,
    string? Provider = null,
    string? Model = null,
    string? Parameters = null
);
