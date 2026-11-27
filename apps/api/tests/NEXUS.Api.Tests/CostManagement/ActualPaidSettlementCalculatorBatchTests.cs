using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Domain;
using Xunit;

namespace NEXUS.Api.Tests.CostManagement;

public sealed class ActualPaidSettlementCalculatorBatchTests
{
    [Fact]
    public async Task Batch_loader_deduplicates_projects_and_matches_single_project_results_across_both_owner_rails()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectOneId = Guid.NewGuid();
        var projectTwoId = Guid.NewGuid();
        var outsideProjectId = Guid.NewGuid();

        var projectOneObligationId = Guid.NewGuid();
        var projectTwoObligationId = Guid.NewGuid();
        var otherTenantObligationId = Guid.NewGuid();
        var projectOneRequestId = Guid.NewGuid();
        var projectTwoRequestId = Guid.NewGuid();
        var otherTenantRequestId = Guid.NewGuid();

        var projectOneObligationSettlementId = Guid.NewGuid();
        var projectOneRequestSettlementId = Guid.NewGuid();
        var projectTwoObligationSettlementId = Guid.NewGuid();
        var projectTwoRequestSettlementId = Guid.NewGuid();
        var otherTenantSettlementId = Guid.NewGuid();
        var outsideProjectSettlementId = Guid.NewGuid();
        var ambiguousOwnerSettlementId = Guid.NewGuid();
        var missingOwnerSettlementId = Guid.NewGuid();

        await using var connection = new SqliteConnection(
            $"Data Source=file:cash-flow-batch-{Guid.NewGuid():N}?mode=memory&cache=shared;Default Timeout=30");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BatchSettlementTestContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new BatchSettlementTestContext(options);
        await db.Database.EnsureCreatedAsync();

        db.PaymentObligations.AddRange(
            Obligation(projectOneObligationId, tenantId, projectOneId, "USD"),
            Obligation(projectTwoObligationId, tenantId, projectTwoId, "EUR"),
            Obligation(otherTenantObligationId, otherTenantId, projectOneId, "USD"));
        db.ProjectPaymentRequestDetails.AddRange(
            RequestDetail(projectOneRequestId, tenantId, projectOneId),
            RequestDetail(projectTwoRequestId, tenantId, projectTwoId),
            RequestDetail(otherTenantRequestId, otherTenantId, projectOneId));

        db.PaymentSettlements.AddRange(
            Settlement(projectOneObligationSettlementId, tenantId, projectOneId, 100m,
                obligationId: projectOneObligationId, currency: null, settlementDate: new DateTime(2026, 1, 2)),
            Settlement(projectOneRequestSettlementId, tenantId, projectOneId, 50m,
                paymentRequestId: projectOneRequestId, installmentId: Guid.NewGuid(), currency: "USD",
                settlementDate: new DateTime(2026, 1, 3)),
            Settlement(projectTwoObligationSettlementId, tenantId, projectTwoId, 200m,
                obligationId: projectTwoObligationId, currency: "EUR", settlementDate: new DateTime(2026, 1, 4)),
            Settlement(projectTwoRequestSettlementId, tenantId, projectTwoId, 75m,
                paymentRequestId: projectTwoRequestId, installmentId: Guid.NewGuid(), currency: "EUR",
                settlementDate: new DateTime(2026, 1, 5)),
            Settlement(otherTenantSettlementId, otherTenantId, projectOneId, 900m,
                obligationId: otherTenantObligationId, currency: "USD", settlementDate: new DateTime(2026, 1, 6)),
            Settlement(outsideProjectSettlementId, tenantId, outsideProjectId, 800m,
                obligationId: Guid.NewGuid(), currency: "USD", settlementDate: new DateTime(2026, 1, 7)),
            Settlement(ambiguousOwnerSettlementId, tenantId, projectOneId, 700m,
                obligationId: projectOneObligationId, paymentRequestId: projectOneRequestId,
                currency: "USD", settlementDate: new DateTime(2026, 1, 8)),
            Settlement(missingOwnerSettlementId, tenantId, projectTwoId, 600m,
                currency: "USD", settlementDate: new DateTime(2026, 1, 9)));

        await db.SaveChangesAsync();

        var batch = await ActualPaidSettlementCalculator.LoadForProjectsAsync(
            db,
            tenantId,
            [projectOneId, projectTwoId, projectOneId],
            cancellationToken: CancellationToken.None);

        Assert.Equal(4, batch.Count);
        Assert.Equal(2, batch.Count(fact => fact.ProjectId == projectOneId));
        Assert.Equal(2, batch.Count(fact => fact.ProjectId == projectTwoId));
        Assert.DoesNotContain(batch, fact => fact.ProjectId == outsideProjectId);
        Assert.DoesNotContain(batch, fact => fact.Id == otherTenantSettlementId);
        Assert.DoesNotContain(batch, fact => fact.Id == ambiguousOwnerSettlementId);
        Assert.DoesNotContain(batch, fact => fact.Id == missingOwnerSettlementId);

        var projectOne = batch.Where(fact => fact.ProjectId == projectOneId).ToArray();
        var projectTwo = batch.Where(fact => fact.ProjectId == projectTwoId).ToArray();
        Assert.Equal(150m, projectOne.Sum(fact => fact.Amount));
        Assert.Equal(275m, projectTwo.Sum(fact => fact.Amount));
        Assert.Equal("USD", projectOne.Single(fact => fact.Id == projectOneObligationSettlementId).Currency);
        Assert.Equal(projectOneRequestId,
            projectOne.Single(fact => fact.Id == projectOneRequestSettlementId).ProjectPaymentRequestId);
        Assert.Equal(projectOneObligationId,
            projectOne.Single(fact => fact.Id == projectOneObligationSettlementId).PaymentObligationId);

        var singleProjectOne = await ActualPaidSettlementCalculator.LoadForProjectAsync(
            db, tenantId, projectOneId, cancellationToken: CancellationToken.None);
        var singleProjectTwo = await ActualPaidSettlementCalculator.LoadForProjectAsync(
            db, tenantId, projectTwoId, cancellationToken: CancellationToken.None);

        Assert.Equal(
            singleProjectOne.OrderBy(fact => fact.Id).ToArray(),
            projectOne.OrderBy(fact => fact.Id).ToArray());
        Assert.Equal(
            singleProjectTwo.OrderBy(fact => fact.Id).ToArray(),
            projectTwo.OrderBy(fact => fact.Id).ToArray());
    }

    private static PaymentObligation Obligation(Guid id, Guid tenantId, Guid projectId, string currency) => new()
    {
        Id = id,
        TenantId = tenantId,
        ProjectId = projectId,
        Currency = currency,
        Amount = 1_000m,
        DueDate = new DateTime(2026, 1, 1)
    };

    private static ProjectPaymentRequestDetail RequestDetail(Guid requestId, Guid tenantId, Guid projectId) => new()
    {
        ApprovalRequestId = requestId,
        TenantId = tenantId,
        ProjectId = projectId,
        RequestKind = ProjectPaymentRequestKinds.PaymentRequest
    };

    private static PaymentSettlement Settlement(
        Guid id,
        Guid tenantId,
        Guid projectId,
        decimal amount,
        Guid? obligationId = null,
        Guid? paymentRequestId = null,
        Guid? installmentId = null,
        string? currency = null,
        DateTime? settlementDate = null) => new()
    {
        Id = id,
        TenantId = tenantId,
        ProjectId = projectId,
        PaymentObligationId = obligationId,
        ProjectPaymentRequestId = paymentRequestId,
        PlannedPaymentInstallmentId = installmentId,
        Amount = amount,
        Currency = currency,
        SettlementDate = settlementDate ?? new DateTime(2026, 1, 1),
        IdempotencyKey = Guid.NewGuid(),
        PaymentReference = $"Settlement-{id:N}"
    };

    private sealed class BatchSettlementTestContext(DbContextOptions<BatchSettlementTestContext> options)
        : DbContext(options)
    {
        public DbSet<PaymentObligation> PaymentObligations => Set<PaymentObligation>();
        public DbSet<PaymentSettlement> PaymentSettlements => Set<PaymentSettlement>();
        public DbSet<ProjectPaymentRequestDetail> ProjectPaymentRequestDetails => Set<ProjectPaymentRequestDetail>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PaymentObligation>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Ignore(item => item.Settlements);
            });

            modelBuilder.Entity<ProjectPaymentRequestDetail>(entity =>
            {
                entity.HasKey(item => item.ApprovalRequestId);
                entity.Ignore(item => item.ReversesRequest);
                entity.Ignore(item => item.Settlements);
            });

            modelBuilder.Entity<PaymentSettlement>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Ignore(item => item.PaymentObligation);
                entity.Ignore(item => item.ProjectPaymentRequestDetail);
                entity.Ignore(item => item.PlannedPaymentInstallment);
                entity.Ignore(item => item.FundingTransfer);
            });
        }
    }
}
