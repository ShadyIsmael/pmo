namespace NEXUS.Modules.ProjectPlanning.Application.Import;

public sealed class PlanningImportParseException : Exception
{
    public PlanningImportParseException(string message) : base(message) { }
    public PlanningImportParseException(string message, Exception innerException) : base(message, innerException) { }
}
