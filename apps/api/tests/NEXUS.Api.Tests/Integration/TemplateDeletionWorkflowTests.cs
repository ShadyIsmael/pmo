using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Moq;
using NEXUS.Api.Documents;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class TemplateDeletionWorkflowTests
{
    [Fact]
    public async Task Request_requires_a_reason_and_rejects_a_second_pending_request()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, _) = await fixture.SeedTemplateAsync();
        var service = fixture.CreateWorkflowService();

        var missingReason = await service.RequestAsync(new CreateTemplateDeletionRequest(template.Id, "  "));
        Assert.Equal("TEMPLATE_DELETION_REASON_REQUIRED", missingReason.Error?.Code);

        var created = await service.RequestAsync(new CreateTemplateDeletionRequest(template.Id, "No longer required"));
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(TemplateDeletionRequest.StatusPendingApproval, created.Value!.Status);

        var duplicate = await service.RequestAsync(new CreateTemplateDeletionRequest(template.Id, "Duplicate"));
        Assert.Equal(409, duplicate.Error?.StatusCode);
        Assert.Equal("TEMPLATE_DELETION_ALREADY_PENDING", duplicate.Error?.Code);
        Assert.Equal(1, await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId && item.TemplateId == template.Id));
    }

    [Fact]
    public async Task Approval_with_protected_references_marks_template_deleted_and_preserves_versions()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true);
        var request = await fixture.CreateWorkflowService().RequestAsync(
            new CreateTemplateDeletionRequest(template.Id, "Retire this template"));
        fixture.SetActor(fixture.ApproverId, approver: true);
        fixture.ReferenceValidator.Setup(validator => validator.CheckAsync(
                fixture.TenantId, template.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateDeletionReferenceCheck(
                true, new HashSet<string>([TemplateDeletionReferenceKinds.ProjectDocument], StringComparer.Ordinal)));

        var result = await fixture.CreateWorkflowService().ApproveAsync(request.Value!.Id, "Approved after review");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TemplateDeletionRequest.DeletionTypeSoftDelete, result.Value!.DeletionType);
        var storedTemplate = await fixture.Context.Documents.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == fixture.TenantId && item.Id == template.Id);
        Assert.True(storedTemplate.IsArchived);
        Assert.True(storedTemplate.IsDeletionApproved);
        Assert.Equal("Archived", storedTemplate.Status);
        Assert.True(await fixture.Context.DocumentVersions.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == version.Id));
        fixture.NotificationPublisher.Verify(publisher => publisher.PublishDecisionAsync(
            It.Is<TemplateDeletionRequest>(item => item.Id == request.Value.Id
                && item.Status == TemplateDeletionRequest.StatusApproved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approval_without_references_hard_deletes_target_and_keeps_request_history()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true);
        var request = await fixture.CreateWorkflowService().RequestAsync(
            new CreateTemplateDeletionRequest(template.Id, "Remove unused template"));
        fixture.SetActor(fixture.ApproverId, approver: true);
        fixture.ReferenceValidator.Setup(validator => validator.CheckAsync(
                fixture.TenantId, template.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateDeletionReferenceCheck(false, new HashSet<string>(StringComparer.Ordinal)));

        var result = await fixture.CreateWorkflowService().ApproveAsync(request.Value!.Id, "No dependencies remain");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TemplateDeletionRequest.DeletionTypeHardDelete, result.Value!.DeletionType);
        Assert.False(await fixture.Context.Documents.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == template.Id));
        Assert.False(await fixture.Context.DocumentVersions.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == version.Id));
        var retainedRequest = await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == fixture.TenantId && item.Id == request.Value.Id);
        Assert.Equal(TemplateDeletionRequest.StatusApproved, retainedRequest.Status);
        Assert.Equal(TemplateDeletionRequest.CleanupStatusPending, retainedRequest.StorageCleanupStatus);
    }

    [Fact]
    public async Task Approval_after_template_mutation_returns_conflict_and_preserves_template()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, _) = await fixture.SeedTemplateAsync();
        var request = await fixture.CreateWorkflowService().RequestAsync(
            new CreateTemplateDeletionRequest(template.Id, "Remove this template"));
        fixture.SetActor(fixture.ApproverId, approver: true);
        template.Name = "Changed after request";
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateWorkflowService().ApproveAsync(request.Value!.Id, "Stale request");

        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Equal("TEMPLATE_DELETION_TEMPLATE_CHANGED", result.Error?.Code);
        Assert.True(await fixture.Context.Documents.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == fixture.TenantId && item.Id == template.Id));
        var storedRequest = await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == fixture.TenantId && item.Id == request.Value.Id);
        Assert.Equal(TemplateDeletionRequest.StatusStale, storedRequest.Status);
    }

    [Fact]
    public async Task Requester_cannot_approve_own_request_and_comments_are_required()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, _) = await fixture.SeedTemplateAsync();
        var request = await fixture.CreateWorkflowService().RequestAsync(
            new CreateTemplateDeletionRequest(template.Id, "Remove this template"));

        var missingComments = await fixture.CreateWorkflowService().RejectAsync(request.Value!.Id, " ");
        Assert.Equal("TEMPLATE_DELETION_DECISION_COMMENTS_REQUIRED", missingComments.Error?.Code);
        var selfDecision = await fixture.CreateWorkflowService().ApproveAsync(request.Value.Id, "Self approval");
        Assert.Equal("TEMPLATE_DELETION_SELF_APPROVAL", selfDecision.Error?.Code);
        Assert.Equal(TemplateDeletionRequest.StatusPendingApproval,
            (await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
                .SingleAsync(item => item.Id == request.Value.Id)).Status);
    }

    [Fact]
    public async Task Request_requires_the_effective_permission_and_active_deny_overrides_a_grant()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, _) = await fixture.SeedTemplateAsync();
        var service = fixture.CreateWorkflowService();

        fixture.SetActor(fixture.RequesterId, approver: false, includeRequestPermission: false);
        var missingGrant = await service.RequestAsync(new CreateTemplateDeletionRequest(template.Id, "Reason"));
        Assert.Equal(403, missingGrant.Error?.StatusCode);

        fixture.SetActor(fixture.RequesterId, approver: false, denyRequestPermission: true);
        var denied = await service.RequestAsync(new CreateTemplateDeletionRequest(template.Id, "Reason"));
        Assert.Equal(403, denied.Error?.StatusCode);
        Assert.Equal(0, await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == fixture.TenantId && item.TemplateId == template.Id));
    }

    [Fact]
    public async Task History_and_decision_are_isolated_from_other_organizations_and_tenants()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, _) = await fixture.SeedTemplateAsync();
        var request = await fixture.CreateWorkflowService().RequestAsync(
            new CreateTemplateDeletionRequest(template.Id, "Scope isolation"));
        var service = fixture.CreateWorkflowService();
        var outsiderId = Guid.NewGuid();
        fixture.SetActor(outsiderId, approver: true);

        var foreignOrganizationHistory = await service.GetTemplateHistoryAsync(template.Id, 1, 20);
        var foreignOrganizationDecision = await service.ApproveAsync(request.Value!.Id, "Should be denied");
        Assert.Equal(404, foreignOrganizationHistory.Error?.StatusCode);
        Assert.Equal(403, foreignOrganizationDecision.Error?.StatusCode);

        fixture.SetActor(outsiderId, approver: true, tenantOverride: Guid.NewGuid());
        var foreignTenantHistory = await service.GetTemplateHistoryAsync(template.Id, 1, 20);
        var foreignTenantDecision = await service.ApproveAsync(request.Value.Id, "Should not find request");
        Assert.Equal(404, foreignTenantHistory.Error?.StatusCode);
        Assert.Equal("TEMPLATE_DELETION_REQUEST_NOT_FOUND", foreignTenantDecision.Error?.Code);
    }

    [Fact]
    public async Task Reference_validator_treats_a_project_document_source_pin_as_protected()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true);
        fixture.Context.Documents.Add(new Document
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            IsTemplate = false,
            SourceTemplateId = template.Id,
            SourceTemplateVersionId = version.Id,
            SourceTemplateVersionNumber = version.VersionNumber,
            Name = "Project copy",
            Status = "Active",
            CreatedBy = fixture.RequesterId.ToString()
        });
        await fixture.Context.SaveChangesAsync();

        var check = await new EfTemplateDeletionReferenceValidator(fixture.Context)
            .CheckAsync(fixture.TenantId, template.Id, [version.Id]);

        Assert.True(check.HasProtectedReferences);
        Assert.Contains(TemplateDeletionReferenceKinds.ProjectDocument, check.ReferenceKinds);
    }

    [Fact]
    public async Task Deletion_approved_template_is_hidden_from_archived_search_but_remains_readable_by_id()
    {
        await using var fixture = await WorkflowFixture.CreateAsync(includeDocumentReadAndCreate: true);
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true, deletionApproved: true, archived: true);
        var documentService = fixture.CreateDocumentService();

        var archived = await documentService.GetPagedAsync(new DocumentListQuery(
            Page: 1, PageSize: 20, IncludeArchived: true, Status: "Archived"));
        var detail = await documentService.GetByIdAsync(template.Id);
        var download = await documentService.GetDownloadAsync(template.Id, version.VersionNumber);

        Assert.DoesNotContain(archived.Items, item => item.Id == template.Id);
        Assert.True(detail.IsSuccess, detail.Error?.Message);
        Assert.True(download.IsSuccess, download.Error?.Message);
        await download.Value!.Content.DisposeAsync();
    }

    [Fact]
    public async Task Deletion_approved_archived_template_cannot_pass_exact_pinned_project_document_exception()
    {
        await using var fixture = await WorkflowFixture.CreateAsync(includeDocumentReadAndCreate: true);
        var projectId = Guid.NewGuid();
        fixture.IncludeProjectInManagementScope(projectId);
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true, deletionApproved: true, archived: true);
        var pinnedReference = new Mock<IArchivedProjectTemplateReferenceValidator>();
        pinnedReference.Setup(validator => validator.IsExactPinnedReferenceAsync(
                fixture.TenantId, projectId, fixture.OrganizationId, template.Id, version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var documentService = fixture.CreateDocumentService(pinnedReference.Object);
        var file = new FormFile(new MemoryStream([0x25, 0x50, 0x44, 0x46]), 0, 4, "file", "copy.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await documentService.CreateAsync(new DocumentMutationDto(
            "Project copy", null, projectId, null,
            SourceTemplateId: template.Id,
            SourceTemplateVersionNumber: version.VersionNumber,
            File: file,
            SourceTemplateVersionId: version.Id), fixture.RequesterId);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        pinnedReference.Verify(validator => validator.IsExactPinnedReferenceAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phase_gate_catalog_rejects_deletion_approved_templates_but_keeps_ordinary_archived_pin_behavior()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (approvedDeleted, approvedVersion) = await fixture.SeedTemplateAsync(
            withVersion: true, deletionApproved: true, archived: true);
        var (ordinaryArchived, ordinaryVersion) = await fixture.SeedTemplateAsync(
            withVersion: true, deletionApproved: false, archived: true);
        using var services = new ServiceCollection()
            .AddPhaseGatePersistence()
            .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(fixture.DatabaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IApprovedTemplateCatalog>();

        var approvedResult = await catalog.GetVersionAsync(
            fixture.TenantId, fixture.OrganizationId, approvedDeleted.Id, approvedVersion.Id, requireActive: false);
        var archivedPinResult = await catalog.GetVersionAsync(
            fixture.TenantId, fixture.OrganizationId, ordinaryArchived.Id, ordinaryVersion.Id, requireActive: false);

        Assert.Null(approvedResult);
        Assert.NotNull(archivedPinResult);
    }

    [Fact]
    public async Task Phase_gate_configuration_save_cannot_add_a_deletion_approved_template()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var (template, version) = await fixture.SeedTemplateAsync(withVersion: true, deletionApproved: true, archived: true);
        var sourcePhaseId = Guid.NewGuid();
        var targetPhaseId = Guid.NewGuid();
        var phasePairs = new Mock<IProjectPhaseGatePhasePairCatalog>();
        phasePairs.Setup(catalog => catalog.GetPhasePairAsync(
                fixture.TenantId, fixture.OrganizationId, sourcePhaseId, targetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectPhaseGatePhasePairDto(
                sourcePhaseId, "start", "Start", "البداية", targetPhaseId, "review", "Review", "المراجعة"));
        var access = new Mock<IOrganizationSettingsAccessService>();
        access.Setup(service => service.HasAccessAsync(fixture.OrganizationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        using var catalogServices = new ServiceCollection()
            .AddPhaseGatePersistence()
            .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(fixture.DatabaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .BuildServiceProvider();
        await using var catalogScope = catalogServices.CreateAsyncScope();
        var catalog = catalogScope.ServiceProvider.GetRequiredService<IApprovedTemplateCatalog>();
        var phaseGate = new PhaseGateConfigurationService(
            fixture.Context,
            fixture.HttpContextAccessor,
            authorization.Object,
            access.Object,
            phasePairs.Object,
            catalog,
            fixture.Audit.Object,
            fixture.ManagementContextAccessor);

        var result = await phaseGate.SaveConfigurationAsync(
            fixture.OrganizationId,
            sourcePhaseId,
            targetPhaseId,
            new SavePhaseGateConfigurationRequest
            {
                Requirements = [new SavePhaseGateRequirementRequest
                {
                    TemplateId = template.Id,
                    TemplateVersionId = version.Id,
                    RequirementType = PhaseGateRequirementType.Mandatory
                }]
            });

        Assert.Equal(PhaseGateConfigurationOperationCode.ValidationFailed, result.Code);
        Assert.Empty(await fixture.Context.Set<OrganizationPhaseGateConfiguration>().IgnoreQueryFilters()
            .Where(item => item.TenantId == fixture.TenantId && item.OrganizationId == fixture.OrganizationId).ToListAsync());
    }

    [Fact]
    public async Task Cleanup_skips_a_storage_key_still_referenced_by_another_tenant()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var sharedKey = "legacy-shared-key";
        var request = new TemplateDeletionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TemplateId = Guid.NewGuid(),
            TemplateName = "Deleted template",
            OwnerOrganizationId = fixture.OrganizationId,
            OwnerScope = TemplateDeletionRequest.OwnerScopeOrganization,
            RequesterId = fixture.RequesterId,
            RequesterName = "Requester",
            Reason = "Approved cleanup",
            Status = TemplateDeletionRequest.StatusApproved,
            RequestedAt = DateTime.UtcNow,
            ApproverId = fixture.ApproverId,
            ApproverName = "Approver",
            DecidedAt = DateTime.UtcNow,
            DecisionComments = "Approved",
            DeletionType = TemplateDeletionRequest.DeletionTypeHardDelete,
            StorageCleanupKeysJson = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { providerId = fixture.StorageProvider.ProviderId, storageKey = sharedKey }
            }),
            StorageCleanupStatus = TemplateDeletionRequest.CleanupStatusPending,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedBy = fixture.RequesterId.ToString()
        };
        fixture.Context.Set<TemplateDeletionRequest>().Add(request);
        await fixture.Context.SaveChangesAsync();

        var otherTenantId = Guid.NewGuid();
        fixture.SetActor(Guid.NewGuid(), approver: false, tenantOverride: otherTenantId);
        var otherTenantDocument = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other tenant document",
            Status = "Active",
            StoragePath = sharedKey,
            CreatedBy = Guid.NewGuid().ToString()
        };
        fixture.Context.Documents.Add(otherTenantDocument);
        fixture.Context.DocumentVersions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            DocumentId = otherTenantDocument.Id,
            VersionNumber = 1,
            StorageProviderId = fixture.StorageProvider.ProviderId,
            StorageKey = sharedKey,
            FileName = "shared.pdf",
            FileSize = 4,
            UploadedAt = DateTime.UtcNow,
            IsCurrent = true,
            CreatedBy = "other-tenant"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.SetActor(fixture.ApproverId, approver: true);

        using var services = new ServiceCollection()
            .AddKeyedSingleton<IStorageProvider>(fixture.StorageProvider.ProviderId, fixture.StorageProvider)
            .BuildServiceProvider();
        var processor = new TemplateDeletionStorageCleanupProcessor(
            fixture.Context, fixture.StorageProvider, services, NullLogger<TemplateDeletionStorageCleanupProcessor>.Instance);
        await processor.ProcessRequestAsync(fixture.TenantId, request.Id);

        var storedRequest = await fixture.Context.Set<TemplateDeletionRequest>().IgnoreQueryFilters()
            .SingleAsync(item => item.Id == request.Id && item.TenantId == fixture.TenantId);
        Assert.Equal(TemplateDeletionRequest.CleanupStatusCompleted, storedRequest.StorageCleanupStatus);
        Assert.Equal(0, fixture.StorageProvider.DeleteCallCount);
        Assert.True(await fixture.Context.DocumentVersions.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == otherTenantId && item.StorageKey == sharedKey));
    }

    private sealed class WorkflowFixture : IAsyncDisposable
    {
        private readonly HttpContextAccessor _http;
        private readonly ManagementContextAccessor _management;
        private readonly string _databaseName;
        private readonly TestStorageProvider _storage = new();

        private WorkflowFixture(
            Guid tenantId,
            Guid requesterId,
            Guid approverId,
            Guid organizationId,
            HttpContextAccessor http,
            ManagementContextAccessor management,
            string databaseName,
            AppDbContext context)
        {
            TenantId = tenantId;
            RequesterId = requesterId;
            ApproverId = approverId;
            OrganizationId = organizationId;
            _http = http;
            _management = management;
            _databaseName = databaseName;
            Context = context;
            var organization = new Organization
            {
                Id = organizationId,
                TenantId = tenantId,
                Name = "Test organization",
                IsActive = true,
                CreatedBy = requesterId.ToString()
            };
            Context.Organizations.Add(organization);
            Context.SaveChanges();
        }

        public Guid TenantId { get; }
        public Guid RequesterId { get; }
        public Guid ApproverId { get; }
        public Guid OrganizationId { get; }
        public string DatabaseName => _databaseName;
        public AppDbContext Context { get; }
        public TestStorageProvider StorageProvider => _storage;
        public HttpContextAccessor HttpContextAccessor => _http;
        public ManagementContextAccessor ManagementContextAccessor => _management;
        public Mock<ITemplateDeletionApproverResolver> ApproverResolver { get; } = new();
        public Mock<ITemplateDeletionReferenceValidator> ReferenceValidator { get; } = new();
        public Mock<ITemplateDeletionNotificationPublisher> NotificationPublisher { get; } = new();
        public Mock<IAuditService> Audit { get; } = new();

        public static async Task<WorkflowFixture> CreateAsync(bool includeDocumentReadAndCreate = false)
        {
            var tenantId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var approverId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
            {
                OrganizationIds = new HashSet<Guid> { organizationId },
                OrganizationReferences = [new ManagementEntityReference(organizationId, "Test organization")]
            };
            var management = new ManagementContextAccessor();
            management.Set(new ManagementContext(requesterId, tenantId, ManagementPerspective.ProjectManager,
                ManagementViewMode.Native, null, null, null, scope));
            AppDbContext.ConfigureTenantResolution(http);
            var databaseName = $"template-deletion-{Guid.NewGuid():N}";
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var fixture = new WorkflowFixture(tenantId, requesterId, approverId, organizationId, http, management,
                databaseName, context);
            fixture.SetActor(requesterId, approver: false, includeDocumentReadAndCreate);

            fixture.ApproverResolver.Setup(resolver => resolver.GetEligibleApproverIdsAsync(
                    tenantId, organizationId, requesterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { approverId });
            fixture.ApproverResolver.Setup(resolver => resolver.IsAuthorizedAdministratorAsync(
                    approverId, tenantId, organizationId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            fixture.ReferenceValidator.Setup(validator => validator.CheckAsync(
                    tenantId, It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TemplateDeletionReferenceCheck(false, new HashSet<string>(StringComparer.Ordinal)));
            fixture.NotificationPublisher.Setup(publisher => publisher.PublishPendingAsync(
                    It.IsAny<TemplateDeletionRequest>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            fixture.NotificationPublisher.Setup(publisher => publisher.PublishDecisionAsync(
                    It.IsAny<TemplateDeletionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            fixture.Audit.Setup(audit => audit.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public async Task<(Document Template, DocumentVersion Version)> SeedTemplateAsync(
            bool withVersion = true,
            bool deletionApproved = false,
            bool archived = false)
        {
            var template = new Document
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrganizationId = OrganizationId,
                IsTemplate = true,
                IsArchived = archived,
                IsDeletionApproved = deletionApproved,
                Name = "Test template",
                Description = "Template details",
                Status = archived ? "Archived" : "Active",
                Version = 1,
                StoragePath = "template-key",
                CreatedBy = RequesterId.ToString()
            };
            var version = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DocumentId = template.Id,
                VersionNumber = 1,
                FileName = "template.pdf",
                FileSize = 4,
                ContentType = "application/pdf",
                StorageProviderId = _storage.ProviderId,
                StorageKey = "template-key",
                IsCurrent = true,
                UploadedBy = RequesterId,
                UploadedAt = DateTime.UtcNow,
                CreatedBy = RequesterId.ToString()
            };
            Context.Documents.Add(template);
            if (withVersion) Context.DocumentVersions.Add(version);
            await Context.SaveChangesAsync();
            return (template, version);
        }

        public TemplateDeletionWorkflowService CreateWorkflowService() => new(
            Context,
            Audit.Object,
            _http,
            _management,
            ApproverResolver.Object,
            ReferenceValidator.Object,
            NotificationPublisher.Object,
        _storage,
        NullLogger<TemplateDeletionWorkflowService>.Instance);

        public DocumentService CreateDocumentService(IArchivedProjectTemplateReferenceValidator? archivedValidator = null) => new(
            Context,
            _storage,
            _http,
            Audit.Object,
            _management,
            archivedProjectTemplateReferenceValidator: archivedValidator);

        public void IncludeProjectInManagementScope(Guid projectId)
        {
            var current = _management.Current!;
            var scope = current.Scope with
            {
                ProjectIds = new HashSet<Guid> { projectId },
                ProjectOrganizationIds = new Dictionary<Guid, Guid> { [projectId] = OrganizationId }
            };
            _management.Set(current with { Scope = scope });
        }

        public void SetActor(
            Guid userId,
            bool approver,
            bool includeDocumentReadAndCreate = false,
            bool includeRequestPermission = true,
            bool denyRequestPermission = false,
            Guid? tenantOverride = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, approver ? "Approver" : "Requester"),
                new("tenantId", (tenantOverride ?? TenantId).ToString()),
                new("permission", DocumentPermissions.View),
                new("permission", TemplateDeletionPermissions.View)
            };
            if (approver)
            {
                claims.Add(new Claim("permission", TemplateDeletionPermissions.Approve));
                claims.Add(new Claim("permission", TemplateDeletionPermissions.Reject));
            }
            else if (includeRequestPermission)
            {
                claims.Add(new Claim("permission", TemplateDeletionPermissions.Request));
            }
            if (denyRequestPermission) claims.Add(new Claim("deny", TemplateDeletionPermissions.Request));
            if (includeDocumentReadAndCreate) claims.Add(new Claim("permission", DocumentPermissions.Create));
            _http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "template-deletion-test"));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        }
    }

    private sealed class TestStorageProvider : IStorageProvider
    {
        public int DeleteCallCount { get; private set; }
        public string ProviderId => "template-deletion-test";
        public Task<StoredFile> StoreAsync(Stream content, string extension, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredFile(ProviderId, "new-key", 0, string.Empty));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream([0x25, 0x50, 0x44, 0x46]));
        public Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredFileMetadata?>(new StoredFileMetadata(ProviderId, storageKey, 4, string.Empty));
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return Task.CompletedTask;
        }
    }
}
