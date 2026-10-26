using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests;

public class PaymentObligationLifecycleTests
{
    [Fact]
    public async Task CreateAsync_StoresPaymentObligationAndTracksOutstandingBalance()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        var auditService = new Mock<IAuditService>();
        var service = CreateService(context, tenantId, projectId, userId, auditService.Object);

        var result = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "INV-001",
            Purpose = "Phase 1 milestone",
            Description = "Planned milestone payment",
            Amount = 500m,
            Currency = "USD",
            DueDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
        }, userId.ToString());

        Assert.True(result.IsSuccess);
        var obligation = await context.Set<PaymentObligation>().SingleAsync();
        Assert.Equal(500m, obligation.Amount);
        Assert.Equal(500m, obligation.OutstandingAmount);
        Assert.Equal("Not Paid", obligation.SettlementProgressStatus);
        Assert.Equal("INV-001", obligation.PaymentReference);

        var invalidPrecision = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "INV-PRECISION",
            Purpose = "Excess precision",
            Amount = 10.12345m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(1),
        }, userId.ToString());
        Assert.True(invalidPrecision.IsFailure);
        Assert.Equal("VALIDATION_ERROR", invalidPrecision.Error!.Code);
    }

    [Fact]
    public async Task RegisterSettlementAsync_WhenAmountExceedsOutstanding_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        var auditService = new Mock<IAuditService>();
        var service = CreateService(context, tenantId, projectId, userId, auditService.Object);
        var created = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "INV-002",
            Purpose = "Retainer",
            Amount = 400m,
            Currency = "USD",
            DueDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        }, userId.ToString());

        var settlement = await service.RegisterSettlementAsync(created.Value!.Id, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 999m,
            SettlementDate = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PaymentReference = "SETT-OVER",
        }, userId.ToString());

        Assert.True(settlement.IsFailure);
        Assert.Equal("VALIDATION_ERROR", settlement.Error!.Code);
    }

    [Fact]
    public async Task RegisterSettlementAsync_PartialSettlement_UpdatesOutstandingAmount()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        var auditService = new Mock<IAuditService>();
        var service = CreateService(context, tenantId, projectId, userId, auditService.Object);
        var created = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "INV-003",
            Purpose = "Milestone 2",
            Amount = 600m,
            Currency = "USD",
            DueDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        }, userId.ToString());

        var settlement = await service.RegisterSettlementAsync(created.Value!.Id, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 140m,
            SettlementDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            PaymentReference = "SETT-140",
        }, userId.ToString());

        Assert.True(settlement.IsSuccess);
        var updated = await context.Set<PaymentObligation>().SingleAsync();
        Assert.Equal(140m, updated.SettledAmount);
        Assert.Equal(460m, updated.OutstandingAmount);
        Assert.Equal("Partially Paid", updated.SettlementProgressStatus);
    }

    [Fact]
    public async Task SendDueNotificationsAsync_CreatesOnlyOneDueNotificationPerObligation()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        context.Set<PaymentObligation>().Add(new PaymentObligation
        {
            TenantId = tenantId,
            CreatedBy = userId.ToString(),
            ProjectId = projectId,
            ProjectName = "Sample project",
            MilestoneId = milestoneId,
            PaymentReference = "INV-OVERDUE",
            Purpose = "Late payment",
            Amount = 250m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(-2),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await context.SaveChangesAsync();

        AppDbContext.ConfigureTenantResolution(CreateHttpContextAccessor(tenantId, userId));

        var financePermission = new Permission { TenantId = tenantId, Name = "finances.read", Resource = "finances", Action = "read", CreatedBy = "test" };
        var financeRole = new Role { TenantId = tenantId, Name = "Finance Viewer", CreatedBy = "test" };
        financeRole.Permissions.Add(financePermission);
        context.Set<User>().Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            FirstName = "Finance",
            LastName = "User",
            Email = "finance@example.test",
            CreatedBy = "test",
            UserRoles = { new UserRole { TenantId = tenantId, Role = financeRole, CreatedBy = "test" } },
        });
        await context.SaveChangesAsync();

        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(item => item.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns<Notification, CancellationToken>(async (notification, cancellationToken) =>
            {
                context.Set<Notification>().Add(notification);
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            });
        var milestoneLookup = new Mock<IProjectMilestoneReferenceLookup>();
        milestoneLookup.Setup(item => item.FindActiveMilestoneNameForProjectAsync(projectId, milestoneId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Phase 1");
        var dueService = new PaymentDueNotificationService(context, notificationService.Object, new UserEffectivePermissionService(context), milestoneLookup.Object);
        var created = await dueService.SendDueNotificationsAsync(DateTime.UtcNow);

        Assert.Equal(1, created);
        var notification = await context.Set<Notification>().SingleAsync();
        Assert.Equal(NotificationTypes.PaymentOverdue, notification.Type);
        Assert.Equal(nameof(PaymentObligation), notification.ReferenceType);

        Assert.Contains("Sample project", notification.Body);
        Assert.Contains("INV-OVERDUE", notification.Body);
        Assert.Contains("Milestone: Phase 1", notification.Body);

        var secondRun = await dueService.SendDueNotificationsAsync(DateTime.UtcNow);
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task CreateUpdateListAndMilestoneValidation_UseOnlyActiveProjectMilestones()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        var auditService = new Mock<IAuditService>();
        var service = CreateService(context, tenantId, projectId, userId, auditService.Object, milestoneId);
        var created = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            MilestoneId = milestoneId,
            PaymentReference = "INV-CRUD",
            Purpose = "Milestone delivery",
            Amount = 100m,
            Currency = "USD",
            DueDate = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
        }, userId.ToString());

        Assert.True(created.IsSuccess);
        var updated = await service.UpdateAsync(created.Value!.Id, new UpdatePaymentObligationDto
        {
            ProjectId = projectId,
            MilestoneId = milestoneId,
            PaymentReference = "INV-CRUD",
            Purpose = "Updated purpose",
            Amount = 120m,
            Currency = "USD",
            DueDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            RowVersion = created.Value.RowVersion,
        }, userId.ToString());
        Assert.True(updated.IsSuccess);
        Assert.Equal("Updated purpose", updated.Value!.Purpose);
        Assert.Equal(milestoneId, updated.Value.MilestoneId);

        var page = await service.GetPagedAsync(1, 10, projectId);
        Assert.Single(page.Items);
        Assert.Equal(120m, page.Items[0].Amount);

        var deleted = await service.DeleteAsync(created.Value.Id, updated.Value.RowVersion, userId.ToString());
        Assert.True(deleted.IsSuccess);
        Assert.Empty((await service.GetPagedAsync(1, 10, projectId)).Items);

        var invalidMilestoneService = CreateService(context, tenantId, projectId, userId, auditService.Object, Guid.NewGuid());
        var invalid = await invalidMilestoneService.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            MilestoneId = Guid.NewGuid(),
            PaymentReference = "INV-BAD-MILESTONE",
            Purpose = "Invalid milestone",
            Amount = 1m,
            DueDate = DateTime.UtcNow,
        }, userId.ToString());
        Assert.True(invalid.IsFailure);
        Assert.Equal("VALIDATION_ERROR", invalid.Error!.Code);
    }

    [Fact]
    public async Task RegisterSettlementAsync_MultipleAndIdempotentSettlementsUpdateOutstandingBalance()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = new AppDbContext(CreateOptions());
        var service = CreateService(context, tenantId, projectId, userId, new Mock<IAuditService>().Object);
        var created = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "INV-MULTI",
            Purpose = "Split payment",
            Amount = 100m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(1),
        }, userId.ToString());
        Assert.True(created.IsSuccess);

        var first = new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 30m,
            SettlementDate = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            PaymentReference = "TX-30",
        };
        var firstResult = await service.RegisterSettlementAsync(created.Value!.Id, first, userId.ToString());
        var duplicateResult = await service.RegisterSettlementAsync(created.Value.Id, first, userId.ToString());
        Assert.True(firstResult.IsSuccess);
        Assert.True(duplicateResult.IsSuccess);
        Assert.Equal(firstResult.Value!.Id, duplicateResult.Value!.Id);
        var conflictingReplay = await service.RegisterSettlementAsync(created.Value.Id, new RecordPaymentSettlementDto
        {
            IdempotencyKey = first.IdempotencyKey,
            Amount = 31m,
            SettlementDate = first.SettlementDate,
            PaymentReference = first.PaymentReference,
        }, userId.ToString());
        Assert.True(conflictingReplay.IsFailure);
        Assert.Equal("CONFLICT", conflictingReplay.Error!.Code);

        var secondResult = await service.RegisterSettlementAsync(created.Value.Id, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 70m,
            SettlementDate = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
            PaymentReference = "TX-70",
        }, userId.ToString());
        Assert.True(secondResult.IsSuccess);
        var details = await service.GetByIdAsync(created.Value.Id);
        Assert.Equal(0m, details.Value!.OutstandingAmount);
        Assert.Equal("Fully Paid", details.Value.SettlementProgressStatus);
        Assert.Equal("Fully Paid", details.Value.LifecycleStatus);
        Assert.Equal(2, details.Value.Settlements.Count);
    }

    [Fact]
    public async Task ApprovedExample_ThreeSettlementsReachFullyPaidWithoutLosingHistory()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = new AppDbContext(CreateOptions());
        var service = CreateService(context, tenantId, projectId, userId, new Mock<IAuditService>().Object, projectCurrency: "EGP");
        var created = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "EGP-100000",
            Purpose = "Approved lifecycle example",
            Amount = 100_000m,
            Currency = "EGP",
            DueDate = DateTime.UtcNow.AddDays(1),
        }, userId.ToString());
        Assert.True(created.IsSuccess);

        foreach (var (amount, reference) in new[]
                 {
                     (30_000m, "EGP-TX-30"),
                     (50_000m, "EGP-TX-50"),
                     (20_000m, "EGP-TX-20"),
                 })
        {
            var settlement = await service.RegisterSettlementAsync(created.Value!.Id, new RecordPaymentSettlementDto
            {
                IdempotencyKey = Guid.NewGuid(),
                Amount = amount,
                SettlementDate = DateTime.UtcNow,
                PaymentReference = reference,
            }, userId.ToString());
            Assert.True(settlement.IsSuccess);
        }

        var payment = await service.GetByIdAsync(created.Value!.Id);
        Assert.True(payment.IsSuccess);
        Assert.Equal(100_000m, payment.Value!.Amount);
        Assert.Equal(100_000m, payment.Value.SettledAmount);
        Assert.Equal(0m, payment.Value.OutstandingAmount);
        Assert.Equal("Fully Paid", payment.Value.SettlementProgressStatus);
        Assert.Equal(3, payment.Value.Settlements.Count);
    }

    [Fact]
    public void DueDateStatusAndSettlementProgressRemainIndependent()
    {
        var payment = new PaymentObligation
        {
            Amount = 100m,
            DueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Settlements =
            {
                new PaymentSettlement { Amount = 25m },
            },
        };

        Assert.Equal("Partially Paid", payment.SettlementProgressStatus);
        Assert.Equal("Overdue", payment.GetDueDateStatus(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Partially Paid and Overdue", payment.GetLifecycleStatus(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)));

        payment.Settlements.Add(new PaymentSettlement { Amount = 75m });
        Assert.Equal("Fully Paid", payment.SettlementProgressStatus);
        Assert.Equal("Settled", payment.GetDueDateStatus(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task SettlementCannotReferenceMissingPayment()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        context.Set<PaymentSettlement>().Add(new PaymentSettlement
        {
            TenantId = tenantId,
            CreatedBy = userId.ToString(),
            PaymentObligationId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            Amount = 25m,
            SettlementDate = DateTime.UtcNow,
            PaymentReference = "NO-PARENT",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentAndSettlementPersistAcrossDbContextRefresh()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        AppDbContext.ConfigureTenantResolution(CreateHttpContextAccessor(tenantId, userId));

        Guid paymentId;
        await using (var createContext = new AppDbContext(options))
        {
            await createContext.Database.EnsureCreatedAsync();
            var service = CreateService(createContext, tenantId, projectId, userId, new Mock<IAuditService>().Object);
            var created = await service.CreateAsync(new CreatePaymentObligationDto
            {
                ProjectId = projectId,
                PaymentReference = "PERSISTED-PAYMENT",
                Purpose = "Persistence check",
                Amount = 100m,
                Currency = "USD",
                DueDate = DateTime.UtcNow.AddDays(1),
            }, userId.ToString());
            Assert.True(created.IsSuccess);
            paymentId = created.Value!.Id;

            var settlement = await service.RegisterSettlementAsync(paymentId, new RecordPaymentSettlementDto
            {
                IdempotencyKey = Guid.NewGuid(),
                Amount = 40m,
                SettlementDate = DateTime.UtcNow,
                PaymentReference = "PERSISTED-SETTLEMENT",
            }, userId.ToString());
            Assert.True(settlement.IsSuccess);
        }

        await using var refreshedContext = new AppDbContext(options);
        var refreshedService = CreateService(refreshedContext, tenantId, projectId, userId, new Mock<IAuditService>().Object);
        var payment = await refreshedService.GetByIdAsync(paymentId);
        Assert.True(payment.IsSuccess);
        Assert.Equal(100m, payment.Value!.Amount);
        Assert.Equal(40m, payment.Value.SettledAmount);
        Assert.Equal(60m, payment.Value.OutstandingAmount);
        Assert.Contains(payment.Value.Settlements, settlement => settlement.PaymentReference == "PERSISTED-SETTLEMENT");
    }

    [Fact]
    public async Task SettlementCannotReferenceParentInAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        context.Set<PaymentObligation>().Add(new PaymentObligation
        {
            Id = paymentId,
            TenantId = otherTenantId,
            CreatedBy = userId.ToString(),
            ProjectId = Guid.NewGuid(),
            ProjectName = "Other tenant project",
            PaymentReference = "OTHER-TENANT",
            Purpose = "Tenant integrity",
            Amount = 100m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(1),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await context.SaveChangesAsync();
        context.Set<PaymentSettlement>().Add(new PaymentSettlement
        {
            TenantId = tenantId,
            CreatedBy = userId.ToString(),
            PaymentObligationId = paymentId,
            IdempotencyKey = Guid.NewGuid(),
            Amount = 25m,
            SettlementDate = DateTime.UtcNow,
            PaymentReference = "CROSS-TENANT",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RegisterSettlementAsync_ConcurrentSettlementsCannotOverdrawTheObligation()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var barrier = new SettlementSaveBarrier(2);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(barrier).Options;

        AppDbContext.ConfigureTenantResolution(CreateHttpContextAccessor(tenantId, userId));
        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Set<PaymentObligation>().Add(new PaymentObligation
            {
                Id = paymentId,
                TenantId = tenantId,
                CreatedBy = userId.ToString(),
                ProjectId = projectId,
                ProjectName = "Sample project",
                PaymentReference = "INV-CONCURRENT",
                Purpose = "Concurrent payment",
                Amount = 100m,
                Currency = "USD",
                DueDate = DateTime.UtcNow.AddDays(1),
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var firstService = CreateService(firstContext, tenantId, projectId, userId, new Mock<IAuditService>().Object);
        var secondService = CreateService(secondContext, tenantId, projectId, userId, new Mock<IAuditService>().Object);
        var firstTask = firstService.RegisterSettlementAsync(paymentId, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 75m,
            SettlementDate = DateTime.UtcNow,
            PaymentReference = "TX-FIRST",
        }, userId.ToString());
        var secondTask = secondService.RegisterSettlementAsync(paymentId, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 75m,
            SettlementDate = DateTime.UtcNow,
            PaymentReference = "TX-SECOND",
        }, userId.ToString());

        var results = await Task.WhenAll(firstTask, secondTask);
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure && result.Error!.Code == "CONFLICT");
        await using var verifyContext = new AppDbContext(options);
        var obligation = await verifyContext.Set<PaymentObligation>()
            .Include(item => item.Settlements)
            .SingleAsync(item => item.Id == paymentId);
        Assert.Equal(75m, obligation.SettledAmount);
        Assert.Equal(25m, obligation.OutstandingAmount);
        Assert.Single(obligation.Settlements);
    }

    [Fact]
    public async Task GetPagedAsync_OnlyReturnsTheCurrentTenantPayments()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var context = new AppDbContext(CreateOptions());
        context.Set<PaymentObligation>().AddRange(
            new PaymentObligation
            {
                TenantId = tenantId,
                CreatedBy = userId.ToString(),
                ProjectId = projectId,
                ProjectName = "Current tenant project",
                PaymentReference = "CURRENT-TENANT",
                Purpose = "Current tenant payment",
                Amount = 100m,
                Currency = "USD",
                DueDate = DateTime.UtcNow.AddDays(1),
                RowVersion = Guid.NewGuid().ToByteArray(),
            },
            new PaymentObligation
            {
                TenantId = otherTenantId,
                CreatedBy = userId.ToString(),
                ProjectId = Guid.NewGuid(),
                ProjectName = "Other tenant project",
                PaymentReference = "OTHER-TENANT",
                Purpose = "Other tenant payment",
                Amount = 100m,
                Currency = "USD",
                DueDate = DateTime.UtcNow.AddDays(1),
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId, projectId, userId, new Mock<IAuditService>().Object);

        var page = await service.GetPagedAsync(1, 10);
        Assert.Single(page.Items);
        Assert.Equal("CURRENT-TENANT", page.Items[0].PaymentReference);
    }

    [Fact]
    public async Task CreateAsync_EnforcesProjectScopeAndReadOnlyManagementMode()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = new AppDbContext(CreateOptions());
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(item => item.Current).Returns(new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())));
        var service = CreateService(context, tenantId, projectId, userId, new Mock<IAuditService>().Object, managementContext: management.Object);
        var outOfScope = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "OUT-OF-SCOPE",
            Purpose = "Scope denial",
            Amount = 100m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(1),
        }, userId.ToString());
        Assert.True(outOfScope.IsFailure);
        Assert.Equal("FORBIDDEN", outOfScope.Error!.Code);

        management.Setup(item => item.Current).Returns(new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var readOnly = await service.CreateAsync(new CreatePaymentObligationDto
        {
            ProjectId = projectId,
            PaymentReference = "READ-ONLY",
            Purpose = "Read-only denial",
            Amount = 100m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(1),
        }, userId.ToString());
        Assert.True(readOnly.IsFailure);
        Assert.Equal("FORBIDDEN", readOnly.Error!.Code);
        Assert.Empty(await context.Set<PaymentObligation>().ToListAsync());
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static PaymentObligationService CreateService(
        AppDbContext context,
        Guid tenantId,
        Guid projectId,
        Guid userId,
        IAuditService auditService,
        Guid? validMilestoneId = null,
        string projectCurrency = "USD",
        IManagementContextAccessor? managementContext = null)
    {
        var projectLookup = new Mock<IProjectReferenceLookup>();
        projectLookup.Setup(item => item.FindAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectReference(projectId, "Sample project", projectCurrency));
        var milestoneLookup = new Mock<IProjectMilestoneReferenceLookup>();
        milestoneLookup.Setup(item => item.IsActiveMilestoneForProjectAsync(projectId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid milestoneId, CancellationToken _) => validMilestoneId == milestoneId);
        var accessor = CreateHttpContextAccessor(tenantId, userId);
        return new PaymentObligationService(context, auditService, projectLookup.Object, milestoneLookup.Object, accessor, managementContext);
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(Guid tenantId, Guid userId)
    {
        var claims = new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
            }
        };

        AppDbContext.ConfigureTenantResolution(accessor);
        return accessor;
    }

    private sealed class SettlementSaveBarrier(int participantCount) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _secondSettlementReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstSettlementSaved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DbContextId? _firstContextId;
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<PaymentSettlement>()
                    .Any(entry => entry.State == EntityState.Added) == true)
            {
                if (Interlocked.Increment(ref _arrivals) == 1)
                {
                    _firstContextId = eventData.Context.ContextId;
                    await _secondSettlementReady.Task.WaitAsync(cancellationToken);
                }
                else if (_arrivals == participantCount)
                {
                    _secondSettlementReady.TrySetResult();
                    await _firstSettlementSaved.Task.WaitAsync(cancellationToken);
                }

            }

            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ContextId == _firstContextId)
            {
                _firstSettlementSaved.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
