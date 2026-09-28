using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Contracts;

public sealed record WorkflowFactMetadata(string Code, string NameEn, string NameAr, string ValueType);

public sealed record WorkflowEntityTypeMetadata(
    string EntityType,
    string NameEn,
    string NameAr,
    IReadOnlyList<WorkflowFactMetadata> Facts);

public interface IWorkflowEntityMetadataProvider
{
    string EntityType { get; }
    WorkflowEntityTypeMetadata GetMetadata();
}

public interface IWorkflowDefinitionConsumerValidator
{
    string EntityType { get; }
    Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default);
}

public sealed record WorkflowValidationReport(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
