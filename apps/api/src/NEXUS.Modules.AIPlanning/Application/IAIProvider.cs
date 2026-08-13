using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Application;

public interface IAIProvider
{
    Task<Result<string>> GeneratePlanAsync(string context, string? parameters = null, CancellationToken cancellationToken = default);
}
