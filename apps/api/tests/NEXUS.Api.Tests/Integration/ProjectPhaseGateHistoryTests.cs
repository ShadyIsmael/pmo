using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Covers the immutable phase-change history that is projected from the append-only
/// AuditEntry trail. These tests never require a new persistence table: the history is
/// read back from the transition audit rows the phase-change service already writes.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class ProjectPhaseGateHistoryTests : IDisposable
{
    private const string SourcePhaseCode = "test-source";
    private const string TargetPhaseCode = "test-target";

    [Fact]
    public async Task Change_phase_audit_snapshot_records_requirements_and_phase_labels()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var config = Config(fixture, version: 4, Requirement("Mandatory"));
        var reader = Reader(fixture, config);
        var appended = new List<AuditEntry>();
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: true,
            onAuditAppend: appended.Add);

        var result = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);

        result.IsSuccess.Should().BeTrue();
        // Exactly one audit event per transition; the snapshot must never create a second row.
        audit.Verify(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        appended.Should().ContainSingle();
        var entry = appended[0];
        entry.EntityType.Should().Be("ProjectPhase");
        entry.Action.Should().Be("Change");
        entry.ChangedFields.Should().Be("Phase");

        using var changes = JsonDocument.Parse(entry.Changes!);
        var root = changes.RootElement;
        root.GetProperty("phaseLabels").GetProperty("fromEn").GetString().Should().Be("Source phase");
        root.GetProperty("phaseLabels").GetProperty("fromAr").GetString().Should().Be("مرحلة البداية");
        root.GetProperty("phaseLabels").GetProperty("toEn").GetString().Should().Be("Target phase");
        root.GetProperty("phaseLabels").GetProperty("toAr").GetString().Should().Be("المرحلة المستهدفة");
        root.GetProperty("evaluationOutcome").GetString().Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
        root.GetProperty("mandatoryRequirementCount").GetInt32().Should().Be(1);
        root.GetProperty("satisfiedMandatoryCount").GetInt32().Should().Be(0);
        root.GetProperty("missingMandatoryCount").GetInt32().Should().Be(1);
        var requirement = root.GetProperty("requirements").EnumerateArray().Single();
        requirement.GetProperty("requirementType").GetString().Should().Be("Mandatory");
        requirement.GetProperty("status").GetString().Should().Be(ProjectPhaseGateRequirementStatuses.Missing);
        requirement.GetProperty("complianceStatus").GetString().Should().Be(ProjectDocumentComplianceStatuses.Missing);
        requirement.GetProperty("templateName").GetString().Should().Be("Mandatory template");
        requirement.GetProperty("complianceFindings").ValueKind.Should().Be(JsonValueKind.Array);

        // NewValues carries the same immutable snapshot for consumers that read the "after" image.
        using var newValues = JsonDocument.Parse(entry.NewValues!);
        newValues.RootElement.GetProperty("requirements").GetArrayLength().Should().Be(1);
        newValues.RootElement.GetProperty("phaseLabels").GetProperty("toAr").GetString().Should().Be("المرحلة المستهدفة");
        newValues.RootElement.GetProperty("missingMandatoryCount").GetInt32().Should().Be(1);
        newValues.RootElement.GetProperty("requirementEvaluation").GetString()
            .Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
    }

    [Fact]
    public async Task History_maps_the_persisted_snapshot_newest_first()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: false);
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var requirementSnapshotId = Guid.NewGuid();
        var sourceRequirementId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();
        var comparedAt = new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);
        var older = BuildTransitionEntry(fixture, new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc),
            previousPhase: "initiation", newPhase: SourcePhaseCode);
        older.Changes = JsonSerializer.Serialize(new
        {
            phase = new { from = "initiation", to = SourcePhaseCode },
            phaseIds = new { from = Guid.NewGuid(), to = fixture.SourcePhaseId },
            evaluationOutcome = ProjectPhaseGateReadinessStatuses.Incomplete,
            mandatoryRequirementCount = 1,
            satisfiedMandatoryCount = 0,
            optionalRequirementCount = 0,
            satisfiedOptionalCount = 0
        });
        var newer = BuildTransitionEntry(fixture, new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
            previousPhase: SourcePhaseCode, newPhase: TargetPhaseCode);
        newer.Changes = JsonSerializer.Serialize(new
        {
            phase = new { from = SourcePhaseCode, to = TargetPhaseCode },
            phaseIds = new { from = fixture.SourcePhaseId, to = fixture.TargetPhaseId },
            phaseLabels = new { fromEn = "Source phase", fromAr = "مرحلة البداية", toEn = "Target phase", toAr = "المرحلة المستهدفة" },
            configurationVersionId,
            configurationVersionNumber = 9,
            evaluationOutcome = ProjectPhaseGateReadinessStatuses.Ready,
            mandatoryRequirementCount = 2,
            satisfiedMandatoryCount = 2,
            missingMandatoryCount = 0,
            optionalRequirementCount = 1,
            satisfiedOptionalCount = 0,
            requirements = new object[]
            {
                new
                {
                    requirementSnapshotId,
                    sourceRequirementId,
                    templateId,
                    templateVersionId,
                    templateName = "Business case",
                    category = "Governance",
                    templateVersionLabel = "v2",
                    requirementType = "Mandatory",
                    status = ProjectPhaseGateRequirementStatuses.Satisfied,
                    complianceStatus = ProjectDocumentComplianceStatuses.Compliant,
                    expectedFileType = "PDF",
                    detectedFileType = "PDF",
                    comparedAt,
                    validatorVersion = "1.0",
                    complianceFindings = new[]
                    {
                        new
                        {
                            code = "FileTypeMatch",
                            messageEn = "The detected file type matches the expected type.",
                            messageAr = "نوع الملف المكتشف مطابق للنوع المتوقع.",
                            expected = "PDF",
                            detected = "PDF"
                        }
                    }
                }
            }
        });
        audit.Setup(item => item.GetPagedAsync(1, 100, "ProjectPhase", "Change", fixture.Project.Id,
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AuditEntry>([older, newer], 1, 100, 2));

        var history = await service.GetPhaseHistoryAsync(fixture.Project.Id);

        history.IsSuccess.Should().BeTrue();
        history.Value.Should().HaveCount(2);
        var latest = history.Value![0];
        latest.Id.Should().Be(newer.Id);
        latest.ProjectId.Should().Be(fixture.Project.Id);
        latest.PreviousPhaseId.Should().Be(fixture.SourcePhaseId);
        latest.NewPhaseId.Should().Be(fixture.TargetPhaseId);
        latest.PreviousPhase.Should().Be(SourcePhaseCode);
        latest.NewPhase.Should().Be(TargetPhaseCode);
        latest.PreviousPhaseLabelEn.Should().Be("Source phase");
        latest.PreviousPhaseLabelAr.Should().Be("مرحلة البداية");
        latest.NewPhaseLabelEn.Should().Be("Target phase");
        latest.NewPhaseLabelAr.Should().Be("المرحلة المستهدفة");
        latest.ConfigurationVersionId.Should().Be(configurationVersionId);
        latest.ConfigurationVersionNumber.Should().Be(9);
        latest.EvaluationOutcome.Should().Be(ProjectPhaseGateReadinessStatuses.Ready);
        latest.MandatoryRequirementCount.Should().Be(2);
        latest.SatisfiedMandatoryCount.Should().Be(2);
        latest.MissingMandatoryCount.Should().Be(0);
        latest.OptionalRequirementCount.Should().Be(1);
        latest.SatisfiedOptionalCount.Should().Be(0);
        latest.ActorId.Should().Be(fixture.UserId);
        latest.ActorName.Should().Be("History actor");
        latest.TransitionedAt.Should().Be(new DateTimeOffset(new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc)));
        latest.Requirements.Should().ContainSingle();
        var requirement = latest.Requirements[0];
        requirement.RequirementSnapshotId.Should().Be(requirementSnapshotId);
        requirement.SourceRequirementId.Should().Be(sourceRequirementId);
        requirement.TemplateId.Should().Be(templateId);
        requirement.TemplateVersionId.Should().Be(templateVersionId);
        requirement.TemplateName.Should().Be("Business case");
        requirement.Category.Should().Be("Governance");
        requirement.TemplateVersionLabel.Should().Be("v2");
        requirement.RequirementType.Should().Be("Mandatory");
        requirement.Status.Should().Be(ProjectPhaseGateRequirementStatuses.Satisfied);
        requirement.ComplianceStatus.Should().Be(ProjectDocumentComplianceStatuses.Compliant);
        requirement.ExpectedFileType.Should().Be("PDF");
        requirement.DetectedFileType.Should().Be("PDF");
        requirement.ComparedAt.Should().Be(comparedAt);
        requirement.ValidatorVersion.Should().Be("1.0");
        requirement.Findings.Should().ContainSingle();
        requirement.Findings[0].Code.Should().Be("FileTypeMatch");
        requirement.Findings[0].MessageEn.Should().Be("The detected file type matches the expected type.");
        requirement.Findings[0].MessageAr.Should().Be("نوع الملف المكتشف مطابق للنوع المتوقع.");
        requirement.Findings[0].Expected.Should().Be("PDF");
        requirement.Findings[0].Detected.Should().Be("PDF");

        // Legacy rows written before the snapshot existed still map their original keys.
        var previous = history.Value![1];
        previous.Id.Should().Be(older.Id);
        previous.PreviousPhaseId.Should().Be(fixture.SourcePhaseId);
        previous.PreviousPhase.Should().Be("initiation");
        previous.NewPhase.Should().Be(SourcePhaseCode);
        previous.NewPhaseId.Should().Be(fixture.SourcePhaseId);
        previous.PreviousPhaseLabelEn.Should().BeNull();
        previous.PreviousPhaseLabelAr.Should().BeNull();
        previous.EvaluationOutcome.Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
        previous.MandatoryRequirementCount.Should().Be(1);
        previous.MissingMandatoryCount.Should().Be(1);
        previous.Requirements.Should().BeEmpty();
    }

    [Fact]
    public async Task History_tolerates_malformed_or_missing_audit_json()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: false);
        var malformed = BuildTransitionEntry(fixture, new DateTime(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc),
            previousPhase: string.Empty, newPhase: string.Empty);
        malformed.Changes = "{ this is not valid json";
        malformed.OriginalValues = "[]";
        malformed.NewValues = "null";
        var missing = BuildTransitionEntry(fixture, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            previousPhase: string.Empty, newPhase: string.Empty);
        missing.Changes = null;
        missing.OriginalValues = null;
        missing.NewValues = null;
        audit.Setup(item => item.GetPagedAsync(1, 100, "ProjectPhase", "Change", fixture.Project.Id,
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AuditEntry>([malformed, missing], 1, 100, 2));

        var history = await service.GetPhaseHistoryAsync(fixture.Project.Id);

        history.IsSuccess.Should().BeTrue();
        history.Value.Should().HaveCount(2);
        foreach (var item in history.Value!)
        {
            item.Requirements.Should().BeEmpty();
            item.PreviousPhaseLabelEn.Should().BeNull();
            item.NewPhaseLabelEn.Should().BeNull();
            item.EvaluationOutcome.Should().Be(ProjectPhaseGateReadinessStatuses.NotApplicable);
            item.PreviousPhaseId.Should().Be(Guid.Empty);
            item.NewPhaseId.Should().Be(Guid.Empty);
            item.PreviousPhase.Should().BeEmpty();
            item.NewPhase.Should().BeEmpty();
            item.MandatoryRequirementCount.Should().Be(0);
            item.MissingMandatoryCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task History_requires_project_view_and_hides_out_of_scope_projects()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();

        var withoutView = CreateService(db, fixture, reader.Object, out var withoutViewAudit,
            includeChangePermission: false, permissions: ["Document.View"]);
        var missingPermission = await withoutView.GetPhaseHistoryAsync(fixture.Project.Id);
        missingPermission.IsFailure.Should().BeTrue();
        missingPermission.Error!.StatusCode.Should().Be(403);
        withoutViewAudit.Verify(item => item.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var deniedView = CreateService(db, fixture, reader.Object, out var deniedViewAudit,
            includeChangePermission: false, permissions: ["Project.View"], deniedPermissions: ["Project.View"]);
        var activeDeny = await deniedView.GetPhaseHistoryAsync(fixture.Project.Id);
        activeDeny.IsFailure.Should().BeTrue();
        activeDeny.Error!.StatusCode.Should().Be(403);
        deniedViewAudit.Verify(item => item.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        // Project.View without Document.View is sufficient for history.
        var scoped = CreateService(db, fixture, reader.Object, out var scopedAudit,
            includeChangePermission: false, permissions: ["Project.View"]);
        scopedAudit.Setup(item => item.GetPagedAsync(1, 100, "ProjectPhase", "Change", fixture.Project.Id,
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AuditEntry>([], 1, 100, 0));
        var empty = await scoped.GetPhaseHistoryAsync(fixture.Project.Id);
        empty.IsSuccess.Should().BeTrue();
        empty.Value.Should().BeEmpty();

        var outOfScope = await scoped.GetPhaseHistoryAsync(Guid.NewGuid());
        outOfScope.IsFailure.Should().BeTrue();
        outOfScope.Error!.StatusCode.Should().Be(403);
    }

    [Fact]
    public void History_endpoint_requires_only_project_view()
    {
        static string[] Policies(string method) => typeof(ProjectPhaseGateController).GetMethod(method)!
            .GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy!).ToArray();

        Policies(nameof(ProjectPhaseGateController.GetHistory)).Should().BeEquivalentTo("Project.View");
        typeof(ProjectPhaseGateController).GetMethod(nameof(ProjectPhaseGateController.GetHistory))!
            .GetCustomAttributes<HttpGetAttribute>().Single().Template.Should().Be("history");
    }

    [Fact]
    public async Task Lifecycle_options_expose_phase_and_status_descriptions()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        const string phaseDescriptionEn = "Delivery phase description";
        const string phaseDescriptionAr = "وصف مرحلة التسليم";
        const string statusDescriptionEn = "In-progress status description";
        const string statusDescriptionAr = "وصف حالة قيد التنفيذ";
        var project = new Project
        {
            Id = projectId, TenantId = tenantId, Name = "History lifecycle project",
            Code = "HISTORY-LIFECYCLE", PortfolioId = portfolioId,
            ProjectPhaseId = phaseId, ProjectStatusId = statusId,
            CreatedBy = userId.ToString()
        };
        var organization = new Organization
        {
            Id = organizationId, TenantId = tenantId, Name = "History lifecycle organization",
            IsActive = true, CreatedBy = userId.ToString()
        };
        var portfolio = new Portfolio
        {
            Id = portfolioId, TenantId = tenantId, OrganizationId = organizationId,
            Name = "History lifecycle portfolio", CreatedBy = userId.ToString()
        };
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("tenantId", tenantId.ToString()),
                    new Claim("permission", "Project.Edit")
                ], "phase-history-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase-gate-history-lifecycle-{Guid.NewGuid():N}")
            .Options);
        await using (db)
        {
            db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
            {
                Id = phaseId, TenantId = tenantId, OrganizationId = organizationId,
                Code = "delivery", NormalizedCode = "DELIVERY",
                NameEn = "Delivery", NameAr = "التسليم", DescriptionEn = phaseDescriptionEn,
                DescriptionAr = phaseDescriptionAr, DisplayOrder = 10, IsActive = true,
                CreatedBy = userId.ToString()
            });
            db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
            {
                TenantId = tenantId, OrganizationId = organizationId,
                Code = "pre-initiation", NormalizedCode = "PRE-INITIATION",
                NameEn = "Pre-Initiation", NameAr = "مرحلة ما قبل البدء",
                DisplayOrder = 0, IsDefault = true, IsActive = true,
                CreatedBy = userId.ToString()
            });
            db.ProjectStatusCatalogEntries.Add(new ProjectStatusCatalogEntry
            {
                Id = statusId, TenantId = tenantId, Code = "in-progress", NormalizedCode = "IN-PROGRESS",
                NameEn = "In Progress", NameAr = "قيد التنفيذ", DescriptionEn = statusDescriptionEn,
                DescriptionAr = statusDescriptionAr, DisplayOrder = 1, IsActive = true,
                CreatedBy = userId.ToString()
            });
            db.AddRange(organization, portfolio, project);
            await db.SaveChangesAsync();

            var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId });
            var management = new ManagementContext(userId, tenantId, ManagementPerspective.ProjectManager,
                ManagementViewMode.Native, null, null, projectId, scope);
            var managementAccessor = new Mock<IManagementContextAccessor>();
            managementAccessor.SetupGet(item => item.Current).Returns(management);
            var service = new ProjectLifecycleCatalogService(db, http, Mock.Of<IAuditService>(),
                managementAccessor.Object, new ProjectOrganizationLookup(db));

            var options = await service.GetOptionsAsync(organizationId: null, projectId);

            options.IsSuccess.Should().BeTrue();
            var phaseOption = options.Value!.Phases.Single(item => item.Id == phaseId);
            phaseOption.DescriptionEn.Should().Be(phaseDescriptionEn);
            phaseOption.DescriptionAr.Should().Be(phaseDescriptionAr);
            var statusOption = options.Value.Statuses.Single(item => item.Id == statusId);
            statusOption.DescriptionEn.Should().Be(statusDescriptionEn);
            statusOption.DescriptionAr.Should().Be(statusDescriptionAr);
        }
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private static AuditEntry BuildTransitionEntry(
        ProjectFixture fixture,
        DateTime timestamp,
        string previousPhase,
        string newPhase) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = fixture.TenantId,
        ProjectId = fixture.Project.Id,
        EntityId = fixture.Project.Id,
        EntityType = "ProjectPhase",
        Action = "Change",
        UserId = fixture.UserId,
        UserName = "History actor",
        Timestamp = timestamp,
        OriginalValues = JsonSerializer.Serialize(new { phase = previousPhase, phaseId = fixture.SourcePhaseId }),
        NewValues = JsonSerializer.Serialize(new
        {
            phase = newPhase,
            phaseId = newPhase == TargetPhaseCode ? fixture.TargetPhaseId : fixture.SourcePhaseId
        })
    };

    private static async Task<ProjectFixture> CreateProjectAsync()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourcePhaseId = Guid.NewGuid();
        var targetPhaseId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase-gate-history-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = portfolioId,
            Name = "Phase history test", Code = $"P-{Guid.NewGuid():N}"[..10],
            ProjectPhaseId = sourcePhaseId, Phase = SourcePhaseCode, Status = "On Hold", RowVersion = [1]
        };
        db.Organizations.Add(new Organization
        {
            Id = organizationId, TenantId = tenantId, Name = "Phase history organization", IsActive = true
        });
        db.ProjectPhaseCatalogEntries.AddRange(
            new ProjectPhaseCatalogEntry
            {
                Id = sourcePhaseId, TenantId = tenantId, OrganizationId = organizationId,
                Code = SourcePhaseCode, NormalizedCode = SourcePhaseCode.ToUpperInvariant(),
                NameEn = "Source phase", NameAr = "مرحلة البداية", DisplayOrder = 10, IsActive = true
            },
            new ProjectPhaseCatalogEntry
            {
                Id = targetPhaseId, TenantId = tenantId, OrganizationId = organizationId,
                Code = TargetPhaseCode, NormalizedCode = TargetPhaseCode.ToUpperInvariant(),
                NameEn = "Target phase", NameAr = "المرحلة المستهدفة", DisplayOrder = 20, IsActive = true
            });
        db.Portfolios.Add(new Portfolio
        {
            Id = portfolioId, TenantId = tenantId, OrganizationId = organizationId,
            Code = "PORT-HISTORY-001", Name = "History Portfolio", Currency = "GBP"
        });
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return new ProjectFixture(db, tenantId, organizationId, userId, sourcePhaseId, targetPhaseId, project);
    }

    private static ProjectPhaseGateReadinessService CreateService(
        AppDbContext db,
        ProjectFixture fixture,
        IProjectPhaseGateConfigurationReader reader,
        out Mock<IAuditService> audit,
        bool includeChangePermission,
        string[]? permissions = null,
        string[]? deniedPermissions = null,
        Action<AuditEntry>? onAuditAppend = null)
    {
        var effectivePermissions = permissions ?? ["Project.View", "Document.View"];
        if (includeChangePermission) effectivePermissions = [.. effectivePermissions, "ProjectPhase.Change"];
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, fixture.UserId.ToString()),
            new("tenantId", fixture.TenantId.ToString())
        };
        claims.AddRange(effectivePermissions.Select(permission => new Claim("permission", permission)));
        claims.AddRange((deniedPermissions ?? []).Select(permission => new Claim("deny", permission)));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "phase-history-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { fixture.Project.Id });
        var management = new ManagementContext(fixture.UserId, fixture.TenantId,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, fixture.Project.Id, scope);
        var managementAccessor = new Mock<IManagementContextAccessor>();
        managementAccessor.SetupGet(item => item.Current).Returns(management);
        audit = new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => onAuditAppend?.Invoke(entry))
            .ReturnsAsync(Result.Success());
        var documents = new Mock<IDocumentService>();
        var storage = new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");

        return new ProjectPhaseGateReadinessService(db, http, reader,
            documents.Object, storage.Object, audit.Object, new ProjectOrganizationLookup(db),
            managementAccessor.Object);
    }

    private static Mock<IProjectPhaseGateConfigurationReader> Reader(ProjectFixture fixture,
        ProjectPhaseGateConfigurationDto config)
    {
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[config]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        return reader;
    }

    private static HttpContextAccessor CreateTenantAccessor(Guid tenantId, Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenantId", tenantId.ToString()),
                new Claim("permission", "Project.View"),
                new Claim("permission", "Document.View"),
                new Claim("permission", "ProjectPhase.Change")
            ], "phase-history-test"))
        }
    };

    private static ProjectPhaseGateConfigurationDto Config(
        ProjectFixture fixture,
        int version,
        params ProjectPhaseGateConfigurationRequirementDto[] requirements) => new(
        Guid.NewGuid(), Guid.NewGuid(), version, fixture.OrganizationId,
        fixture.SourcePhaseId, SourcePhaseCode, fixture.TargetPhaseId, TargetPhaseCode, requirements);

    private static ProjectPhaseGateConfigurationRequirementDto Requirement(string type) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"{type} template", "Governance", "v1", type);

    private sealed record ProjectFixture(
        AppDbContext Db,
        Guid TenantId,
        Guid OrganizationId,
        Guid UserId,
        Guid SourcePhaseId,
        Guid TargetPhaseId,
        Project Project);
}
