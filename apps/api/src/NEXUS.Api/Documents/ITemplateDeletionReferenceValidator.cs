namespace NEXUS.Api.Documents;

public interface ITemplateDeletionReferenceValidator
{
    Task<TemplateDeletionReferenceCheck> CheckAsync(
        Guid tenantId,
        Guid templateId,
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken = default);
}

public sealed record TemplateDeletionReferenceCheck(
    bool HasProtectedReferences,
    IReadOnlySet<string> ReferenceKinds);

public static class TemplateDeletionReferenceKinds
{
    public const string ProjectDocument = "ProjectDocument";
    public const string OrganizationPhaseGateConfiguration = "OrganizationPhaseGateConfiguration";
    public const string ProjectPhaseGateRequirementSnapshot = "ProjectPhaseGateRequirementSnapshot";
    public const string ProjectPhaseGateEvidence = "ProjectPhaseGateEvidence";
    public const string DocumentReview = "DocumentReview";
}
