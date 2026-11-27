using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.CostManagement;

/// <summary>
/// Regression tests for financial decimal precision mapping (Sprint 1 / Task 01).
/// Verifies monetary budget-closing allocations retain decimal(19,4), while percentage
/// allocation properties retain decimal(7,4).
/// </summary>
public sealed class FinancialDecimalPrecisionMappingTests : IDisposable
{
    private readonly AppDbContext _context;

    public FinancialDecimalPrecisionMappingTests()
    {
        // These assertions inspect EF model metadata only. Use the production provider's
        // model conventions without creating tables or opening a SQL Server connection.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=FinancialDecimalPrecisionModel;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        _context = new AppDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ProjectAnnualBudgetClosing_AllocationProperties_HavePrecision19Scale4()
    {
        var closingEntity = _context.Model.FindEntityType(typeof(ProjectAnnualBudgetClosing));
        Assert.NotNull(closingEntity);

        foreach (var propertyName in new[]
        {
            nameof(ProjectAnnualBudgetClosing.ApprovedPaymentRequestAllocations),
            nameof(ProjectAnnualBudgetClosing.EffectiveActiveAllocations),
        })
        {
            var property = closingEntity!.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(19, property!.GetPrecision());
            Assert.Equal(4, property!.GetScale());
        }
    }

    [Fact]
    public void ProjectAnnualBudgetClosing_AllMonetaryProperties_HavePrecision19Scale4()
    {
        var closingEntity = _context.Model.FindEntityType(typeof(ProjectAnnualBudgetClosing));
        Assert.NotNull(closingEntity);

        foreach (var propertyName in new[]
        {
            nameof(ProjectAnnualBudgetClosing.ApprovedAnnualBudget),
            nameof(ProjectAnnualBudgetClosing.NewFunding),
            nameof(ProjectAnnualBudgetClosing.IncomingCarryForward),
            nameof(ProjectAnnualBudgetClosing.ApprovedAdjustments),
            nameof(ProjectAnnualBudgetClosing.ApprovedPaymentRequestAllocations),
            nameof(ProjectAnnualBudgetClosing.ApprovedReversalReleases),
            nameof(ProjectAnnualBudgetClosing.EffectiveActiveAllocations),
            nameof(ProjectAnnualBudgetClosing.ActualSettlementsInFiscalYear),
            nameof(ProjectAnnualBudgetClosing.OutstandingCommitments),
            nameof(ProjectAnnualBudgetClosing.FreeAvailableBudget),
            nameof(ProjectAnnualBudgetClosing.TotalUnspentBudget),
            nameof(ProjectAnnualBudgetClosing.ProposedFreeCarryForward),
            nameof(ProjectAnnualBudgetClosing.ApprovedFreeCarryForward),
            nameof(ProjectAnnualBudgetClosing.ApprovedReservedCarryForward),
            nameof(ProjectAnnualBudgetClosing.NonCarriedFreeBalance),
            nameof(ProjectAnnualBudgetClosing.NextFiscalYearNewFunding),
            nameof(ProjectAnnualBudgetClosing.NextFiscalYearAdjustments),
        })
        {
            var property = closingEntity!.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(19, property!.GetPrecision());
            Assert.Equal(4, property!.GetScale());
        }
    }

    [Fact]
    public void PercentageAllocationProperties_RemainPrecision7Scale4()
    {
        AssertPrecision7Scale4(typeof(PlanAssignment), nameof(PlanAssignment.AllocationPct));
        AssertPrecision7Scale4(typeof(ResourceRequest), nameof(ResourceRequest.RequiredAllocation));
    }

    private void AssertPrecision7Scale4(Type entityType, string propertyName)
    {
        var property = _context.Model.FindEntityType(entityType)?.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(7, property!.GetPrecision());
        Assert.Equal(4, property.GetScale());
    }
}
