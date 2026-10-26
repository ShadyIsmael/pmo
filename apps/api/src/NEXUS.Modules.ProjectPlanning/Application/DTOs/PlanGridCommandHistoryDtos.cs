namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public sealed class PlanGridCommandHistoryStateDto
{
    public bool CanUndo { get; init; }
    public Guid? UndoCommandId { get; init; }
    public string? UndoOperation { get; init; }
    public DateTime? UndoCommittedAt { get; init; }
    public bool CanRedo { get; init; }
    public Guid? RedoCommandId { get; init; }
    public string? RedoOperation { get; init; }
    public DateTime? RedoCommittedAt { get; init; }
}

public sealed class PlanGridCommandHistoryActionDto
{
    public Guid CommandId { get; init; }
    public string State { get; init; } = string.Empty;
    public PlanGridCommandHistoryStateDto History { get; init; } = new();
}
