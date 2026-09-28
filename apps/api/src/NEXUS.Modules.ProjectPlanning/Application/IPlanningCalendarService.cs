using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanningCalendarService
{
    Task<Result<IReadOnlyList<CalendarTemplateDto>>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<Result<CalendarTemplateDto>> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<Result<CalendarTemplateDto>> CreateTemplateAsync(CreateCalendarTemplateDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<CalendarTemplateDto>> UpdateTemplateAsync(Guid templateId, UpdateCalendarTemplateDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteTemplateAsync(Guid templateId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PlanCalendarDto>>> GetByPlanAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PlanCalendarDto>>> GetByVersionAsync(Guid projectId, Guid planId, Guid planVersionId, CancellationToken cancellationToken = default);
    Task<Result<PlanCalendarDto>> CreatePlanCalendarAsync(Guid projectId, Guid planId, CreatePlanCalendarDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanCalendarDto>> UpdatePlanCalendarAsync(Guid projectId, Guid planId, Guid calendarId, UpdatePlanCalendarDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeletePlanCalendarAsync(Guid projectId, Guid planId, Guid calendarId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
}
