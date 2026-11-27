using System.Reflection;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectStatusReportCurrencyQualityTests
{
    [Theory]
    [InlineData(true, false, false, "ProjectCurrencyUnavailable")]
    [InlineData(false, true, false, "UnresolvedCurrency")]
    [InlineData(false, false, true, "CurrencyMismatch")]
    [InlineData(false, false, false, "Available")]
    public void Currency_quality_classification_is_explicit(
        bool projectCurrencyUnavailable, bool unresolved, bool foreign, string expected)
    {
        var classify = typeof(ProjectStatusReportService).GetMethod(
            "ClassifyActualPaidDataQuality", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(classify);
        Assert.Equal(expected, classify!.Invoke(null, [projectCurrencyUnavailable, unresolved, foreign]));
    }

    [Fact]
    public void Currency_quality_is_persisted_in_snapshot_metadata_and_mapped_without_inventing_an_amount()
    {
        var addMetadata = typeof(ProjectStatusReportService).GetMethod(
            "AddActualPaidDataQualitySnapshot", BindingFlags.NonPublic | BindingFlags.Static);
        var mapDetail = typeof(ProjectStatusReportService).GetMethod(
            "MapDetail", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(addMetadata);
        Assert.NotNull(mapDetail);
        var performanceJson = (string?)addMetadata!.Invoke(null, ["{}", "CurrencyMismatch"]);
        var report = new ProjectStatusReport
        {
            PerformanceSnapshotJson = performanceJson,
            ActualPaidSnapshot = null
        };

        var dto = Assert.IsType<ProjectStatusReportDto>(mapDetail!.Invoke(null, [report]));

        Assert.Null(dto.ActualPaidSnapshot);
        Assert.Equal("CurrencyMismatch", dto.ActualPaidDataQualitySnapshot);
    }

    [Fact]
    public void Legacy_report_without_currency_quality_metadata_is_marked_not_recorded()
    {
        var mapDetail = typeof(ProjectStatusReportService).GetMethod(
            "MapDetail", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(mapDetail);
        var dto = Assert.IsType<ProjectStatusReportDto>(mapDetail!.Invoke(null, [new ProjectStatusReport
        {
            ActualPaidSnapshot = null
        }]));

        Assert.Null(dto.ActualPaidSnapshot);
        Assert.Equal("NotRecorded", dto.ActualPaidDataQualitySnapshot);
    }

    [Fact]
    public void Planned_payment_quality_is_persisted_and_legacy_snapshot_is_not_assumed_complete()
    {
        var addMetadata = typeof(ProjectStatusReportService).GetMethod(
            "AddPlannedPaymentDataQualitySnapshot", BindingFlags.NonPublic | BindingFlags.Static);
        var mapDetail = typeof(ProjectStatusReportService).GetMethod(
            "MapDetail", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(addMetadata);
        Assert.NotNull(mapDetail);

        var metadata = (string?)addMetadata!.Invoke(null, ["{}", "CurrencyMismatch"]);
        var report = new ProjectStatusReport { PerformanceSnapshotJson = metadata, OutstandingPlannedPaymentsSnapshot = null };
        var dto = Assert.IsType<ProjectStatusReportDto>(mapDetail!.Invoke(null, [report]));
        Assert.Null(dto.OutstandingPlannedPaymentsSnapshot);
        Assert.Equal("CurrencyMismatch", dto.PlannedPaymentsDataQualitySnapshot);

        var legacy = Assert.IsType<ProjectStatusReportDto>(mapDetail.Invoke(null, [new ProjectStatusReport()]));
        Assert.Equal("NotRecorded", legacy.PlannedPaymentsDataQualitySnapshot);
    }
}
