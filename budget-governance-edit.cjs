const fs = require('fs');
const root = 'apps/api/src/';
function edit(path, from, to) { let s = fs.readFileSync(path,'utf8').replace(/\r/g,''); from=from.replace(/\r/g,''); if(!s.includes(from)) throw Error('Missing text '+path+': '+from.slice(0,60)); fs.writeFileSync(path,s.replace(from,to)); }
function e(path,from,to){edit(root+path,from,to);}
e('NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs','public class ProjectChangeRequestDto\n{'.replaceAll('\n','\r\n'),`public class ProjectChangeRequestDto
{
    public decimal? BudgetBeforeChange { get; set; }
    public decimal? BudgetChangeAmount { get; set; }
    public decimal? ProposedProjectBudget { get; set; }`);
e('NEXUS.Modules.ProjectManagement/Application/Mapping/ProjectMapping.cs','            Impact = request.Impact,',`            Impact = request.Impact,
            BudgetBeforeChange = request.BudgetBeforeChange,
            BudgetChangeAmount = request.BudgetChangeAmount,
            ProposedProjectBudget = request.BudgetBeforeChange + request.BudgetChangeAmount,`);
e('NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs','        // Generate a sequential change request number per project.',`        if (dto.BudgetChangeAmount.HasValue &&
            (dto.BudgetChangeAmount == 0 || decimal.Round(dto.BudgetChangeAmount.Value, 2) != dto.BudgetChangeAmount ||
             string.IsNullOrWhiteSpace(dto.Justification) || !project.Budget.HasValue || project.Budget + dto.BudgetChangeAmount < 0))
            return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Budget impact requires an established project ceiling, a non-zero amount with at most two decimal places, a non-negative resulting budget and justification."));

        // Generate a sequential change request number per project.`);
e('NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs','        request.Number = nextNumber;',`        request.Number = nextNumber;
        request.BudgetChangeAmount = dto.BudgetChangeAmount;
        request.BudgetBeforeChange = dto.BudgetChangeAmount.HasValue ? project.Budget : null;`);
e('NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs','        request.Status = ProjectChangeRequest.Statuses.Approved;',`        if (request.BudgetChangeAmount.HasValue)
        {
            var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.TenantId == tenantId, cancellationToken);
            if (project is null) return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
            if (!request.BudgetBeforeChange.HasValue || project.Budget != request.BudgetBeforeChange)
                return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("The approved project budget has changed. Cancel and resubmit this Change Request against the current ceiling."));
            var proposed = request.BudgetBeforeChange.Value + request.BudgetChangeAmount.Value;
            var financials = await _financialLookup.GetAsync(project.Id, project.Currency, cancellationToken);
            if (proposed < 0 || proposed < (financials?.AllocatedBudget ?? 0) ||
                (request.BudgetChangeAmount < 0 && proposed < (financials?.ActualCost ?? 0) + (financials?.CommittedCost ?? 0)))
                return Result<ProjectChangeRequestDto>.Failure(Error.Validation("The resulting project budget cannot be below current allocations or financial obligations."));
            project.Budget = proposed;
            project.UpdatedBy = userId;
            project.UpdatedAt = DateTime.UtcNow;
        }

        request.Status = ProjectChangeRequest.Statuses.Approved;`);
e('NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs','new { request.Status, request.ReviewedBy, request.ReviewedAt, DecisionNotes = decisionNotes }','new { request.Status, request.ReviewedBy, request.ReviewedAt, request.BudgetBeforeChange, request.BudgetChangeAmount, ProposedProjectBudget = request.BudgetBeforeChange + request.BudgetChangeAmount, DecisionNotes = decisionNotes }');
e('NEXUS.Persistence/Configurations/ProjectChangeRequestConfiguration.cs','        builder.Property(x => x.Impact);',`        builder.Property(x => x.Impact);
        builder.Property(x => x.BudgetBeforeChange).HasPrecision(18, 2);
        builder.Property(x => x.BudgetChangeAmount).HasPrecision(18, 2);`);
console.log('Core governance edits applied');

