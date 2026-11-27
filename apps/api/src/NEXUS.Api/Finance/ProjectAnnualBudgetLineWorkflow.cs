using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

/// <summary>Draft-only Annual Budget Line configuration; approved lines are immutable.</summary>
public sealed class ProjectAnnualBudgetLineWorkflow(
    AppDbContext db,
    IUserEffectivePermissionService permissions,
    IManagementContextAccessor management)
{
    private const string ViewPermission = "ProjectAnnualBudget.View";
    private const string EditPermission = "ProjectAnnualBudget.EditDraft";
    private const string ManageLinesPermission = "ProjectAnnualBudget.ManageLines";

    public async Task<Result<IReadOnlyList<ProjectAnnualBudgetLineDto>>> GetPageAsync(
        Guid tenantId, Guid projectId, Guid budgetId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await RequireAccessAsync(tenantId, projectId, actorId, ViewPermission, false, cancellationToken);
        if (access is not null)
            return Result<IReadOnlyList<ProjectAnnualBudgetLineDto>>.Failure(access);
        var budget = await FindBudgetAsync(tenantId, projectId, budgetId, cancellationToken);
        if (budget is null)
            return Result<IReadOnlyList<ProjectAnnualBudgetLineDto>>.Failure(Error.NotFound("Annual Budget not found."));

        var reservationResult = await GetLineReservationsAsync(tenantId, projectId, budgetId, cancellationToken);
        if (reservationResult.IsFailure)
            return Result<IReadOnlyList<ProjectAnnualBudgetLineDto>>.Failure(reservationResult.Error!);
        var reservations = reservationResult.Value!;
        var canEdit = await CanManageBudgetLinesAsync(tenantId, projectId, budget, actorId, cancellationToken);
        var lines = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == budgetId && !item.IsDeleted)
            .OrderBy(item => item.Reference)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<ProjectAnnualBudgetLineDto>>.Success(lines.Select(line =>
        {
            var reserved = reservations.GetValueOrDefault(line.Id);
            return Map(line, budget, reserved, canEdit);
        }).ToArray());
    }

    public async Task<Result<ProjectAnnualBudgetLineDto>> CreateAsync(
        Guid tenantId, Guid projectId, Guid budgetId, Guid actorId, string? actorName,
        CreateProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        var access = await RequireAccessAsync(tenantId, projectId, actorId, EditPermission, true, cancellationToken,
            alternatePermission: ManageLinesPermission);
        if (access is not null) return Result<ProjectAnnualBudgetLineDto>.Failure(access);
        var description = dto.Description?.Trim() ?? string.Empty;
        if (description.Length is 0 or > 200 || !IsValidAmount(dto.AuthorizedLimit))
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Validation(
                "A Budget Line description and a positive amount with at most four decimal places are required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var budget = await FindBudgetForUpdateAsync(tenantId, projectId, budgetId, cancellationToken);
        if (budget is null) return Result<ProjectAnnualBudgetLineDto>.Failure(Error.NotFound("Annual Budget not found."));
        var mutationError = await ValidateBudgetLineMutationAsync(tenantId, projectId, budget, actorId, cancellationToken);
        if (mutationError is not null) return Result<ProjectAnnualBudgetLineDto>.Failure(mutationError);

        var currentTotal = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == budgetId && !item.IsDeleted)
            .SumAsync(item => (decimal?)item.AuthorizedLimit, cancellationToken) ?? 0m;
        if (currentTotal + dto.AuthorizedLimit > budget.TotalAnnualBudget)
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Conflict(
                "The combined Budget Line limits cannot exceed the Annual Budget amount."));

        var lineId = Guid.NewGuid();
        var line = new ProjectAnnualBudgetLine
        {
            Id = lineId,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectAnnualBudgetId = budgetId,
            Reference = MakeReference(lineId),
            Description = description,
            AuthorizedLimit = dto.AuthorizedLimit,
            CreatedBy = actorId.ToString("D")
        };
        db.ProjectAnnualBudgetLines.Add(line);
        AddAudit(tenantId, projectId, budget, line, actorId, actorName, "Created", null,
            new { line.Reference, line.Description, line.AuthorizedLimit, currency = budget.Currency });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ProjectAnnualBudgetLineDto>.Success(Map(line, budget, 0m, true));
    }

    public async Task<Result<ProjectAnnualBudgetLineDto>> UpdateAsync(
        Guid tenantId, Guid projectId, Guid budgetId, Guid lineId, Guid actorId, string? actorName,
        UpdateProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        var access = await RequireAccessAsync(tenantId, projectId, actorId, EditPermission, true, cancellationToken,
            alternatePermission: ManageLinesPermission);
        if (access is not null) return Result<ProjectAnnualBudgetLineDto>.Failure(access);
        var description = dto.Description?.Trim() ?? string.Empty;
        if (description.Length is 0 or > 200 || !IsValidAmount(dto.AuthorizedLimit))
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Validation(
                "A Budget Line description and a positive amount with at most four decimal places are required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var budget = await FindBudgetForUpdateAsync(tenantId, projectId, budgetId, cancellationToken);
        if (budget is null) return Result<ProjectAnnualBudgetLineDto>.Failure(Error.NotFound("Annual Budget not found."));
        var mutationError = await ValidateBudgetLineMutationAsync(tenantId, projectId, budget, actorId, cancellationToken);
        if (mutationError is not null) return Result<ProjectAnnualBudgetLineDto>.Failure(mutationError);
        var line = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == budgetId && item.Id == lineId && !item.IsDeleted, cancellationToken);
        if (line is null) return Result<ProjectAnnualBudgetLineDto>.Failure(Error.NotFound("Budget Line not found."));
        if (!MatchesVersion(line.RowVersion, dto.RowVersion))
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Conflict("Budget Line changed. Refresh before editing."));
        if (await HasRequestReferenceAsync(tenantId, projectId, lineId, cancellationToken))
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Conflict("A Budget Line linked to a Payment Request cannot be changed."));

        var currentTotal = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == budgetId && !item.IsDeleted && item.Id != lineId)
            .SumAsync(item => (decimal?)item.AuthorizedLimit, cancellationToken) ?? 0m;
        if (currentTotal + dto.AuthorizedLimit > budget.TotalAnnualBudget)
            return Result<ProjectAnnualBudgetLineDto>.Failure(Error.Conflict(
                "The combined Budget Line limits cannot exceed the Annual Budget amount."));

        var before = new { line.Description, line.AuthorizedLimit };
        line.Description = description;
        line.AuthorizedLimit = dto.AuthorizedLimit;
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = actorId.ToString("D");
        AddAudit(tenantId, projectId, budget, line, actorId, actorName, "Updated", before,
            new { line.Reference, line.Description, line.AuthorizedLimit, currency = budget.Currency });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ProjectAnnualBudgetLineDto>.Success(Map(line, budget, 0m, true));
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId, Guid projectId, Guid budgetId, Guid lineId, Guid actorId, string? actorName,
        DeleteProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        var access = await RequireAccessAsync(tenantId, projectId, actorId, EditPermission, true, cancellationToken,
            alternatePermission: ManageLinesPermission);
        if (access is not null) return Result.Failure(access);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var budget = await FindBudgetForUpdateAsync(tenantId, projectId, budgetId, cancellationToken);
        if (budget is null) return Result.Failure(Error.NotFound("Annual Budget not found."));
        var mutationError = await ValidateBudgetLineMutationAsync(tenantId, projectId, budget, actorId, cancellationToken);
        if (mutationError is not null) return Result.Failure(mutationError);
        var line = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == budgetId && item.Id == lineId && !item.IsDeleted, cancellationToken);
        if (line is null) return Result.Failure(Error.NotFound("Budget Line not found."));
        if (!MatchesVersion(line.RowVersion, dto.RowVersion))
            return Result.Failure(Error.Conflict("Budget Line changed. Refresh before deleting."));
        if (await HasRequestReferenceAsync(tenantId, projectId, lineId, cancellationToken))
            return Result.Failure(Error.Conflict("A Budget Line linked to a Payment Request cannot be deleted."));

        var before = new { line.Reference, line.Description, line.AuthorizedLimit };
        line.IsDeleted = true;
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = actorId.ToString("D");
        AddAudit(tenantId, projectId, budget, line, actorId, actorName, "Deleted", before, null);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> RequireAccessAsync(Guid tenantId, Guid projectId, Guid actorId,
        string permission, bool write, CancellationToken cancellationToken, string? alternatePermission = null)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || projectId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId || !current.Scope.ProjectIds.Contains(projectId))
            return Error.NotFound("Project not found.");
        if (write && current.EffectiveReadOnly)
            return Error.Forbidden("This operation is unavailable in a read-only management context.");
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == actorId && user.TenantId == tenantId
            && user.IsActive && !user.IsDeleted, cancellationToken))
            return Error.Forbidden("An active tenant user is required.");
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return effective.Contains(permission) || (alternatePermission is not null && effective.Contains(alternatePermission))
            ? null : Error.Forbidden($"The {permission} permission is required.");
    }

    private async Task<Error?> ValidateBudgetLineMutationAsync(
        Guid tenantId, Guid projectId, ProjectAnnualBudget budget, Guid actorId, CancellationToken cancellationToken)
    {
        if (budget.ClosedAtUtc.HasValue || budget.Status is not (ProjectAnnualBudgetStatuses.Draft or ProjectAnnualBudgetStatuses.Approved))
            return Error.Conflict("Budget Lines can be changed only on an open Draft or Approved Annual Budget.");
        if (await HasPendingReviewAsync(tenantId, budget.Id, cancellationToken))
            return Error.Conflict("Budget Lines are locked while an Annual Budget review or revision is pending.");
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (budget.Status == ProjectAnnualBudgetStatuses.Approved && !effective.Contains(ManageLinesPermission))
            return Error.Forbidden($"The {ManageLinesPermission} permission is required to manage lines on an approved Annual Budget.");
        if (budget.Status == ProjectAnnualBudgetStatuses.Draft
            && !effective.Contains(EditPermission) && !effective.Contains(ManageLinesPermission))
            return Error.Forbidden($"The {EditPermission} or {ManageLinesPermission} permission is required.");
        return null;
    }

    private async Task<bool> CanManageBudgetLinesAsync(
        Guid tenantId, Guid projectId, ProjectAnnualBudget budget, Guid actorId, CancellationToken cancellationToken)
    {
        if (management.Current?.EffectiveReadOnly == true || budget.ClosedAtUtc.HasValue
            || budget.Status is not (ProjectAnnualBudgetStatuses.Draft or ProjectAnnualBudgetStatuses.Approved)
            || await HasPendingReviewAsync(tenantId, budget.Id, cancellationToken))
            return false;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return budget.Status == ProjectAnnualBudgetStatuses.Approved
            ? effective.Contains(ManageLinesPermission)
            : effective.Contains(EditPermission) || effective.Contains(ManageLinesPermission);
    }

    private Task<bool> HasPendingReviewAsync(Guid tenantId, Guid budgetId, CancellationToken cancellationToken) =>
        db.ApprovalRequests.IgnoreQueryFilters().AnyAsync(request => request.TenantId == tenantId
            && !request.IsDeleted && request.EntityType == "ProjectAnnualBudget" && request.EntityId == budgetId
            && (request.Type == "ProjectAnnualBudgetReview" || request.Type == "ProjectAnnualBudgetRevision")
            && request.Status == PaymentRequestStatuses.Pending, cancellationToken);

    private Task<ProjectAnnualBudget?> FindBudgetAsync(Guid tenantId, Guid projectId, Guid budgetId, CancellationToken cancellationToken) =>
        db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item => item.Id == budgetId
            && item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken);

    private async Task<ProjectAnnualBudget?> FindBudgetForUpdateAsync(Guid tenantId, Guid projectId, Guid budgetId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            var connection = db.Database.GetDbConnection();
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT [Id] FROM [ProjectAnnualBudgets] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @budgetId AND [TenantId] = @tenantId AND [ProjectId] = @projectId AND [IsDeleted] = 0";
            AddParameter(command, "@budgetId", budgetId);
            AddParameter(command, "@tenantId", tenantId);
            AddParameter(command, "@projectId", projectId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull) return null;
        }
        return await db.ProjectAnnualBudgets.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == budgetId
            && item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Reserved amount per Budget Line. This must match the allocation the Payment Request
    /// workflow enforces on submit and approval: active (Pending/Approved) Payment Requests
    /// minus approved Reversal Requests that release those requests. Counting reversal details
    /// as additional consumption would understate availability and double count a reversal.
    /// </summary>
    private async Task<Result<Dictionary<Guid, decimal>>> GetLineReservationsAsync(
        Guid tenantId, Guid projectId, Guid budgetId, CancellationToken cancellationToken)
    {
        var activeStatuses = new[] { PaymentRequestStatuses.Pending, PaymentRequestStatuses.Approved };
        var requests = await (from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                              join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                                  on detail.ApprovalRequestId equals request.Id
                              where detail.TenantId == tenantId && detail.ProjectId == projectId
                                  && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                                  && detail.ProjectAnnualBudgetId == budgetId && detail.ProjectAnnualBudgetLineId.HasValue
                                  && !request.IsDeleted
                                  && activeStatuses.Contains(request.Status)
                              select new
                              {
                                  detail.ProjectAnnualBudgetLineId,
                                  request.Id,
                                  request.Amount,
                                  request.Currency,
                                  request.Status
                              }).ToListAsync(cancellationToken);
        if (requests.Count == 0) return Result<Dictionary<Guid, decimal>>.Success(new Dictionary<Guid, decimal>());

        var requestGroups = requests.GroupBy(item => item.Id).ToArray();
        if (requestGroups.Any(group => group.Select(item => item.ProjectAnnualBudgetLineId).Distinct().Count() != 1))
            return Result<Dictionary<Guid, decimal>>.Failure(Error.Conflict(
                "An active Payment Request is allocated to multiple Budget Lines without a safe split; Budget Line availability cannot be calculated."));
        var activeRequests = requestGroups.Select(group => group.First()).ToArray();

        var requestIds = activeRequests.Select(item => item.Id).ToArray();
        var approvedReversals = await (
            from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals reversal.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && detail.ReversesApprovalRequestId.HasValue
                && requestIds.Contains(detail.ReversesApprovalRequestId.Value)
                && reversal.Status == PaymentRequestStatuses.Approved
                && !reversal.IsDeleted
            select new { RequestId = detail.ReversesApprovalRequestId!.Value, reversal.Amount, reversal.Currency }
        ).ToListAsync(cancellationToken);

        var requestById = activeRequests.ToDictionary(item => item.Id);
        if (approvedReversals.Any(item => !requestById.TryGetValue(item.RequestId, out var request)
                || !string.Equals(NormalizeCurrency(item.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)))
            return Result<Dictionary<Guid, decimal>>.Failure(Error.Conflict(
                "An approved reversal uses a different currency from its Payment Request; Budget Line availability cannot be calculated safely."));

        var releasedByRequest = approvedReversals.GroupBy(item => item.RequestId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount ?? 0m));

        var result = new Dictionary<Guid, decimal>();
        foreach (var group in activeRequests.GroupBy(item => item.ProjectAnnualBudgetLineId!.Value))
        {
            var reserved = 0m;
            foreach (var row in group)
            {
                var amount = row.Amount ?? 0m;
                var released = releasedByRequest.GetValueOrDefault(row.Id);
                if (amount <= 0m) continue;
                reserved += amount - Math.Min(released, amount);
            }
            result[group.Key] = reserved;
        }

        // Do not publish a free-balance figure if an active request cannot be compared with its
        // Annual Budget. Returning zero here would understate the reservation and invite overspend.
        var budgetCurrency = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == budgetId && item.TenantId == tenantId
                && item.ProjectId == projectId && !item.IsDeleted)
            .Select(item => item.Currency)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(budgetCurrency))
            return Result<Dictionary<Guid, decimal>>.Failure(Error.Conflict(
                "The Annual Budget currency is unavailable; Budget Line availability cannot be calculated safely."));

        var expectedCurrency = NormalizeCurrency(budgetCurrency);
        foreach (var row in activeRequests)
        {
            if (!string.Equals(NormalizeCurrency(row.Currency), expectedCurrency, StringComparison.Ordinal))
                return Result<Dictionary<Guid, decimal>>.Failure(Error.Conflict(
                    "An active Payment Request uses a different currency from its Annual Budget; Budget Line availability cannot be calculated safely."));
            var amount = row.Amount ?? 0m;
            var released = releasedByRequest.GetValueOrDefault(row.Id);
            if (amount <= 0m || released > amount)
                return Result<Dictionary<Guid, decimal>>.Failure(Error.Conflict(
                    "An active Payment Request has an invalid amount or over-released reservation; Budget Line availability cannot be calculated safely."));
        }
        return Result<Dictionary<Guid, decimal>>.Success(result);
    }

    private static string NormalizeCurrency(string? currency) =>
        currency?.Trim().ToUpperInvariant() ?? string.Empty;

    private Task<bool> HasRequestReferenceAsync(Guid tenantId, Guid projectId, Guid lineId, CancellationToken cancellationToken) =>
        db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.ProjectAnnualBudgetLineId == lineId, cancellationToken);

    private void AddAudit(Guid tenantId, Guid projectId, ProjectAnnualBudget budget, ProjectAnnualBudgetLine line,
        Guid actorId, string? actorName, string action, object? before, object? after)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = "ProjectAnnualBudgetLine",
            EntityId = line.Id,
            Action = action,
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = DateTime.UtcNow,
            Description = $"Budget Line {line.Reference} {action.ToLowerInvariant()} under Annual Budget {budget.FinancialReference}.",
            OriginalValues = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            ChangedFields = "reference,description,authorizedLimit,currency,annualBudgetId,fiscalYear",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
    }

    private static ProjectAnnualBudgetLineDto Map(ProjectAnnualBudgetLine line, ProjectAnnualBudget budget,
        decimal reserved, bool canEdit) => new(line.Id, budget.Id, line.Reference, line.Description,
        line.AuthorizedLimit, reserved, Math.Max(0m, line.AuthorizedLimit - reserved), budget.Currency, line.RowVersion, canEdit);

    private static void AddParameter(DbCommand command, string name, Guid value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Guid;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string MakeReference(Guid id) => $"ABL-{id:N}".ToUpperInvariant();
    private static bool IsValidAmount(decimal amount) => amount > 0m && amount < 1_000_000_000_000_000m && decimal.Round(amount, 4) == amount;
    private static bool MatchesVersion(byte[]? current, byte[]? expected) => current is { Length: > 0 } && expected is { Length: > 0 } && current.SequenceEqual(expected);
}

