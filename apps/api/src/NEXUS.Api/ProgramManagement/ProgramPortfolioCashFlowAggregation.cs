using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Api.ProgramManagement;

/// <summary>
/// Combines project Cash Flow responses without crossing currency, date, or
/// lifecycle boundaries. This type deliberately contains no database access.
/// </summary>
public static class ProgramPortfolioCashFlowAggregation
{
    public const string MissingProjectResponseCode = "PROJECT_CASH_FLOW_RESPONSE_UNAVAILABLE";
    public const string ProjectResponseMismatchCode = "PROJECT_CASH_FLOW_PROJECT_ID_MISMATCH";

    public static ProgramPortfolioCashFlowDto Aggregate(
        string entityType,
        Guid entityId,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CashFlowGranularity granularity,
        ProgramPortfolioCashFlowProjectScopeDto scope,
        IReadOnlyDictionary<Guid, ProjectCashFlowDto> projectCashFlows)
    {
        var projects = scope.Projects
            .GroupBy(item => item.ProjectId)
            .Select(group => group.First())
            .OrderBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProjectId)
            .Select(item => CreateContribution(item, periodFrom, periodTo, granularity, projectCashFlows))
            .ToArray();

        var currencyInputs = projects.SelectMany(CreateCurrencyInputs).ToArray();
        var currencyGroups = currencyInputs
            .GroupBy(item => item.Currency, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key is null ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => AggregateCurrencyGroup(group.Key, group.ToArray(), scope.PartialCoverage))
            .ToArray();

        var dataQualityIssues = projects
            .SelectMany(project => project.CashFlow.DataQualityIssues.Select(issue =>
                new ProgramPortfolioCashFlowQualityIssueDto(project.ProjectId, project.ProjectName, issue)))
            .Concat(projects.Where(project => project.CashFlow.DataQualityStatus == CashFlowDataQualityStatus.Unavailable
                && project.CashFlow.DataQualityIssues.Count == 0)
                .Select(project => new ProgramPortfolioCashFlowQualityIssueDto(
                    project.ProjectId,
                    project.ProjectName,
                    new CashFlowQualityIssueDto(
                        MissingProjectResponseCode,
                        CashFlowDataQualityStatus.Unavailable,
                        null,
                        project.ProjectId,
                        null,
                        null,
                        null,
                        project.Currency))))
            .DistinctBy(IssueIdentity)
            .OrderBy(issue => issue.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Issue.SourceId)
            .ToArray();

        var projectStatuses = projects.Select(item => item.CashFlow.DataQualityStatus).ToArray();
        var status = CombineStatus(projectStatuses
            .Concat(currencyGroups.Select(group => group.DataQualityStatus))
            .ToArray(), scope.PartialCoverage);
        var unavailableCount = projects.Count(project =>
            project.CashFlow.DataQualityStatus == CashFlowDataQualityStatus.Unavailable);
        var knownCurrencyCount = currencyGroups.Count(group => group.Currency is not null);

        return new ProgramPortfolioCashFlowDto(
            entityType,
            entityId,
            periodFrom,
            periodTo,
            granularity,
            status,
            scope.PartialCoverage,
            projects.Length,
            unavailableCount,
            knownCurrencyCount > 1 || currencyGroups.Any(group => group.Currency is null) && knownCurrencyCount > 0,
            currencyGroups,
            projects,
            dataQualityIssues);
    }

    private static ProgramPortfolioCashFlowProjectContributionDto CreateContribution(
        ProgramPortfolioCashFlowProjectMembershipDto membership,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CashFlowGranularity granularity,
        IReadOnlyDictionary<Guid, ProjectCashFlowDto> projectCashFlows)
    {
        if (!projectCashFlows.TryGetValue(membership.ProjectId, out var cashFlow))
        {
            cashFlow = new ProjectCashFlowDto(
                membership.ProjectId,
                periodFrom,
                periodTo,
                granularity,
                CashFlowDataQualityStatus.Unavailable,
                Array.Empty<CashFlowQualityIssueDto>(),
                Array.Empty<ProjectCashFlowCurrencyGroupDto>());
        }
        else if (cashFlow.ProjectId != membership.ProjectId)
        {
            cashFlow = new ProjectCashFlowDto(
                membership.ProjectId,
                periodFrom,
                periodTo,
                granularity,
                CashFlowDataQualityStatus.Unavailable,
                cashFlow.DataQualityIssues.Append(new CashFlowQualityIssueDto(
                    ProjectResponseMismatchCode,
                    CashFlowDataQualityStatus.Unavailable,
                    null,
                    cashFlow.ProjectId,
                    null,
                    null,
                    null,
                    membership.Currency)).ToArray(),
                Array.Empty<ProjectCashFlowCurrencyGroupDto>());
        }
        else if (cashFlow.PeriodFrom != periodFrom || cashFlow.PeriodTo != periodTo || cashFlow.Granularity != granularity)
        {
            cashFlow = new ProjectCashFlowDto(
                cashFlow.ProjectId,
                periodFrom,
                periodTo,
                granularity,
                CashFlowDataQualityStatus.Unavailable,
                cashFlow.DataQualityIssues.ToArray(),
                Array.Empty<ProjectCashFlowCurrencyGroupDto>());
        }

        return new ProgramPortfolioCashFlowProjectContributionDto(
            membership.ProjectId,
            membership.ProjectPublicId,
            membership.ProjectName,
            membership.Currency,
            membership.ProgramId,
            membership.ProgramName,
            membership.IsDirectProject,
            cashFlow);
    }

    private static IEnumerable<ProjectCurrencyInput> CreateCurrencyInputs(
        ProgramPortfolioCashFlowProjectContributionDto project)
    {
        var groups = project.CashFlow.CurrencyGroups
            .GroupBy(group => NormalizeCurrency(group.Currency), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProjectCurrencyInput(project, group.Key, group.ToArray()))
            .ToArray();

        var projectCurrency = NormalizeCurrency(project.Currency);
        if (projectCurrency is not null && groups.All(group => group.Currency != projectCurrency))
        {
            groups = groups.Append(new ProjectCurrencyInput(
                project,
                projectCurrency,
                Array.Empty<ProjectCashFlowCurrencyGroupDto>())).ToArray();
        }

        if (groups.Length > 0)
        {
            return groups;
        }

        if (project.CashFlow.DataQualityStatus == CashFlowDataQualityStatus.Available)
        {
            return Array.Empty<ProjectCurrencyInput>();
        }

        // Keep unavailable project data visible under its authoritative project
        // currency, or an explicit unknown-currency group when that is absent.
        return [new ProjectCurrencyInput(project, projectCurrency, Array.Empty<ProjectCashFlowCurrencyGroupDto>())];
    }

    private static ProgramPortfolioCashFlowCurrencyGroupDto AggregateCurrencyGroup(
        string? currency,
        ProjectCurrencyInput[] contributors,
        bool partialCoverage)
    {
        var periods = contributors
            .SelectMany(input => input.Groups.SelectMany(group => group.Buckets.Select(bucket => bucket.PeriodStart)))
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        var buckets = periods.Select(period => AggregateBucket(period, currency, contributors, partialCoverage)).ToArray();
        var undated = AggregateUndated(currency, contributors, partialCoverage);
        var status = CombineStatus(contributors.SelectMany(Statuses).ToArray(), partialCoverage)
            .RaiseForUnknownCurrency(currency);
        var qualityCodes = contributors
            .SelectMany(input => QualityCodes(input.Project.CashFlow, input.Groups, currency))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        return new ProgramPortfolioCashFlowCurrencyGroupDto(
            currency,
            contributors.Length,
            status,
            buckets,
            undated,
            qualityCodes);
    }

    private static ProgramPortfolioCashFlowBucketDto AggregateBucket(
        DateOnly period,
        string? currency,
        ProjectCurrencyInput[] contributors,
        bool partialCoverage)
    {
        var items = contributors
            .SelectMany(input => input.Groups.SelectMany(group => group.Buckets
                .Where(bucket => bucket.PeriodStart == period)
                .SelectMany(bucket => bucket.Items.Select(line => Wrap(input.Project, line)))))
            .DistinctBy(ItemIdentity)
            .OrderBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line.EventDate)
            .ThenBy(item => item.Line.ExpectedDate)
            .ThenBy(item => item.Line.SourceId)
            .ToArray();

        var status = CombineStatus(contributors.SelectMany(Statuses).ToArray(), partialCoverage)
            .RaiseForUnknownCurrency(currency);
        var qualityCodes = contributors
            .SelectMany(input => QualityCodesForBucket(input, currency, period))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        return new ProgramPortfolioCashFlowBucketDto(
            period,
            SumMetric(currency, contributors, period, static bucket => bucket.PlannedResidual),
            SumMetric(currency, contributors, period, static bucket => bucket.CommittedUnpaid),
            SumMetric(currency, contributors, period, static bucket => bucket.ActualGross),
            SumMetric(currency, contributors, period, static bucket => bucket.ExecutedAdjustments),
            SumMetric(currency, contributors, period, static bucket => bucket.ActualNet),
            status,
            qualityCodes,
            items);
    }

    private static ProgramPortfolioCashFlowUndatedDto AggregateUndated(
        string? currency,
        ProjectCurrencyInput[] contributors,
        bool partialCoverage)
    {
        var items = contributors
            .SelectMany(input => input.Groups.SelectMany(group => group.Undated.Items.Select(line => Wrap(input.Project, line))))
            .DistinctBy(ItemIdentity)
            .OrderBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line.ExpectedDate)
            .ThenBy(item => item.Line.SourceId)
            .ToArray();

        var status = CombineStatus(contributors.SelectMany(Statuses).ToArray(), partialCoverage)
            .RaiseForUnknownCurrency(currency);
        var qualityCodes = contributors
            .SelectMany(input => QualityCodes(input.Project.CashFlow, input.Groups, currency))
            .Concat(items.SelectMany(item => item.Line.QualityCodes))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        return new ProgramPortfolioCashFlowUndatedDto(
            SumUndatedMetric(currency, contributors, static totals => totals.PlannedResidual),
            SumUndatedMetric(currency, contributors, static totals => totals.CommittedUnpaid),
            SumUndatedMetric(currency, contributors, static totals => totals.ActualGross),
            SumUndatedMetric(currency, contributors, static totals => totals.ExecutedAdjustments),
            SumUndatedMetric(currency, contributors, static totals => totals.ActualNet),
            status,
            qualityCodes,
            items);
    }

    private static decimal? SumMetric(
        string? currency,
        IReadOnlyList<ProjectCurrencyInput> contributors,
        DateOnly period,
        Func<CashFlowBucketDto, decimal?> select)
    {
        if (currency is null || contributors.Count == 0) return null;
        decimal sum = 0m;
        foreach (var input in contributors)
        {
            if (!IsNumericallyComplete(input)) return null;

            var matching = input.Groups.SelectMany(group => group.Buckets)
                .Where(bucket => bucket.PeriodStart == period)
                .ToArray();
            if (matching.Length == 0) continue;
            if (matching.Length != 1) return null;
            var value = select(matching[0]);
            if (!value.HasValue) return null;
            sum += value.Value;
        }
        return sum;
    }

    private static decimal? SumUndatedMetric(
        string? currency,
        IReadOnlyList<ProjectCurrencyInput> contributors,
        Func<CashFlowUndatedTotalsDto, decimal?> select)
    {
        if (currency is null || contributors.Count == 0) return null;
        decimal sum = 0m;
        foreach (var input in contributors)
        {
            if (!IsNumericallyComplete(input)) return null;

            var values = input.Groups.Select(group => select(group.Undated)).ToArray();
            if (values.Length == 0) continue;
            if (values.Any(value => !value.HasValue)) return null;
            sum += values.Sum(value => value!.Value);
        }
        return sum;
    }

    private static bool IsNumericallyComplete(ProjectCurrencyInput input) =>
        input.Project.CashFlow.DataQualityStatus == CashFlowDataQualityStatus.Available
        && input.Groups.All(group => group.DataQualityStatus == CashFlowDataQualityStatus.Available);

    private static IEnumerable<CashFlowDataQualityStatus> Statuses(ProjectCurrencyInput input)
    {
        yield return input.Project.CashFlow.DataQualityStatus;
        foreach (var group in input.Groups)
        {
            yield return group.DataQualityStatus;
        }
    }

    private static IEnumerable<string> QualityCodes(
        ProjectCashFlowDto cashFlow,
        IReadOnlyList<ProjectCashFlowCurrencyGroupDto> groups,
        string? currency)
    {
        var codes = cashFlow.DataQualityIssues
            .Where(issue => currency is null || issue.Currency is null || NormalizeCurrency(issue.Currency) == currency)
            .Select(issue => issue.Code)
            .Concat(groups.SelectMany(group => group.Buckets.SelectMany(bucket => bucket.Items)
                .Concat(group.Undated.Items).SelectMany(line => line.QualityCodes)));
        return codes;
    }

    private static IEnumerable<string> QualityCodesForBucket(ProjectCurrencyInput input, string? currency, DateOnly period)
    {
        var issueCodes = input.Project.CashFlow.DataQualityIssues
            .Where(issue => currency is null || issue.Currency is null || NormalizeCurrency(issue.Currency) == currency)
            .Select(issue => issue.Code);
        var itemCodes = input.Groups
            .SelectMany(group => group.Buckets.Where(bucket => bucket.PeriodStart == period))
            .SelectMany(bucket => bucket.Items)
            .SelectMany(line => line.QualityCodes);
        return issueCodes.Concat(itemCodes);
    }

    private static ProgramPortfolioCashFlowItemDto Wrap(
        ProgramPortfolioCashFlowProjectContributionDto project,
        CashFlowLineDto line) => new(
        project.ProjectId,
        project.ProjectPublicId,
        project.ProjectName,
        project.ProgramId,
        project.ProgramName,
        project.IsDirectProject,
        line);

    private static CashFlowDataQualityStatus CombineStatus(
        IReadOnlyList<CashFlowDataQualityStatus> statuses,
        bool partialCoverage)
    {
        if (statuses.Count == 0) return partialCoverage ? CashFlowDataQualityStatus.Partial : CashFlowDataQualityStatus.Available;
        if (statuses.All(status => status == CashFlowDataQualityStatus.Unavailable)) return CashFlowDataQualityStatus.Unavailable;
        if (partialCoverage || statuses.Any(status => status != CashFlowDataQualityStatus.Available)) return CashFlowDataQualityStatus.Partial;
        return CashFlowDataQualityStatus.Available;
    }

    private static CashFlowDataQualityStatus RaiseForUnknownCurrency(
        this CashFlowDataQualityStatus status,
        string? currency) =>
        currency is null && status == CashFlowDataQualityStatus.Available
            ? CashFlowDataQualityStatus.Partial
            : status;

    private static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length != 3
            || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            return null;
        }

        return normalized;
    }

    private static string IssueIdentity(ProgramPortfolioCashFlowQualityIssueDto issue) =>
        $"{issue.ProjectId}|{issue.Issue.Code}|{issue.Issue.SourceType}|{issue.Issue.SourceId}|{issue.Issue.InstallmentId}|{issue.Issue.PaymentRequestId}|{issue.Issue.PaymentObligationId}|{issue.Issue.Currency}";

    private static string ItemIdentity(ProgramPortfolioCashFlowItemDto item) =>
        $"{item.ProjectId}|{item.Line.Measure}|{item.Line.SourceType}|{item.Line.SourceId}|{item.Line.InstallmentId}|{item.Line.PaymentRequestId}|{item.Line.PaymentObligationId}|{item.Line.OriginalSettlementId}|{item.Line.EventDate}|{item.Line.ExpectedDate}";

    private sealed record ProjectCurrencyInput(
        ProgramPortfolioCashFlowProjectContributionDto Project,
        string? Currency,
        IReadOnlyList<ProjectCashFlowCurrencyGroupDto> Groups);
}
