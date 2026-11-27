using NEXUS.Api.ProgramManagement;
using NEXUS.Modules.CostManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests;

public sealed class ProgramPortfolioCashFlowAggregationTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 12, 31);

    [Fact]
    public void Aggregate_DeduplicatesProjectsAndKeepsCurrencyTotalsSeparate()
    {
        var usd = Guid.NewGuid();
        var eur = Guid.NewGuid();
        var scope = new ProgramPortfolioCashFlowProjectScopeDto(Guid.NewGuid(), false,
        [
            Membership(usd, "USD"),
            Membership(usd, "USD"),
            Membership(eur, "EUR")
        ]);
        var reports = new Dictionary<Guid, ProjectCashFlowDto>
        {
            [usd] = Report(usd, "USD", CashFlowDataQualityStatus.Available, planned: 100m),
            [eur] = Report(eur, "EUR", CashFlowDataQualityStatus.Available, planned: 250m)
        };

        var result = ProgramPortfolioCashFlowAggregation.Aggregate(
            "Portfolio", Guid.NewGuid(), From, To, CashFlowGranularity.Month, scope, reports);

        Assert.Equal(2, result.IncludedProjectCount);
        Assert.True(result.HasMixedCurrencies);
        Assert.Collection(result.CurrencyGroups.OrderBy(group => group.Currency),
            group =>
            {
                Assert.Equal("EUR", group.Currency);
                Assert.Equal(1, group.ContributingProjectCount);
                Assert.Equal(250m, Assert.Single(group.Buckets).PlannedResidual);
            },
            group =>
            {
                Assert.Equal("USD", group.Currency);
                Assert.Equal(1, group.ContributingProjectCount);
                Assert.Equal(100m, Assert.Single(group.Buckets).PlannedResidual);
            });
    }

    [Fact]
    public void Aggregate_ReturnsNullWhenAnyIncludedContributionIsPartial()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var scope = new ProgramPortfolioCashFlowProjectScopeDto(Guid.NewGuid(), false,
        [Membership(first, "USD"), Membership(second, "USD")]);
        var reports = new Dictionary<Guid, ProjectCashFlowDto>
        {
            [first] = Report(first, "USD", CashFlowDataQualityStatus.Available, planned: 100m),
            [second] = Report(second, "USD", CashFlowDataQualityStatus.Partial, planned: 75m)
        };

        var result = ProgramPortfolioCashFlowAggregation.Aggregate(
            "Program", Guid.NewGuid(), From, To, CashFlowGranularity.Month, scope, reports);

        var group = Assert.Single(result.CurrencyGroups);
        var bucket = Assert.Single(group.Buckets);
        Assert.Equal(CashFlowDataQualityStatus.Partial, result.DataQualityStatus);
        Assert.Equal(CashFlowDataQualityStatus.Partial, group.DataQualityStatus);
        Assert.Null(bucket.PlannedResidual);
        Assert.Equal(2, group.ContributingProjectCount);
        Assert.Equal(2, result.Projects.Count);
    }

    [Fact]
    public void Aggregate_PreservesUndatedAdjustmentsAndMarksMissingProjectResponse()
    {
        var withData = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var scope = new ProgramPortfolioCashFlowProjectScopeDto(Guid.NewGuid(), false,
        [Membership(withData, "USD"), Membership(missing, "USD")]);
        var report = Report(withData, "USD", CashFlowDataQualityStatus.Available, undatedAdjustment: -12m);

        var result = ProgramPortfolioCashFlowAggregation.Aggregate(
            "Portfolio", Guid.NewGuid(), From, To, CashFlowGranularity.Month, scope,
            new Dictionary<Guid, ProjectCashFlowDto> { [withData] = report });

        var group = Assert.Single(result.CurrencyGroups);
        Assert.Equal(CashFlowDataQualityStatus.Partial, result.DataQualityStatus);
        Assert.Equal(CashFlowDataQualityStatus.Partial, group.DataQualityStatus);
        Assert.Null(group.Undated.ExecutedAdjustments);
        Assert.Contains(group.Undated.Items, item => item.ProjectId == withData && item.Line.EventDate is null);
        Assert.Contains(result.DataQualityIssues, item => item.ProjectId == missing
            && item.Issue.Code == ProgramPortfolioCashFlowAggregation.MissingProjectResponseCode);
        Assert.Equal(1, result.ProjectsWithUnavailableData);
    }

    private static ProgramPortfolioCashFlowProjectMembershipDto Membership(Guid projectId, string? currency) => new(
        projectId,
        Guid.NewGuid(),
        $"Project {projectId:N}",
        currency,
        null,
        null,
        true);

    private static ProjectCashFlowDto Report(
        Guid projectId,
        string? currency,
        CashFlowDataQualityStatus status,
        decimal? planned = null,
        decimal? undatedAdjustment = null)
    {
        var bucket = planned.HasValue
            ? new[] { new CashFlowBucketDto(From, planned, 0m, 0m, 0m, 0m, [Line(CashFlowMeasure.PlannedResidual, planned.Value)]) }
            : Array.Empty<CashFlowBucketDto>();
        return new ProjectCashFlowDto(
            projectId,
            From,
            To,
            CashFlowGranularity.Month,
            status,
            Array.Empty<CashFlowQualityIssueDto>(),
            [new ProjectCashFlowCurrencyGroupDto(
                currency,
                status,
                bucket,
                new CashFlowUndatedTotalsDto(
                    0m,
                    0m,
                    0m,
                    undatedAdjustment,
                    undatedAdjustment,
                    undatedAdjustment.HasValue ? [Line(CashFlowMeasure.ExecutedAdjustment, undatedAdjustment.Value)] : Array.Empty<CashFlowLineDto>()))]);
    }

    private static CashFlowLineDto Line(CashFlowMeasure measure, decimal amount) => new(
        measure,
        measure == CashFlowMeasure.ExecutedAdjustment ? CashFlowSourceTypes.PaymentSettlementAdjustment : CashFlowSourceTypes.PlannedInstallment,
        Guid.NewGuid(),
        null,
        null,
        null,
        null,
        "Cash flow item",
        "Confirmed",
        measure == CashFlowMeasure.ExecutedAdjustment ? null : From,
        null,
        null,
        amount,
        "USD",
        Array.Empty<string>());
}
