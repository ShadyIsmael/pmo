using System.Security.Claims;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Application.Mapping;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class ProjectPhaseGateLifecycleTests
{
    private const string SourcePhaseCode = "test-source";
    private const string TargetPhaseCode = "test-target";

    [Theory]
    [InlineData("ProjectPhase")]
    [InlineData("ProjectPhaseGateEvidence")]
    public void Phase_gate_audit_events_remain_in_the_project_management_module(string entityType)
    {
        AuditModuleMap.Resolve(entityType).Should().Be(AuditModuleMap.ProjectManagement);
        AuditModuleMap.EntityTypesFor(AuditModuleMap.ProjectManagement).Should().Contain(entityType);
    }

    [Fact]
    public void Phase_gate_endpoints_require_the_canonical_action_permissions()
    {
        static string[] Policies(string method) => typeof(ProjectPhaseGateController).GetMethod(method)!
            .GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy!).ToArray();

        Policies(nameof(ProjectPhaseGateController.GetReadiness)).Should().BeEquivalentTo("Project.View", "Document.View");
        Policies(nameof(ProjectPhaseGateController.ChangePhase)).Should().BeEquivalentTo("Project.View", "ProjectPhase.Change");
        Policies(nameof(ProjectPhaseGateController.LinkDocument)).Should().BeEquivalentTo("Project.View", "Document.View", "Document.Edit");
        Policies(nameof(ProjectPhaseGateController.UnlinkDocument)).Should().BeEquivalentTo("Project.View", "Document.View", "Document.Edit");
    }

    [Fact]
    public void New_project_mapping_uses_status_id_and_does_not_assign_client_phase_or_status_text()
    {
        var statusId = Guid.NewGuid();
        var create = new CreateProjectDto
        {
            Name = "New project", Code = "P-1", StatusId = statusId, Status = "On Hold"
        };
        var project = create.ToEntity("creator");

        project.ProjectStatusId.Should().Be(statusId);
        typeof(CreateProjectDto).GetProperty("Phase").Should().BeNull();
        typeof(UpdateProjectDto).GetProperty("Phase").Should().BeNull();

        var historical = new Project { Phase = "delivery", Status = "Planning", ProjectStatusId = statusId };
        new UpdateProjectDto { Status = "Active" }.UpdateEntity(historical, "editor");

        historical.Phase.Should().Be("delivery");
        historical.ProjectStatusId.Should().Be(statusId);
        historical.Status.Should().Be("Planning");
    }

    [Fact]
    public async Task Readiness_pins_configuration_but_missing_or_mismatched_documents_do_not_block_transition()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var originalConfig = Config(fixture, version: 7, Requirement("Mandatory"), Requirement("Optional"));
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[originalConfig]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalConfig);
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: true);

        var first = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        first.IsSuccess.Should().BeTrue();
        first.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
        first.Value.MissingMandatoryCount.Should().Be(1);
        first.Value.SatisfiedOptionalCount.Should().Be(0);
        first.Value.ConfigurationVersionId.Should().Be(originalConfig.VersionId);
        first.Value.Requirements.Should().ContainSingle(item => item.Status == ProjectPhaseGateRequirementStatuses.Missing);
        first.Value.Requirements.Should().ContainSingle(item => item.Status == ProjectPhaseGateRequirementStatuses.OptionalNotProvided);

        var mandatory = first.Value.Requirements.Single(item => item.RequirementType == "Mandatory");
        var wrongVersionDocumentId = Guid.NewGuid();
        var wrongVersionId = Guid.NewGuid();
        var wrongVersionDocument = new Document
        {
            Id = wrongVersionDocumentId, TenantId = fixture.TenantId, ProjectId = fixture.Project.Id,
            Name = "Wrong version evidence", FileName = "evidence.pdf", SourceTemplateId = mandatory.TemplateId,
            SourceTemplateVersionId = Guid.NewGuid(), IsTemplate = false, IsArchived = false, Status = "Active"
        };
        db.Documents.Add(wrongVersionDocument);
        db.DocumentVersions.Add(new DocumentVersion
        {
            Id = wrongVersionId, TenantId = fixture.TenantId, DocumentId = wrongVersionDocumentId,
            Document = wrongVersionDocument, VersionNumber = 1, FileName = "evidence.pdf", FileSize = 10,
            StorageProviderId = "test", StorageKey = "opaque-key", IsCurrent = true
        });
        db.ProjectPhaseGateEvidence.Add(new ProjectPhaseGateEvidence
        {
            TenantId = fixture.TenantId, ProjectId = fixture.Project.Id, OrganizationId = fixture.OrganizationId,
            RequirementSnapshotId = mandatory.RequirementSnapshotId, DocumentId = wrongVersionDocumentId,
            DocumentVersionId = wrongVersionId, CreatedBy = fixture.UserId.ToString()
        });
        await db.SaveChangesAsync();

        var revisedConfig = Config(fixture, version: 8, Requirement("Optional"));
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[revisedConfig]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revisedConfig);

        var afterSettingsEdit = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        afterSettingsEdit.IsSuccess.Should().BeTrue();
        afterSettingsEdit.Value!.ConfigurationVersionId.Should().Be(originalConfig.VersionId);
        afterSettingsEdit.Value.ConfigurationVersionNumber.Should().Be(7);
        afterSettingsEdit.Value.Requirements.Select(item => item.TemplateVersionId)
            .Should().BeEquivalentTo(originalConfig.Requirements.Select(item => item.TemplateVersionId));
        afterSettingsEdit.Value.Requirements.Single(item => item.RequirementType == "Mandatory").Status
            .Should().Be(ProjectPhaseGateRequirementStatuses.InvalidOrUnavailable);

        // The original snapshot remains pinned, and an unavailable mismatched document remains
        // visible as incomplete readiness. It does not block an otherwise authorized transition.
        var transition = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);
        transition.IsSuccess.Should().BeTrue();
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase
            .Should().Be(TargetPhaseCode);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.EntityType == "ProjectPhase"
            && entry.Changes!.Contains("Incomplete")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unconfigured_phase_pair_changes_without_a_graph_edge_or_document_gate()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)Array.Empty<ProjectPhaseGateConfigurationDto>());
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectPhaseGateConfigurationDto?)null);
        var service = CreateService(db, fixture, reader.Object, out _, includeChangePermission: true);

        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        readiness.IsSuccess.Should().BeTrue();
        readiness.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.NotApplicable);
        readiness.Value.ReadinessPercentage.Should().BeNull();
        (await db.ProjectPhaseGateSnapshots.CountAsync()).Should().Be(0);

        var transitionService = CreateService(db, fixture, reader.Object, out var audit,
            includeChangePermission: true, permissions: ["Project.View"]);
        var transition = await transitionService.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);
        transition.IsSuccess.Should().BeTrue();
        transition.Value!.ConfigurationVersionId.Should().BeNull();
        transition.Value.ConfigurationVersionNumber.Should().BeNull();
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase
            .Should().Be(TargetPhaseCode);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.ProjectId == fixture.Project.Id
                && entry.EntityType == "ProjectPhase" && entry.Action == "Change"
                && entry.Changes!.Contains("NotApplicable")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Category_readiness_matches_uploaded_source_template_and_uses_current_phase_category_for_every_target()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var sourceCategory = new DocumentCategory
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, OrganizationId = fixture.OrganizationId,
            Name = "Pre-Initiation documents", IsActive = true
        };
        var targetCategory = new DocumentCategory
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, OrganizationId = fixture.OrganizationId,
            Name = "Initiation documents", IsActive = true
        };
        var sourcePhase = await db.ProjectPhaseCatalogEntries.SingleAsync(item => item.Id == fixture.SourcePhaseId);
        sourcePhase.DocumentCategoryId = sourceCategory.Id;
        var targetPhase = await db.ProjectPhaseCatalogEntries.SingleAsync(item => item.Id == fixture.TargetPhaseId);
        targetPhase.DocumentCategoryId = targetCategory.Id;
        var alternateTargetId = Guid.NewGuid();
        db.ProjectPhaseCatalogEntries.Add(new ProjectPhaseCatalogEntry
        {
            Id = alternateTargetId, TenantId = fixture.TenantId, OrganizationId = fixture.OrganizationId,
            Code = "test-alternate-target", NormalizedCode = "TEST-ALTERNATE-TARGET",
            NameEn = "Alternate target", NameAr = "وجهة بديلة", DisplayOrder = 30, IsActive = true
        });
        db.DocumentCategories.AddRange(sourceCategory, targetCategory);
        var required = AddTemplate(fixture, sourceCategory.Id, "Project Charter", isMandatory: true);
        var optional = AddTemplate(fixture, sourceCategory.Id, "Optional Checklist", isMandatory: false);
        var targetOnly = AddTemplate(fixture, targetCategory.Id, "Initiation Plan", isMandatory: true);
        AddProjectDocument(fixture, required.Template.Id, required.Version.Id, required.Version.VersionNumber,
            "current-charter", organizationId: null);
        await db.SaveChangesAsync();

        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)Array.Empty<ProjectPhaseGateConfigurationDto>());
        var storage = new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");
        storage.Setup(item => item.GetMetadataAsync("current-charter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "current-charter", 10, "current-charter"));
        var service = CreateService(db, fixture, reader.Object, out _, includeChangePermission: false,
            storageProvider: storage);

        var readinessForTarget = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        var readinessForAlternateTarget = await service.GetAsync(fixture.Project.Id, alternateTargetId);

        readinessForTarget.IsSuccess.Should().BeTrue();
        readinessForAlternateTarget.IsSuccess.Should().BeTrue();
        foreach (var readiness in new[] { readinessForTarget.Value!, readinessForAlternateTarget.Value! })
        {
            readiness.CurrentPhaseId.Should().Be(fixture.SourcePhaseId);
            readiness.Requirements.Should().HaveCount(2);
            readiness.Requirements.Select(item => item.TemplateId).Should()
                .BeEquivalentTo(new[] { required.Template.Id, optional.Template.Id });
            readiness.Requirements.Should().NotContain(item => item.TemplateId == targetOnly.Template.Id);
            readiness.SatisfiedMandatoryCount.Should().Be(1);
            readiness.MissingMandatoryCount.Should().Be(0);
            readiness.OptionalRequirementCount.Should().Be(1);
            readiness.SatisfiedOptionalCount.Should().Be(0);
            readiness.ReadinessPercentage.Should().Be(100m);
            readiness.Requirements.Should().AllSatisfy(item => item.SupportsEvidenceLinking.Should().BeFalse());
        }
        readinessForTarget.Value!.Requirements.Single(item => item.TemplateId == required.Template.Id).Status
            .Should().Be(ProjectPhaseGateRequirementStatuses.Satisfied);
        readinessForAlternateTarget.Value!.Requirements.Single(item => item.TemplateId == optional.Template.Id).Status
            .Should().Be(ProjectPhaseGateRequirementStatuses.OptionalNotProvided);
    }

    [Fact]
    public async Task Category_readiness_rejects_wrong_versions_and_foreign_documents_then_refreshes_and_keeps_transition_non_blocking()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var category = new DocumentCategory
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, OrganizationId = null,
            Name = "Shared governance", IsActive = true
        };
        var phase = await db.ProjectPhaseCatalogEntries.SingleAsync(item => item.Id == fixture.SourcePhaseId);
        phase.DocumentCategoryId = category.Id;
        db.DocumentCategories.Add(category);
        var template = AddTemplate(fixture, category.Id, "Risk Register", isMandatory: true);
        AddProjectDocument(fixture, template.Template.Id, Guid.NewGuid(), template.Version.VersionNumber,
            "wrong-template-version", organizationId: null);
        AddProjectDocument(fixture, template.Template.Id, template.Version.Id, template.Version.VersionNumber,
            "foreign-organization-document", organizationId: Guid.NewGuid());
        await db.SaveChangesAsync();

        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)Array.Empty<ProjectPhaseGateConfigurationDto>());
        var storage = new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");
        storage.Setup(item => item.GetMetadataAsync("wrong-template-version", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "wrong-template-version", 10, "wrong"));
        storage.Setup(item => item.GetMetadataAsync("foreign-organization-document", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "foreign-organization-document", 10, "foreign"));
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: true,
            storageProvider: storage);

        var initial = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        initial.IsSuccess.Should().BeTrue();
        initial.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
        initial.Value.Requirements.Should().ContainSingle(item => item.Status == ProjectPhaseGateRequirementStatuses.Missing);
        initial.Value.MissingMandatoryCount.Should().Be(1);

        AddProjectDocument(fixture, template.Template.Id, template.Version.Id, template.Version.VersionNumber,
            "replacement-document", organizationId: null);
        await db.SaveChangesAsync();
        storage.Setup(item => item.GetMetadataAsync("replacement-document", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "replacement-document", 10, "replacement"));
        var afterUpload = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        afterUpload.IsSuccess.Should().BeTrue();
        afterUpload.Value!.Requirements.Single().Status.Should().Be(ProjectPhaseGateRequirementStatuses.Satisfied);
        afterUpload.Value.SatisfiedMandatoryCount.Should().Be(1);

        var replacementDocument = await db.Documents.SingleAsync(item => item.Name == "Evidence replacement-document");
        replacementDocument.IsArchived = true;
        replacementDocument.Status = "Archived";
        await db.SaveChangesAsync();
        var afterArchive = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        afterArchive.IsSuccess.Should().BeTrue();
        afterArchive.Value!.Requirements.Single().Status.Should().Be(ProjectPhaseGateRequirementStatuses.Missing);
        afterArchive.Value.MissingMandatoryCount.Should().Be(1);

        var transition = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);
        transition.IsSuccess.Should().BeTrue();
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase.Should().Be(TargetPhaseCode);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.EntityType == "ProjectPhase"
            && entry.Changes!.Contains("Incomplete")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Readiness_requires_project_and_document_view_and_honors_active_deny()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        var service = CreateService(db, fixture, reader.Object, out _, includeChangePermission: false,
            permissions: ["Project.View", "Document.View"], deniedPermissions: ["Project.View"]);

        var result = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(403);
        (await db.ProjectPhaseGateSnapshots.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Readiness_does_not_disclose_a_project_outside_current_management_scope()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        var service = CreateService(db, fixture, reader.Object, out _, includeChangePermission: false);

        var result = await service.GetAsync(Guid.NewGuid(), fixture.TargetPhaseId);

        result.IsFailure.Should().BeTrue();
        result.Error!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Phase_change_requires_canonical_permission_and_honors_active_deny()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        var withoutPermission = CreateService(db, fixture, reader.Object, out _, includeChangePermission: false);
        var deniedPermission = CreateService(db, fixture, reader.Object, out _, includeChangePermission: true,
            deniedPermissions: ["ProjectPhase.Change"]);
        var token = Convert.ToBase64String(fixture.Project.RowVersion);

        var missingGrant = await withoutPermission.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId, token, fixture.UserId);
        var activeDeny = await deniedPermission.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId, token, fixture.UserId);

        missingGrant.IsFailure.Should().BeTrue();
        missingGrant.Error!.StatusCode.Should().Be(403);
        activeDeny.IsFailure.Should().BeTrue();
        activeDeny.Error!.StatusCode.Should().Be(403);
        (await db.ProjectPhaseGateSnapshots.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("foreign-organization")]
    [InlineData("cross-tenant")]
    [InlineData("cross-project")]
    [InlineData("missing-storage")]
    public async Task Invalid_or_unavailable_document_evidence_never_satisfies_mandatory_requirement(string failureMode)
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var config = Config(fixture, version: 2, Requirement("Mandatory"));
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[config]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        var service = CreateService(db, fixture, reader.Object, out _, includeChangePermission: false);
        var initial = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        var requirement = initial.Value!.Requirements.Single();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var crossTenantDocument = failureMode == "cross-tenant";
        var document = new Document
        {
            Id = documentId,
            TenantId = crossTenantDocument ? Guid.NewGuid() : fixture.TenantId,
            ProjectId = crossTenantDocument ? null
                : failureMode == "cross-project" ? Guid.NewGuid() : fixture.Project.Id,
            OrganizationId = crossTenantDocument ? null
                : failureMode == "foreign-organization" ? Guid.NewGuid() : null,
            Name = "Evidence",
            FileName = "evidence.pdf",
            SourceTemplateId = crossTenantDocument ? null : requirement.TemplateId,
            SourceTemplateVersionId = crossTenantDocument ? null : requirement.TemplateVersionId,
            IsTemplate = false,
            IsArchived = false,
            IsDeleted = failureMode == "deleted",
            Status = "Active"
        };

        // A tenant-A evidence row cannot legally reference a tenant-B document.
        // Seed the foreign document under its own tenant and leave it unlinked so
        // tenant A's readiness query must continue to report the requirement as missing.
        if (crossTenantDocument)
            AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(document.TenantId, fixture.UserId));
        try
        {
            db.Documents.Add(document);
            db.DocumentVersions.Add(new DocumentVersion
            {
                Id = versionId,
                TenantId = document.TenantId,
                DocumentId = document.Id,
                Document = document,
                VersionNumber = 1,
                FileName = "evidence.pdf",
                FileSize = 10,
                StorageProviderId = "test",
                StorageKey = "missing-object",
                IsCurrent = true
            });
            await db.SaveChangesAsync();
        }
        finally
        {
            if (crossTenantDocument)
                AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(fixture.TenantId, fixture.UserId));
        }

        if (!crossTenantDocument)
        {
            db.ProjectPhaseGateEvidence.Add(new ProjectPhaseGateEvidence
            {
                TenantId = fixture.TenantId,
                ProjectId = fixture.Project.Id,
                OrganizationId = fixture.OrganizationId,
                RequirementSnapshotId = requirement.RequirementSnapshotId,
                DocumentId = documentId,
                DocumentVersionId = versionId,
                CreatedBy = fixture.UserId.ToString()
            });
            await db.SaveChangesAsync();
        }

        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);

        readiness.IsSuccess.Should().BeTrue();
        readiness.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.Incomplete);
        readiness.Value.MissingMandatoryCount.Should().Be(1);
        readiness.Value.Requirements.Single().Status.Should().Be(crossTenantDocument
            ? ProjectPhaseGateRequirementStatuses.Missing
            : ProjectPhaseGateRequirementStatuses.InvalidOrUnavailable);
    }

    [Fact]
    public async Task Optional_only_configuration_allows_transition_and_appends_existing_audit_event()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var config = Config(fixture, version: 3, Requirement("Optional"));
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[config]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: true);

        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        readiness.IsSuccess.Should().BeTrue();
        readiness.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.Ready);
        readiness.Value.ReadinessPercentage.Should().Be(100m);

        var staleTransition = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String([255]), fixture.UserId);
        staleTransition.IsFailure.Should().BeTrue();
        staleTransition.Error!.StatusCode.Should().Be(409);
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase
            .Should().Be(SourcePhaseCode);
        audit.Verify(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);

        var result = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            Convert.ToBase64String(fixture.Project.RowVersion), fixture.UserId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ConfigurationVersionId.Should().Be(config.VersionId);
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase
            .Should().Be(TargetPhaseCode);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.ProjectId == fixture.Project.Id
                && entry.EntityType == "ProjectPhase" && entry.Action == "Change"
                && entry.ChangedFields == "Phase" && entry.Changes!.Contains(config.VersionId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Exact_accessible_mandatory_document_version_satisfies_requirement_and_transition()
    {
        var fixture = await CreateProjectAsync();
        await using var db = fixture.Db;
        var config = Config(fixture, version: 5, Requirement("Mandatory"));
        var reader = new Mock<IProjectPhaseGateConfigurationReader>();
        reader.Setup(item => item.GetCurrentBySourceAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectPhaseGateConfigurationDto>)[config]);
        reader.Setup(item => item.GetCurrentAsync(fixture.TenantId, fixture.OrganizationId,
                fixture.SourcePhaseId, fixture.TargetPhaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        var storage = new Mock<IStorageProvider>();
        storage.Setup(item => item.GetMetadataAsync("private-evidence-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "private-evidence-key", 10, "checksum"));
        storage.Setup(item => item.GetMetadataAsync("pinned-template-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("test", "pinned-template-key", 10, "template-checksum"));
        storage.Setup(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[10], writable: false));
        var service = CreateService(db, fixture, reader.Object, out var audit, includeChangePermission: true,
            storageProvider: storage);
        var initial = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        var requirement = initial.Value!.Requirements.Single();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var document = new Document
        {
            Id = documentId, TenantId = fixture.TenantId, ProjectId = fixture.Project.Id,
            Name = "Business case evidence", FileName = "case.pdf", SourceTemplateId = requirement.TemplateId,
            SourceTemplateVersionId = requirement.TemplateVersionId, SourceTemplateVersionNumber = 1,
            IsTemplate = false, IsArchived = false, Status = "Active"
        };
        db.Documents.Add(document);
        db.DocumentVersions.Add(new DocumentVersion
        {
            Id = versionId, TenantId = fixture.TenantId, DocumentId = documentId, Document = document,
            VersionNumber = 1, FileName = "case.pdf", FileSize = 10, StorageProviderId = "test",
            StorageKey = "private-evidence-key", Sha256 = "checksum", IsCurrent = true
        });
        // Readiness validates the pinned approved template/version pair, so the fixture must
        // persist it instead of leaving the configuration's template references unresolved.
        var template = new Document
        {
            Id = requirement.TemplateId, TenantId = fixture.TenantId, Name = "Pinned approved template",
            FileName = "template.pdf", FileType = "PDF", OrganizationId = fixture.OrganizationId,
            IsTemplate = true, Status = "Active", Version = 1, CreatedBy = fixture.UserId.ToString()
        };
        db.Documents.Add(template);
        db.DocumentVersions.Add(new DocumentVersion
        {
            Id = requirement.TemplateVersionId, TenantId = fixture.TenantId, DocumentId = template.Id,
            Document = template, VersionNumber = 1, FileName = "template.pdf", FileSize = 10,
            StorageProviderId = "test", StorageKey = "pinned-template-key", Sha256 = "template-checksum",
            IsCurrent = true, CreatedBy = fixture.UserId.ToString()
        });
        db.ProjectPhaseGateEvidence.Add(new ProjectPhaseGateEvidence
        {
            TenantId = fixture.TenantId, ProjectId = fixture.Project.Id, OrganizationId = fixture.OrganizationId,
            RequirementSnapshotId = requirement.RequirementSnapshotId, DocumentId = documentId,
            DocumentVersionId = versionId, CreatedBy = fixture.UserId.ToString()
        });
        await db.SaveChangesAsync();

        var readiness = await service.GetAsync(fixture.Project.Id, fixture.TargetPhaseId);
        readiness.IsSuccess.Should().BeTrue();
        readiness.Value!.ReadinessStatus.Should().Be(ProjectPhaseGateReadinessStatuses.Ready);
        readiness.Value.SatisfiedMandatoryCount.Should().Be(1);
        readiness.Value.ReadinessPercentage.Should().Be(100m);
        readiness.Value.PhaseTransitionStatus.Should().Be(ProjectPhaseTransitionStatuses.AwaitingTransition);

        var transition = await service.ChangePhaseAsync(fixture.Project.Id, fixture.TargetPhaseId,
            readiness.Value.ProjectRowVersion, fixture.UserId);
        transition.IsSuccess.Should().BeTrue();
        (await db.Projects.SingleAsync(item => item.Id == fixture.Project.Id)).Phase
            .Should().Be(TargetPhaseCode);
        audit.Verify(item => item.AppendAsync(It.Is<AuditEntry>(entry => entry.ProjectId == fixture.Project.Id
                && entry.EntityType == "ProjectPhase" && entry.Action == "Change"
                && entry.ChangedFields == "Phase" && entry.Changes!.Contains(config.VersionId.ToString())
                && entry.Changes.Contains(documentId.ToString()) && entry.Changes.Contains(versionId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<ProjectFixture> CreateProjectAsync()
    {
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourcePhaseId = Guid.NewGuid();
        var targetPhaseId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase-gate-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        AppDbContext.ConfigureTenantResolution(CreateTenantAccessor(tenantId, userId));
        var portfolioId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PortfolioId = portfolioId,
            Name = "Lifecycle test", Code = $"P-{Guid.NewGuid():N}"[..10],
            ProjectPhaseId = sourcePhaseId, Phase = SourcePhaseCode, Status = "On Hold", RowVersion = [1]
        };
        db.Organizations.Add(new Organization
        {
            Id = organizationId, TenantId = tenantId, Name = "Lifecycle test organization", IsActive = true
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
            Code = "PORT-GATE-001", Name = "Test Portfolio", Currency = "GBP"
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
        Mock<IStorageProvider>? storageProvider = null,
        Guid? managementUserId = null)
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
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "phase-gate-test"))
            }
        };
        AppDbContext.ConfigureTenantResolution(http);
        var scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { fixture.Project.Id });
        var management = new ManagementContext(managementUserId ?? fixture.UserId, fixture.TenantId,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, fixture.Project.Id, scope);
        var managementAccessor = new Mock<IManagementContextAccessor>();
        managementAccessor.SetupGet(item => item.Current).Returns(management);
        audit = new Mock<IAuditService>();
        audit.Setup(item => item.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var documents = new Mock<IDocumentService>();
        var storage = storageProvider ?? new Mock<IStorageProvider>();
        storage.SetupGet(item => item.ProviderId).Returns("test");

        return new ProjectPhaseGateReadinessService(db, http, reader,
            documents.Object, storage.Object, audit.Object, new NEXUS.Persistence.ProjectOrganizationLookup(db), managementAccessor.Object);
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
                new Claim("permission", "Document.View")
            ], "phase-gate-test"))
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

    private static TemplateFixture AddTemplate(ProjectFixture fixture, Guid categoryId, string name, bool isMandatory)
    {
        var template = new Document
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, OrganizationId = fixture.OrganizationId,
            CategoryId = categoryId, Name = name, FileName = $"{name}.pdf", FileType = "PDF",
            IsTemplate = true, IsMandatory = isMandatory, Status = "Active", IsArchived = false, Version = 1
        };
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, DocumentId = template.Id, Document = template,
            VersionNumber = 1, FileName = template.FileName, FileSize = 10, StorageProviderId = "test",
            StorageKey = $"template-{template.Id:N}", IsCurrent = true
        };
        template.Versions.Add(version);
        fixture.Db.Documents.Add(template);
        return new TemplateFixture(template, version);
    }

    private static Document AddProjectDocument(
        ProjectFixture fixture,
        Guid sourceTemplateId,
        Guid sourceTemplateVersionId,
        int sourceVersionNumber,
        string storageKey,
        Guid? organizationId)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProjectId = fixture.Project.Id,
            OrganizationId = organizationId, Name = $"Evidence {storageKey}", FileName = "evidence.pdf",
            FileType = "PDF", SourceTemplateId = sourceTemplateId,
            SourceTemplateVersionId = sourceTemplateVersionId,
            SourceTemplateVersionNumber = sourceVersionNumber, IsTemplate = false,
            IsArchived = false, Status = "Active", Version = 1
        };
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, DocumentId = document.Id, Document = document,
            VersionNumber = 1, FileName = "evidence.pdf", FileSize = 10, StorageProviderId = "test",
            StorageKey = storageKey, Sha256 = storageKey, IsCurrent = true
        };
        document.Versions.Add(version);
        fixture.Db.Documents.Add(document);
        return document;
    }

    private sealed record ProjectFixture(
        AppDbContext Db,
        Guid TenantId,
        Guid OrganizationId,
        Guid UserId,
        Guid SourcePhaseId,
        Guid TargetPhaseId,
        Project Project);

    private sealed record TemplateFixture(Document Template, DocumentVersion Version);
}
