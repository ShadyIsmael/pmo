namespace NEXUS.Modules.ProjectPlanning.Application.Import;

public interface IPlanningImportProvider
{
    string Format { get; }
    IReadOnlyCollection<string> SupportedExtensions { get; }
    bool CanRead(string fileName);
    Task<PlanningImportDocument> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
