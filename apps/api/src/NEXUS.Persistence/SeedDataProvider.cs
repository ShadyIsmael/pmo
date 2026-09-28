using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public static class SeedDataProvider
{
    /// <summary>
    /// Demo tenant used by all seed data. Represents the single-tenant development
    /// deployment (no separate Tenant table exists; tenancy is expressed via TenantId).
    /// </summary>
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Documented local development password for all demo accounts.</summary>
    public const string DemoPassword = "Demo123!";

    private static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ExecutiveUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PortfolioManagerUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ProjectManagerUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid TeamMemberUserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid PmoAdminUserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid FinanceControllerUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ResourceManagerUserId = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly Guid SeedProjectMarkerId = Guid.Parse("e0000001-0000-0000-0000-000000000001");

    private static readonly Guid AdminRoleId = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid ExecutiveRoleId = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid PortfolioManagerRoleId = Guid.Parse("a0000003-0000-0000-0000-000000000003");
    private static readonly Guid ProjectManagerRoleId = Guid.Parse("a0000004-0000-0000-0000-000000000004");
    private static readonly Guid TeamMemberRoleId = Guid.Parse("a0000005-0000-0000-0000-000000000005");
    private static readonly Guid PmoAdminRoleId = Guid.Parse("a0000006-0000-0000-0000-000000000006");
    private static readonly Guid FinanceControllerRoleId = Guid.Parse("a0000007-0000-0000-0000-000000000007");
    private static readonly Guid ResourceManagerRoleId = Guid.Parse("a0000008-0000-0000-0000-000000000008");

    private static readonly Guid Portfolio1Id = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Portfolio2Id = Guid.Parse("a0000001-0000-0000-0000-000000000002");
    private static readonly Guid Portfolio3Id = Guid.Parse("a0000001-0000-0000-0000-000000000003");
    private static readonly Guid Portfolio4Id = Guid.Parse("a0000001-0000-0000-0000-000000000004");

    public static async Task SeedAsync(AppDbContext context)
    {
        // Passwords must never be seeded outside of Development.
        if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase))
            return;

        await SeedIdentityAsync(context);

        await EnsurePortfolioAndProgramSeedDataAsync(context);
        await EnsureOrganizationSeedDataAsync(context);
        await EnsureWorkItemStatusSeedDataAsync(context);
        await EnsureCurrencySeedDataAsync(context);
        await EnsureVendorAndOutsourcedSeedDataAsync(context);
        await EnsureDocumentCatalogAsync(context);
        // Project seed data used to be reset on every Development startup. That
        // reset cascaded into user-created Risks (and their workflow children),
        // leaving previously returned Risk IDs permanently stale after a restart.
        // Seed project data once and leave it, including user-owned records, intact.
        // An explicit opt-in remains available for rebuilding a disposable demo DB.
        var resetRequested = string.Equals(Environment.GetEnvironmentVariable("NEXUS_RESET_PROJECT_SEED"), "true", StringComparison.OrdinalIgnoreCase);
        var hasSeedProject = await context.Projects.IgnoreQueryFilters()
            .AnyAsync(project => project.TenantId == TenantId && project.Id == SeedProjectMarkerId);
        if (hasSeedProject && !resetRequested)
            return;

        if (resetRequested && hasSeedProject)
            await ResetProjectSeedDataAsync(context);

        var projectDefinitions = new[]
        {
            new { Id = Guid.Parse("e0000001-0000-0000-0000-000000000001"), Code = "PRJ-1001", Name = "Core Banking Modernization", Status = "InProgress", Priority = "Critical", Phase = "Implementation", Health = "Amber", RiskRating = "High", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 1, 15), EndDate = new DateTime(2026, 12, 15), Budget = 1800000m, Progress = 62m, Description = "Modernize the retail banking platform and replace legacy transaction services.", BusinessCase = "Increase processing resilience and reduce manual reconciliation across core ledgers.", Notes = "Critical regulatory timeline with executive oversight.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000001-0000-0000-0000-000000000001"), DepartmentId = Guid.Parse("a1000001-0000-0000-0000-000000000001") },
            new { Id = Guid.Parse("e0000002-0000-0000-0000-000000000002"), Code = "PRJ-1002", Name = "Enterprise ERP Implementation", Status = "Planning", Priority = "High", Phase = "Planning", Health = "Green", RiskRating = "Medium", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2027, 2, 28), Budget = 1450000m, Progress = 24m, Description = "Deploy a global ERP platform across finance, procurement, and supply chain.", BusinessCase = "Standardize operations and improve reporting consistency across business units.", Notes = "Scope prioritization currently underway.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000002-0000-0000-0000-000000000002"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
            new { Id = Guid.Parse("e0000003-0000-0000-0000-000000000003"), Code = "PRJ-1003", Name = "Customer Mobile Application", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Green", RiskRating = "Medium", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 3, 15), EndDate = new DateTime(2026, 10, 30), Budget = 980000m, Progress = 58m, Description = "Launch the next-generation mobile experience for retail and small business customers.", BusinessCase = "Improve digital adoption and reduce customer servicing costs.", Notes = "Release candidate is scheduled for August.", PortfolioId = Portfolio3Id, ProgramId = Guid.Parse("d0000006-0000-0000-0000-000000000006"), DepartmentId = Guid.Parse("a1000003-0000-0000-0000-000000000003") },
            new { Id = Guid.Parse("e0000004-0000-0000-0000-000000000004"), Code = "PRJ-1004", Name = "Cloud Infrastructure Migration", Status = "Completed", Priority = "Medium", Phase = "Deployment", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2025, 11, 1), EndDate = new DateTime(2026, 6, 30), Budget = 760000m, Progress = 100m, Description = "Migrate core workloads to the new cloud landing zone and modernize network services.", BusinessCase = "Reduce operational cost and improve disaster recovery readiness.", Notes = "Post-cutover stabilization completed successfully.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000004-0000-0000-0000-000000000004"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") },
            new { Id = Guid.Parse("e0000005-0000-0000-0000-000000000005"), Code = "PRJ-1005", Name = "Cybersecurity Improvement Program", Status = "OnHold", Priority = "Critical", Phase = "AnalysisAndDesign", Health = "Red", RiskRating = "High", ConfidenceLevel = "Red", StartDate = new DateTime(2026, 4, 1), EndDate = new DateTime(2027, 4, 15), Budget = 1250000m, Progress = 33m, Description = "Upgrade identity controls, endpoint protection, and security monitoring capabilities.", BusinessCase = "Strengthen compliance posture and reduce cyber exposure across the enterprise.", Notes = "Vendor onboarding is pending executive approval.", PortfolioId = Portfolio4Id, ProgramId = Guid.Parse("d0000007-0000-0000-0000-000000000007"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") },
            new { Id = Guid.Parse("e0000006-0000-0000-0000-000000000006"), Code = "PRJ-1006", Name = "Data Warehouse and Business Intelligence", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2027, 2, 15), Budget = 1100000m, Progress = 47m, Description = "Consolidate reporting datasets and deliver executive analytics using a modern warehouse platform.", BusinessCase = "Improve decision quality through trusted data and self-service analytics.", Notes = "Data quality remediation remains the main dependency.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000003-0000-0000-0000-000000000003"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
            new { Id = Guid.Parse("e0000007-0000-0000-0000-000000000007"), Code = "PRJ-1007", Name = "Digital Document Management", Status = "Planning", Priority = "Medium", Phase = "Planning", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2026, 11, 30), Budget = 620000m, Progress = 18m, Description = "Implement a centralized document repository with workflow automation and retention controls.", BusinessCase = "Improve compliance and reduce storage and retrieval costs.", Notes = "Current focus is policy alignment and integration strategy.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000002-0000-0000-0000-000000000002"), DepartmentId = Guid.Parse("a1000003-0000-0000-0000-000000000003") },
            new { Id = Guid.Parse("e0000008-0000-0000-0000-000000000008"), Code = "PRJ-1008", Name = "Contact Center Transformation", Status = "InProgress", Priority = "High", Phase = "Testing", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2027, 1, 31), Budget = 890000m, Progress = 71m, Description = "Upgrade the contact center with omnichannel routing and workforce management integrations.", BusinessCase = "Improve service levels and lower average handle times.", Notes = "Pilot group feedback is being incorporated into final workflows.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000005-0000-0000-0000-000000000005"), DepartmentId = Guid.Parse("a1000001-0000-0000-0000-000000000001") },
            new { Id = Guid.Parse("e0000009-0000-0000-0000-000000000009"), Code = "PRJ-1009", Name = "HR Management System", Status = "Closed", Priority = "Medium", Phase = "Closure", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 3, 31), Budget = 540000m, Progress = 100m, Description = "Deploy an integrated HR system for employee lifecycle management and analytics.", BusinessCase = "Streamline process automation and improve reporting across HR operations.", Notes = "Benefits tracking is in place and lessons learned were captured.", PortfolioId = Portfolio3Id, ProgramId = Guid.Parse("d0000006-0000-0000-0000-000000000006"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
            new { Id = Guid.Parse("e0000010-0000-0000-0000-000000000010"), Code = "PRJ-1010", Name = "Corporate Network Upgrade", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 5, 31), Budget = 1320000m, Progress = 39m, Description = "Upgrade campus and branch networking, security controls, and remote access connectivity.", BusinessCase = "Improve uptime, simplify support, and strengthen remote user experience.", Notes = "Hardware procurement is in progress and will gate the next wave.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000004-0000-0000-0000-000000000004"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") }
        };

        var projects = projectDefinitions.Select(definition => new Project
        {
            Id = definition.Id,
            TenantId = TenantId,
            Name = definition.Name,
            Code = definition.Code,
            Description = definition.Description,
            Status = definition.Status,
            Priority = definition.Priority,
            ProgramId = definition.ProgramId,
            PortfolioId = definition.PortfolioId,
            DepartmentId = definition.DepartmentId,
            ProjectManagerId = ProjectManagerUserId,
            SponsorId = ExecutiveUserId,
            StartDate = definition.StartDate,
            EndDate = definition.EndDate,
            Budget = definition.Budget,
            Currency = "GBP",
            Progress = definition.Progress,
            Health = definition.Health,
            RagStatus = definition.Health,
            ConfidenceLevel = definition.ConfidenceLevel,
            Phase = definition.Phase,
            RiskRating = definition.RiskRating,
            BusinessCase = definition.BusinessCase,
            Notes = definition.Notes,
            CreatedBy = "System"
        }).ToList();

        context.Projects.AddRange(projects);

        var projectManagerAssignments = new List<ProjectManagerAssignment>();
        var handoverProjects = new[]
        {
            new { Index = 0, PreviousManagerId = Guid.Parse("90000001-0000-0000-0000-000000000000"), HandoverDate = new DateTime(2026, 4, 1), Reason = "Reassigned to lead ERP program delivery" },
            new { Index = 3, PreviousManagerId = Guid.Parse("90000001-0001-0000-0000-000000000000"), HandoverDate = new DateTime(2026, 2, 1), Reason = "Transitioned to the infrastructure programme portfolio" }
        };

        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            var handover = handoverProjects.FirstOrDefault(h => h.Index == index);

            if (handover is null)
            {
                projectManagerAssignments.Add(new ProjectManagerAssignment
                {
                    Id = CreateDeterministicGuid("5f000001", index, 0, 5),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    ProjectManagerId = project.ProjectManagerId!.Value,
                    StartDate = project.StartDate!.Value,
                    IsCurrent = true,
                    CreatedBy = "System"
                });
            }
            else
            {
                projectManagerAssignments.Add(new ProjectManagerAssignment
                {
                    Id = CreateDeterministicGuid("5f000001", index, 0, 5),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    ProjectManagerId = handover.PreviousManagerId,
                    StartDate = project.StartDate!.Value,
                    EndDate = handover.HandoverDate,
                    IsCurrent = false,
                    CreatedBy = "System"
                });

                projectManagerAssignments.Add(new ProjectManagerAssignment
                {
                    Id = CreateDeterministicGuid("5f000001", index, 1, 5),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    ProjectManagerId = project.ProjectManagerId!.Value,
                    StartDate = handover.HandoverDate,
                    IsCurrent = true,
                    HandoverFromProjectManagerId = handover.PreviousManagerId,
                    HandoverDate = handover.HandoverDate,
                    HandoverReason = handover.Reason,
                    CreatedBy = "System"
                });
            }
        }

        // Project team seeding is best-effort: an empty catalog is a valid state, so
        // skip members/allocations instead of failing the whole seed.
        var resourceCatalog = await context.Resources.IgnoreQueryFilters().Where(resource => resource.TenantId == TenantId).OrderBy(resource => resource.Name).ToListAsync();
        var hasResourceCatalog = resourceCatalog.Count > 0;

        var projectTeamTemplates = new[]
        {
            new[] { ("Project Manager", 100m, 40m), ("Business Analyst", 60m, 20m), ("Technical Lead", 80m, 25m), ("Developer", 70m, 30m), ("QA Engineer", 40m, 15m) },
            new[] { ("Project Manager", 90m, 35m), ("Business Analyst", 55m, 18m), ("Technical Lead", 75m, 24m), ("Developer", 65m, 28m), ("QA Engineer", 35m, 12m) },
            new[] { ("Project Manager", 95m, 38m), ("Business Analyst", 50m, 18m), ("Technical Lead", 70m, 22m), ("Developer", 60m, 26m), ("QA Engineer", 30m, 10m) },
            new[] { ("Project Manager", 85m, 32m), ("Business Analyst", 45m, 16m), ("Technical Lead", 65m, 20m), ("Developer", 55m, 24m), ("QA Engineer", 30m, 10m) },
            new[] { ("Project Manager", 80m, 30m), ("Business Analyst", 40m, 14m), ("Technical Lead", 60m, 18m), ("Developer", 50m, 22m), ("QA Engineer", 25m, 8m) },
            new[] { ("Project Manager", 75m, 28m), ("Business Analyst", 35m, 12m), ("Technical Lead", 55m, 16m), ("Developer", 45m, 20m), ("QA Engineer", 20m, 8m) },
            new[] { ("Project Manager", 70m, 26m), ("Business Analyst", 30m, 12m), ("Technical Lead", 50m, 16m), ("Developer", 40m, 18m), ("QA Engineer", 25m, 8m) },
            new[] { ("Project Manager", 65m, 24m), ("Business Analyst", 25m, 10m), ("Technical Lead", 45m, 14m), ("Developer", 35m, 16m), ("QA Engineer", 20m, 8m) },
            new[] { ("Project Manager", 60m, 22m), ("Business Analyst", 20m, 8m), ("Technical Lead", 40m, 12m), ("Developer", 30m, 14m), ("QA Engineer", 15m, 6m) },
            new[] { ("Project Manager", 55m, 20m), ("Business Analyst", 20m, 8m), ("Technical Lead", 35m, 12m), ("Developer", 25m, 12m), ("QA Engineer", 15m, 6m) }
        };

        var projectMembers = new List<ProjectMember>();
        var projectAllocations = new List<ResourceAllocation>();
        var projectPhases = new List<ProjectPhase>();
        var projectMilestones = new List<Milestone>();

        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            var template = projectTeamTemplates[index];
            for (var memberIndex = 0; hasResourceCatalog && memberIndex < template.Length; memberIndex++)
            {
                var (roleName, allocationPercentage, hoursPerWeek) = template[memberIndex];
                var resource = resourceCatalog[(index + memberIndex) % resourceCatalog.Count];
                projectMembers.Add(new ProjectMember
                {
                    Id = CreateDeterministicGuid("f0000001", index, memberIndex, 1),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    ResourceId = resource.Id,
                    Role = roleName,
                    AllocationPercentage = allocationPercentage,
                    HoursPerWeek = hoursPerWeek,
                    StartDate = project.StartDate!.Value,
                    EndDate = project.EndDate!.Value,
                    Status = allocationPercentage >= 60m ? "Active" : "Tentative",
                    IsBillable = true,
                    Notes = $"{roleName} allocation for {project.Name}",
                    CreatedBy = "System"
                });

                projectAllocations.Add(new ResourceAllocation
{
                        Id = CreateDeterministicGuid("9f000001", index, memberIndex, 2),
                        TenantId = TenantId,
                        ResourceId = resource.Id,
                        ProjectId = project.Id,
                        ProjectName = project.Name,
                        ProjectRoleId = null,
                        AllocationPercentage = allocationPercentage,
                        HoursPerWeek = hoursPerWeek,
                        StartDate = project.StartDate!.Value,
                        EndDate = project.EndDate!.Value,
                        Status = ResourceAllocation.StatusConfirmed,
                        IsBillable = true,
                        Notes = $"Allocation for {project.Name}",
                        CreatedBy = "System"
                    });
            }

            var phaseNames = new[] { "Initiation", "Planning", "Analysis and Design", "Implementation", "Testing", "Deployment", "Closure" };
            for (var phaseIndex = 0; phaseIndex < 4; phaseIndex++)
            {
                projectPhases.Add(new ProjectPhase
                {
                    Id = CreateDeterministicGuid("8f000001", index, phaseIndex, 3),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    Name = phaseNames[phaseIndex],
                    Description = $"{project.Name} phase {phaseNames[phaseIndex]}",
                    SortOrder = phaseIndex + 1,
                    Status = phaseIndex < 3 ? "Complete" : "InProgress",
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    Progress = phaseIndex < 3 ? 100m : project.Progress,
                    CreatedBy = "System"
                });
            }

            var milestoneNames = new[] { "Kickoff", "Design Approval", "Go Live" };
            foreach (var milestoneName in milestoneNames)
            {
                projectMilestones.Add(new Milestone
                {
                    Id = CreateDeterministicGuid("7f000001", index, milestoneNames.ToList().IndexOf(milestoneName), 4),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    Name = $"{project.Name} - {milestoneName}",
                    DueDate = project.EndDate!.Value.AddDays(-30 + (projectMilestones.Count % 3) * 10),
                    Status = milestoneName == "Go Live" && project.Status == "Completed" ? "Completed" : milestoneName == "Go Live" ? "Planned" : "Completed",
                    Category = milestoneName == "Kickoff" ? "Gateway" : milestoneName == "Design Approval" ? "Delivery" : "Delivery",
                    Progress = milestoneName == "Go Live" ? project.Progress : 100m,
                    CreatedBy = "System"
                });
            }
        }

        // Ensure Alex Admin's canonical Resource (Mina Patel) has at least one Active ProjectMember
        // for the current work date so timer finalization can succeed.
        var adminResource = resourceCatalog.FirstOrDefault(r => r.UserId == AdminUserId);
        if (adminResource is not null)
        {
            var activeProject = projects.FirstOrDefault(p => p.Status == "InProgress" && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
                               ?? projects.FirstOrDefault(p => p.Status == "InProgress")
                               ?? projects.FirstOrDefault();
            if (activeProject is not null)
            {
                var existingAdminMember = projectMembers.FirstOrDefault(m => m.ResourceId == adminResource.Id && m.ProjectId == activeProject.Id);
                if (existingAdminMember is null)
                {
                    projectMembers.Add(new ProjectMember
                    {
                        Id = CreateDeterministicGuid("f0000100", 0, 0, 0),
                        TenantId = TenantId,
                        ProjectId = activeProject.Id,
                        ResourceId = adminResource.Id,
                        Role = "Project Administrator",
                        AllocationPercentage = 100m,
                        HoursPerWeek = 40m,
                        StartDate = activeProject.StartDate!.Value,
                        EndDate = activeProject.EndDate!.Value,
                        Status = "Active",
                        IsBillable = true,
                        Notes = "Admin resource membership for time tracking UAT",
                        CreatedBy = "System"
                    });
                }
                else
                {
                    existingAdminMember.Status = "Active";
                    existingAdminMember.AllocationPercentage = 100m;
                    existingAdminMember.HoursPerWeek = 40m;
                }
            }
        }

        context.ProjectMembers.AddRange(projectMembers);
        context.ResourceAllocations.AddRange(projectAllocations);
        context.ProjectPhases.AddRange(projectPhases);
        context.Milestones.AddRange(projectMilestones);
        context.ProjectManagerAssignments.AddRange(projectManagerAssignments);

        var projectSnapshots = new List<ProjectSnapshot>();
        foreach (var handover in handoverProjects)
        {
            var project = projects[handover.Index];
            var projectMembersForProject = projectMembers.Where(m => m.ProjectId == project.Id).ToList();
            var allocationsForProject = projectAllocations.Where(a => a.ProjectId == project.Id).ToList();
            var phasesForProject = projectPhases.Where(p => p.ProjectId == project.Id).ToList();
            var milestonesForProject = projectMilestones.Where(m => m.ProjectId == project.Id).ToList();

            var resourceNames = resourceCatalog.ToDictionary(r => r.Id, r => r.Name);

            projectSnapshots.Add(new ProjectSnapshot
            {
                Id = CreateDeterministicGuid("6f000001", handover.Index, 0, 6),
                TenantId = TenantId,
                ProjectId = project.Id,
                SnapshotNumber = 1,
                SnapshotName = "Handover Snapshot #1",
                SnapshotType = "ManagerHandover",
                SnapshotDate = handover.HandoverDate,
                EffectiveEndDate = handover.HandoverDate,
                PreviousProjectManagerId = handover.PreviousManagerId,
                NewProjectManagerId = project.ProjectManagerId!.Value,
                HandoverReason = handover.Reason,
                HandoverNotes = $"Seeded handover snapshot captured at {handover.HandoverDate:yyyy-MM-dd} when {handover.Reason}.",
                Name = project.Name,
                Code = project.Code,
                Description = project.Description,
                ProgramId = project.ProgramId,
                Status = project.Status,
                Priority = project.Priority,
                Category = project.Category,
                SponsorId = project.SponsorId,
                PortfolioId = project.PortfolioId,
                DepartmentId = project.DepartmentId,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                ActualStartDate = project.ActualStartDate,
                ActualEndDate = project.ActualEndDate,
                Budget = project.Budget,
                Currency = project.Currency,
                ActualCost = project.ActualCost,
                PlannedValue = project.PlannedValue,
                EarnedValue = project.EarnedValue,
                CostVariance = null,
                ScheduleVariance = null,
                Progress = project.Progress,
                Health = project.Health,
                RagStatus = project.RagStatus,
                ConfidenceLevel = project.ConfidenceLevel,
                Phase = project.Phase,
                RiskRating = project.RiskRating,
                BusinessCase = project.BusinessCase,
                Notes = project.Notes,
                Data = BuildSeedSnapshotData(project, projectMembersForProject, allocationsForProject, phasesForProject, milestonesForProject, resourceNames),
                CreatedBy = "System"
            });
        }
        context.ProjectSnapshots.AddRange(projectSnapshots);

        // Seed a single pending handover request on a project that does not already have a snapshot,
        // demonstrating the approval-based handover flow. The selected new manager is a seeded
        // project manager account (liam.chen@nexus.com / Demo123!).
        var pendingRequestProjectIndex = 2;
        var pendingProject = projects[pendingRequestProjectIndex];
        var pendingRequestExists = await context.ProjectHandoverRequests.IgnoreQueryFilters()
            .AnyAsync(r => r.ProjectId == pendingProject.Id, cancellationToken: default);
        if (!pendingRequestExists)
        {
            var pendingNewManagerId = Guid.Parse("90000001-0002-0000-0000-000000000000");
            var pendingRequest = new ProjectHandoverRequest
            {
                Id = CreateDeterministicGuid("5f000002", pendingRequestProjectIndex, 0, 6),
                TenantId = TenantId,
                ProjectId = pendingProject.Id,
                CurrentProjectManagerId = pendingProject.ProjectManagerId!.Value,
                NewProjectManagerId = pendingNewManagerId,
                EffectiveHandoverDate = new DateTime(2026, 9, 1),
                Reason = "Transitioning to a dedicated digital delivery lead",
                Notes = "Seeded pending handover request awaiting the new manager's acceptance.",
                Status = ProjectHandoverRequest.Statuses.Pending,
                RequestedBy = PmoAdminUserId,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
            context.ProjectHandoverRequests.Add(pendingRequest);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureWorkItemStatusSeedDataAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Code = "Backlog", NameEn = "Backlog", NameAr = "المتراكم", DisplayOrder = 10, Color = "slate", IsDefault = false },
            new { Code = "ToDo", NameEn = "To Do", NameAr = "للتنفيذ", DisplayOrder = 20, Color = "blue", IsDefault = true },
            new { Code = "InProgress", NameEn = "In Progress", NameAr = "قيد التنفيذ", DisplayOrder = 30, Color = "amber", IsDefault = false },
            new { Code = "InReview", NameEn = "In Review", NameAr = "قيد المراجعة", DisplayOrder = 40, Color = "violet", IsDefault = false },
            new { Code = "Done", NameEn = "Done", NameAr = "مكتمل", DisplayOrder = 50, Color = "emerald", IsDefault = false },
            new { Code = "Cancelled", NameEn = "Cancelled", NameAr = "ملغى", DisplayOrder = 60, Color = "rose", IsDefault = false },
            new { Code = "OnHold", NameEn = "On Hold", NameAr = "معلّق", DisplayOrder = 70, Color = "orange", IsDefault = false }
        };

        var statuses = await context.Lookups.IgnoreQueryFilters()
            .Where(lookup => lookup.TenantId == TenantId && lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted)
            .ToListAsync();

        foreach (var definition in definitions)
        {
            var status = statuses.FirstOrDefault(candidate => candidate.Code == definition.Code);
            if (status is null)
            {
                context.Lookups.Add(new Lookup
                {
                    Id = CreateDeterministicGuid("b0000001", Array.IndexOf(definitions, definition), 0, 7),
                    TenantId = TenantId,
                    Category = ReferenceDataCategories.WorkItemStatus,
                    Code = definition.Code,
                    Name = definition.NameEn,
                    NameEn = definition.NameEn,
                    NameAr = definition.NameAr,
                    DisplayOrder = definition.DisplayOrder,
                    IsActive = true,
                    Color = definition.Color,
                    IsDefault = definition.IsDefault,
                    CreatedBy = "System"
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(status.NameEn)) status.NameEn = status.Name;
                if (string.IsNullOrWhiteSpace(status.NameAr)) status.NameAr = status.NameEn;
            }
        }

        await context.SaveChangesAsync();

        var activeStatuses = await context.Lookups
            .Where(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && lookup.IsActive && !lookup.IsDeleted)
            .OrderBy(lookup => lookup.DisplayOrder)
            .ThenBy(lookup => lookup.Code)
            .ToListAsync();
        var defaultStatus = activeStatuses.FirstOrDefault(status => status.IsDefault) ?? activeStatuses.FirstOrDefault(status => status.Code == "ToDo") ?? activeStatuses.FirstOrDefault();
        if (defaultStatus is not null)
        {
            foreach (var status in activeStatuses) status.IsDefault = status.Id == defaultStatus.Id;
            await context.SaveChangesAsync();
        }
    }

    private static string BuildSeedSnapshotData(
        Project project,
        List<ProjectMember> members,
        List<ResourceAllocation> allocations,
        List<ProjectPhase> phases,
        List<Milestone> milestones,
        Dictionary<Guid, string> resourceNames)
    {
        var data = new ProjectSnapshotDataDto
        {
            Members = members.Select(m => new SnapshotMemberDto
            {
                OriginalId = m.Id,
                ResourceId = m.ResourceId,
                ResourceName = resourceNames.TryGetValue(m.ResourceId, out var name) ? name : null,
                Role = m.Role,
                AllocationPercentage = m.AllocationPercentage,
                HoursPerWeek = m.HoursPerWeek,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                Status = m.Status,
                IsBillable = m.IsBillable,
                Notes = m.Notes
            }).ToList(),
            Allocations = allocations.Select(a => new SnapshotAllocationDto
            {
                OriginalId = a.Id,
                ResourceId = a.ResourceId,
                ResourceName = resourceNames.TryGetValue(a.ResourceId, out var name) ? name : null,
                ProjectRoleId = a.ProjectRoleId,
                AllocationPercentage = a.AllocationPercentage,
                HoursPerWeek = a.HoursPerWeek,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                Status = a.Status,
                IsBillable = a.IsBillable,
                Notes = a.Notes
            }).ToList(),
            Phases = phases.Select(p => new SnapshotPhaseDto
            {
                OriginalId = p.Id,
                Name = p.Name,
                Description = p.Description,
                SortOrder = p.SortOrder,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Progress = p.Progress
            }).ToList(),
            Milestones = milestones.Select(m => new SnapshotMilestoneDto
            {
                OriginalId = m.Id,
                Name = m.Name,
                DueDate = m.DueDate,
                CompletedDate = m.CompletedDate,
                Status = m.Status,
                Progress = m.Progress,
                Category = m.Category,
                AssigneeId = m.AssigneeId
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    private static Guid CreateDeterministicGuid(string prefix, int primary, int secondary, int tertiary)
    {
        var suffix = (primary + 1) * 100000 + (secondary + 1) * 1000 + tertiary;
        return Guid.Parse($"{prefix}-{primary:D4}-{secondary:D4}-0000-{suffix:D12}");
    }

    private static async Task ResetProjectSeedDataAsync(AppDbContext context)
    {
        var projectIds = await context.Projects.IgnoreQueryFilters()
            .Where(project => project.TenantId == TenantId)
            .Select(project => project.Id)
            .ToListAsync();

        if (projectIds.Count == 0)
        {
            return;
        }

        var projectAllocations = await context.ResourceAllocations.IgnoreQueryFilters()
            .Where(allocation => allocation.TenantId == TenantId && allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value))
            .ToListAsync();
        if (projectAllocations.Count > 0)
        {
            context.ResourceAllocations.RemoveRange(projectAllocations);
        }

        var workItems = await context.WorkItems.IgnoreQueryFilters()
            .Where(workItem => workItem.TenantId == TenantId && workItem.ProjectId.HasValue && projectIds.Contains(workItem.ProjectId.Value))
            .ToListAsync();
        if (workItems.Count > 0)
        {
            context.WorkItems.RemoveRange(workItems);
        }

        var projectMembers = await context.ProjectMembers.IgnoreQueryFilters()
            .Where(member => member.TenantId == TenantId && projectIds.Contains(member.ProjectId))
            .ToListAsync();
        if (projectMembers.Count > 0)
        {
            context.ProjectMembers.RemoveRange(projectMembers);
        }

        var managerAssignments = await context.ProjectManagerAssignments.IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == TenantId && projectIds.Contains(assignment.ProjectId))
            .ToListAsync();
        if (managerAssignments.Count > 0)
        {
            context.ProjectManagerAssignments.RemoveRange(managerAssignments);
        }

        var projectSnapshots = await context.ProjectSnapshots.IgnoreQueryFilters()
            .Where(snapshot => snapshot.TenantId == TenantId && projectIds.Contains(snapshot.ProjectId))
            .ToListAsync();
        if (projectSnapshots.Count > 0)
        {
            context.ProjectSnapshots.RemoveRange(projectSnapshots);
        }

        var projectHandoverRequests = await context.ProjectHandoverRequests.IgnoreQueryFilters()
            .Where(request => request.TenantId == TenantId && projectIds.Contains(request.ProjectId))
            .ToListAsync();
        if (projectHandoverRequests.Count > 0)
        {
            context.ProjectHandoverRequests.RemoveRange(projectHandoverRequests);
        }

        var projectPhases = await context.ProjectPhases.IgnoreQueryFilters()
            .Where(phase => phase.TenantId == TenantId && projectIds.Contains(phase.ProjectId))
            .ToListAsync();
        if (projectPhases.Count > 0)
        {
            context.ProjectPhases.RemoveRange(projectPhases);
        }

        var milestones = await context.Milestones.IgnoreQueryFilters()
            .Where(milestone => milestone.TenantId == TenantId && projectIds.Contains(milestone.ProjectId))
            .ToListAsync();
        if (milestones.Count > 0)
        {
            context.Milestones.RemoveRange(milestones);
        }

        var projectStakeholders = await context.ProjectStakeholders.IgnoreQueryFilters()
            .Where(stakeholder => stakeholder.TenantId == TenantId && projectIds.Contains(stakeholder.ProjectId))
            .ToListAsync();
        if (projectStakeholders.Count > 0)
        {
            context.ProjectStakeholders.RemoveRange(projectStakeholders);
        }

        var projectSchedules = await context.ProjectSchedules.IgnoreQueryFilters()
            .Where(schedule => schedule.TenantId == TenantId && projectIds.Contains(schedule.ProjectId))
            .ToListAsync();
        if (projectSchedules.Count > 0)
        {
            context.ProjectSchedules.RemoveRange(projectSchedules);
        }

        var workBreakdownStructures = await context.WorkBreakdownStructures.IgnoreQueryFilters()
            .Where(wbs => wbs.TenantId == TenantId && projectIds.Contains(wbs.ProjectId))
            .ToListAsync();
        if (workBreakdownStructures.Count > 0)
        {
            context.WorkBreakdownStructures.RemoveRange(workBreakdownStructures);
        }

        var baselines = await context.Baselines.IgnoreQueryFilters()
            .Where(baseline => baseline.TenantId == TenantId && projectIds.Contains(baseline.ProjectId))
            .ToListAsync();
        if (baselines.Count > 0)
        {
            context.Baselines.RemoveRange(baselines);
        }

        var deliverables = await context.Deliverables.IgnoreQueryFilters()
            .Where(deliverable => deliverable.TenantId == TenantId && projectIds.Contains(deliverable.ProjectId))
            .ToListAsync();
        if (deliverables.Count > 0)
        {
            context.Deliverables.RemoveRange(deliverables);
        }

        var statusReports = await context.ProjectStatusReports.IgnoreQueryFilters()
            .Where(report => report.TenantId == TenantId && projectIds.Contains(report.ProjectId))
            .ToListAsync();
        if (statusReports.Count > 0)
        {
            context.ProjectStatusReports.RemoveRange(statusReports);
        }

        var issues = await context.Issues.IgnoreQueryFilters()
            .Where(issue => issue.TenantId == TenantId && issue.ProjectId.HasValue && projectIds.Contains(issue.ProjectId.Value))
            .ToListAsync();
        if (issues.Count > 0)
        {
            context.Issues.RemoveRange(issues);
        }

        var risks = await context.Risks.IgnoreQueryFilters()
            .Where(risk => risk.TenantId == TenantId && projectIds.Contains(risk.ProjectId))
            .ToListAsync();
        if (risks.Count > 0)
        {
            context.Risks.RemoveRange(risks);
        }

        var meetings = await context.Meetings.IgnoreQueryFilters()
            .Where(meeting => meeting.TenantId == TenantId && meeting.ProjectId.HasValue && projectIds.Contains(meeting.ProjectId.Value))
            .ToListAsync();
        if (meetings.Count > 0)
        {
            context.Meetings.RemoveRange(meetings);
        }

        var budgets = await context.Budgets.IgnoreQueryFilters()
            .Where(budget => budget.TenantId == TenantId && budget.ProjectId.HasValue && projectIds.Contains(budget.ProjectId.Value))
            .ToListAsync();
        if (budgets.Count > 0)
        {
            context.Budgets.RemoveRange(budgets);
        }

        var costEntries = await context.CostEntries.IgnoreQueryFilters()
            .Where(entry => entry.TenantId == TenantId && entry.ProjectId.HasValue && projectIds.Contains(entry.ProjectId.Value))
            .ToListAsync();
        if (costEntries.Count > 0)
        {
            context.CostEntries.RemoveRange(costEntries);
        }

        var documents = await context.Documents.IgnoreQueryFilters()
            .Where(document => document.TenantId == TenantId && document.ProjectId.HasValue && projectIds.Contains(document.ProjectId.Value))
            .ToListAsync();
        if (documents.Count > 0)
        {
            context.Documents.RemoveRange(documents);
        }

        var projectEntities = await context.Projects.IgnoreQueryFilters()
            .Where(project => project.TenantId == TenantId && projectIds.Contains(project.Id))
            .ToListAsync();
        if (projectEntities.Count > 0)
        {
            context.Projects.RemoveRange(projectEntities);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsurePortfolioAndProgramSeedDataAsync(AppDbContext context)
    {
        if (!await context.Portfolios.IgnoreQueryFilters().AnyAsync(portfolio => portfolio.Id == Portfolio1Id && portfolio.TenantId == TenantId))
        {
            var portfolios = new List<Portfolio>
            {
                new() { Id = Portfolio1Id, TenantId = TenantId, Name = "Digital Transformation", Code = "DIGITAL", Description = "Digital transformation initiatives", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 5000000m, ActualSpend = 1250000m, Health = "Green", Priority = "Critical", CreatedBy = "System" },
                new() { Id = Portfolio2Id, TenantId = TenantId, Name = "Infrastructure & Operations", Code = "INFRA", Description = "Infrastructure and operations projects", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 3500000m, ActualSpend = 2100000m, Health = "Amber", Priority = "High", CreatedBy = "System" },
                new() { Id = Portfolio3Id, TenantId = TenantId, Name = "Product Innovation", Code = "INNOVATE", Description = "New product development and innovation", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 2500000m, ActualSpend = 800000m, Health = "Green", Priority = "High", CreatedBy = "System" },
                new() { Id = Portfolio4Id, TenantId = TenantId, Name = "Compliance & Security", Code = "COMPLY", Description = "Regulatory compliance and security", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 1800000m, ActualSpend = 950000m, Health = "Red", Priority = "Critical", CreatedBy = "System" }
            };
            context.Portfolios.AddRange(portfolios);
        }

        if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().IgnoreQueryFilters().AnyAsync(program => program.Id == Guid.Parse("d0000001-0000-0000-0000-000000000001") && program.TenantId == TenantId))
        {
            var programs = new List<global::NEXUS.Modules.Portfolio.Domain.Program>
            {
                new() { Id = Guid.Parse("d0000001-0000-0000-0000-000000000001"), TenantId = TenantId, Name = "Cloud Migration Program", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 2000000m, ActualSpend = 450000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000002-0000-0000-0000-000000000002"), TenantId = TenantId, Name = "ERP Modernisation", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 1800000m, ActualSpend = 600000m, Health = "Amber", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000003-0000-0000-0000-000000000003"), TenantId = TenantId, Name = "Data & Analytics", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 1200000m, ActualSpend = 200000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000004-0000-0000-0000-000000000004"), TenantId = TenantId, Name = "Network Infrastructure", PortfolioId = Portfolio2Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 1500000m, ActualSpend = 900000m, Health = "Amber", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000005-0000-0000-0000-000000000005"), TenantId = TenantId, Name = "Office Modernisation", PortfolioId = Portfolio2Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 2000000m, ActualSpend = 1200000m, Health = "Red", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000006-0000-0000-0000-000000000006"), TenantId = TenantId, Name = "Product Launch Program", PortfolioId = Portfolio3Id, Status = "Planning", ProgramManagerId = ProjectManagerUserId, Budget = 2500000m, ActualSpend = 800000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000007-0000-0000-0000-000000000007"), TenantId = TenantId, Name = "Security Enhancement Program", PortfolioId = Portfolio4Id, Status = "Active", ProgramManagerId = ProjectManagerUserId, Budget = 1800000m, ActualSpend = 950000m, Health = "Red", CreatedBy = "System" }
            };
            context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().AddRange(programs);
        }

        var seededPortfolios = await context.Portfolios.IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.OwnerId.HasValue && !x.IsDeleted).ToListAsync();
        foreach (var portfolio in seededPortfolios)
        {
            if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment>().IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId && x.PortfolioId == portfolio.Id && x.IsCurrent))
                context.Set<global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment>().Add(new global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment { TenantId = TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = portfolio.OwnerId!.Value, StartDate = portfolio.StartDate ?? portfolio.CreatedAt, IsCurrent = true, CreatedBy = "System" });
        }

        var seededPrograms = await context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.ProgramManagerId.HasValue && !x.IsDeleted).ToListAsync();
        foreach (var program in seededPrograms)
        {
            if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment>().IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId && x.ProgramId == program.Id && x.IsCurrent))
                context.Set<global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment>().Add(new global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment { TenantId = TenantId, ProgramId = program.Id, ProgramManagerId = program.ProgramManagerId!.Value, StartDate = program.StartDate ?? program.CreatedAt, IsCurrent = true, CreatedBy = "System" });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureOrganizationSeedDataAsync(AppDbContext context)
    {
        var organizationId = Guid.Parse("90000001-0000-0000-0000-000000000001");
        var businessUnitId = Guid.Parse("90000002-0000-0000-0000-000000000001");
        var departmentIds = new[]
        {
            Guid.Parse("a1000001-0000-0000-0000-000000000001"),
            Guid.Parse("a1000002-0000-0000-0000-000000000002"),
            Guid.Parse("a1000003-0000-0000-0000-000000000003"),
            Guid.Parse("a1000004-0000-0000-0000-000000000004")
        };

        if (!await context.Organizations.IgnoreQueryFilters().AnyAsync(organization => organization.Id == organizationId && organization.TenantId == TenantId))
        {
            context.Organizations.Add(new Organization
            {
                Id = organizationId,
                TenantId = TenantId,
                Name = "NEXUS Group",
                Code = "NEXUS",
                Description = "Enterprise operating organization",
                CreatedBy = "System"
            });
        }

        if (!await context.BusinessUnits.IgnoreQueryFilters().AnyAsync(businessUnit => businessUnit.Id == businessUnitId && businessUnit.TenantId == TenantId))
        {
            context.BusinessUnits.Add(new BusinessUnit
            {
                Id = businessUnitId,
                TenantId = TenantId,
                OrganizationId = organizationId,
                Name = "Technology",
                Code = "TECH",
                Description = "Technology and transformation business unit",
                CreatedBy = "System"
            });
        }

        if (!await context.Departments.IgnoreQueryFilters().AnyAsync(department => department.Id == departmentIds[0] && department.TenantId == TenantId))
        {
            context.Departments.AddRange(new[]
            {
                new Department { Id = departmentIds[0], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Engineering", Code = "ENG", Description = "Engineering and delivery", CreatedBy = "System" },
                new Department { Id = departmentIds[1], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Operations", Code = "OPS", Description = "Operations and service delivery", CreatedBy = "System" },
                new Department { Id = departmentIds[2], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Product", Code = "PRD", Description = "Product and customer experience", CreatedBy = "System" },
                new Department { Id = departmentIds[3], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Security", Code = "SEC", Description = "Security and compliance", CreatedBy = "System" }
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureCurrencySeedDataAsync(AppDbContext context)
    {
        var currencyDefinitions = new[]
        {
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000001"), Code = "GBP", Name = "Pound Sterling", Symbol = "£", IsBase = true },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000002"), Code = "USD", Name = "US Dollar", Symbol = "$", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000003"), Code = "EUR", Name = "Euro", Symbol = "€", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000004"), Code = "AED", Name = "UAE Dirham", Symbol = "د.إ", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000005"), Code = "EGP", Name = "Egyptian Pound", Symbol = "ج.م", IsBase = false }
        };

        foreach (var definition in currencyDefinitions)
        {
            if (await context.Currencies.IgnoreQueryFilters().AnyAsync(currency => currency.Id == definition.Id && currency.TenantId == TenantId))
                continue;

            context.Currencies.Add(new Currency
            {
                Id = definition.Id,
                TenantId = TenantId,
                Code = definition.Code,
                Name = definition.Name,
                Symbol = definition.Symbol,
                ExchangeRate = 1m,
                IsBase = definition.IsBase,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the canonical VendorCategory reference-data set (Supplier, Consultant,
    /// Technology, Outsourcing, Other) using deterministic ids. Idempotent: re-running
    /// the seed never duplicates or overwrites categories managed through Reference Data.
    /// </summary>
    private static async Task EnsureVendorCategorySeedDataAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Code = "SUP", NameEn = "Supplier", NameAr = "مورّد", DisplayOrder = 10 },
            new { Code = "CON", NameEn = "Consultant", NameAr = "مستشار", DisplayOrder = 20 },
            new { Code = "TEC", NameEn = "Technology", NameAr = "تقنية", DisplayOrder = 30 },
            new { Code = "OUT", NameEn = "Outsourcing", NameAr = "تعاقد خارجي", DisplayOrder = 40 },
            new { Code = "OTH", NameEn = "Other", NameAr = "أخرى", DisplayOrder = 50 }
        };

        var existing = await context.Lookups.IgnoreQueryFilters()
            .Where(lookup => lookup.TenantId == TenantId && lookup.Category == ReferenceDataCategories.VendorCategory && !lookup.IsDeleted)
            .ToListAsync();

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            if (existing.Any(candidate => candidate.Code == definition.Code))
                continue;

            context.Lookups.Add(new Lookup
            {
                Id = CreateDeterministicGuid("b0000002", index, 0, 7),
                TenantId = TenantId,
                Category = ReferenceDataCategories.VendorCategory,
                Code = definition.Code,
                Name = definition.NameEn,
                NameEn = definition.NameEn,
                NameAr = definition.NameAr,
                DisplayOrder = definition.DisplayOrder,
                IsActive = true,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds vendor reference data only: the canonical VendorCategory set and the Skill
    /// catalog. No demo vendors, outsourced resources, contracts, or resource-skill links are
    /// created, so a fresh tenant starts with an empty vendor and resource list. Rows left
    /// behind by an earlier seed revision are purged so the demo data cannot come back.
    /// Idempotent: reference data is only added when missing.
    /// </summary>
    private static async Task EnsureVendorAndOutsourcedSeedDataAsync(AppDbContext context)
    {
        await EnsureVendorCategorySeedDataAsync(context);
        await EnsureSkillSeedDataAsync(context);

        await PurgeSeededDemoDataAsync(context);
    }

    /// <summary>
    /// Hard-deletes the demo rows created by earlier seed revisions: the CloudNova and
    /// DataWorks vendors, the six internal employee resources, the ten outsourced resources,
    /// and the contracts, allocations, project memberships and resource-skill links that
    /// pointed at them. Matching is done on the deterministic seed ids only, so resources
    /// and vendors created by users are never touched.
    /// </summary>
    private static async Task PurgeSeededDemoDataAsync(AppDbContext context)
    {
        var seededVendorIds = new[]
        {
            Guid.Parse("b3000001-0000-0000-0000-000000000001"),
            Guid.Parse("b3000001-0000-0000-0000-000000000002")
        };

        var seededResourceIds = Enumerable.Range(1, 6)
            .Select(i => Guid.Parse($"70000001-0000-0000-0000-{i:D12}"))
            .Concat(Enumerable.Range(1, 10).Select(i => Guid.Parse($"70000002-0000-0000-0000-{i:D12}")))
            .ToList();

        var resources = await context.Resources.IgnoreQueryFilters()
            .Where(r => r.TenantId == TenantId && seededResourceIds.Contains(r.Id))
            .ToListAsync();

        var seededResourceIdSet = seededResourceIds.ToHashSet();

        var skillLinks = await context.ResourceSkills.IgnoreQueryFilters()
            .Where(rs => rs.TenantId == TenantId && seededResourceIdSet.Contains(rs.ResourceId))
            .ToListAsync();
        context.ResourceSkills.RemoveRange(skillLinks);

        var contracts = await context.Contracts.IgnoreQueryFilters()
            .Where(c => c.TenantId == TenantId && seededResourceIdSet.Contains(c.ResourceId))
            .ToListAsync();
        context.Contracts.RemoveRange(contracts);

        var allocations = await context.ResourceAllocations.IgnoreQueryFilters()
            .Where(a => a.TenantId == TenantId && seededResourceIdSet.Contains(a.ResourceId))
            .ToListAsync();
        context.ResourceAllocations.RemoveRange(allocations);

        var members = await context.ProjectMembers.IgnoreQueryFilters()
            .Where(m => m.TenantId == TenantId && seededResourceIdSet.Contains(m.ResourceId))
            .ToListAsync();
        context.ProjectMembers.RemoveRange(members);

        context.Resources.RemoveRange(resources);

        var vendors = await context.Vendors.IgnoreQueryFilters()
            .Where(v => v.TenantId == TenantId && seededVendorIds.Contains(v.Id))
            .ToListAsync();
        context.Vendors.RemoveRange(vendors);

        if (skillLinks.Count > 0 || contracts.Count > 0 || allocations.Count > 0 || members.Count > 0 || resources.Count > 0 || vendors.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task EnsureSkillSeedDataAsync(AppDbContext context)
    {
        var skillDefinitions = new[]
        {
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000001"), Name = "Cloud Architecture", Category = "Infrastructure", Description = "Design of scalable cloud solutions" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000002"), Name = "Kubernetes", Category = "Infrastructure", Description = "Container orchestration" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000003"), Name = "Terraform", Category = "Infrastructure", Description = "Infrastructure as code" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000004"), Name = "CI/CD", Category = "Engineering", Description = "Continuous integration and delivery pipelines" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000005"), Name = "SQL", Category = "Data", Description = "Relational database querying and modelling" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000006"), Name = "Data Engineering", Category = "Data", Description = "Building and maintaining data pipelines" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000007"), Name = "ETL", Category = "Data", Description = "Extract, transform, load processes" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000008"), Name = "Power BI", Category = "Data", Description = "Business intelligence reporting" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000009"), Name = "Python", Category = "Development", Description = "General purpose programming language" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000010"), Name = "Networking", Category = "Infrastructure", Description = "Network design and operations" }
        };

        var existingSkills = await context.Skills.IgnoreQueryFilters().Where(s => s.TenantId == TenantId).ToListAsync();
        foreach (var definition in skillDefinitions)
        {
            if (existingSkills.Any(s => s.Name == definition.Name))
                continue;

            context.Skills.Add(new Skill
            {
                Id = definition.Id,
                TenantId = TenantId,
                Name = definition.Name,
                Category = definition.Category,
                Description = definition.Description,
                IsActive = true,
                CreatedBy = "System"
            });
        }
        await context.SaveChangesAsync();

    }

    public static async Task SeedIdentityOnlyAsync(AppDbContext context)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase))
            return;

        await SeedIdentityAsync(context);
    }

    private static async Task SeedIdentityAsync(AppDbContext context)
    {
        var passwordHasher = new PasswordHasher<User>();
        var passwordHash = passwordHasher.HashPassword(new User(), DemoPassword);

        var existingRoles = await context.Roles.Include(r => r.Permissions).ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var existingUsers = await context.Users.ToListAsync();
        var existingAssignments = await context.UserRoles.ToListAsync();

        // Permissions: legacy set plus canonical Resource permissions referenced by
        // the [Authorize(Policy = "...")] policies in Program.cs.
        var permissions = new List<Permission>
        {
            new() { Id = Guid.Parse("b0000001-0000-0000-0000-000000000001"), TenantId = TenantId, Name = "projects.read", Resource = "projects", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000002-0000-0000-0000-000000000002"), TenantId = TenantId, Name = "projects.write", Resource = "projects", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000003-0000-0000-0000-000000000003"), TenantId = TenantId, Name = "projects.delete", Resource = "projects", Action = "delete", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000004-0000-0000-0000-000000000004"), TenantId = TenantId, Name = "portfolios.read", Resource = "portfolios", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000005-0000-0000-0000-000000000005"), TenantId = TenantId, Name = "portfolios.write", Resource = "portfolios", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000006-0000-0000-0000-000000000006"), TenantId = TenantId, Name = "resources.read", Resource = "resources", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000007-0000-0000-0000-000000000007"), TenantId = TenantId, Name = "resources.write", Resource = "resources", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000008-0000-0000-0000-000000000008"), TenantId = TenantId, Name = "users.read", Resource = "users", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000009-0000-0000-0000-000000000009"), TenantId = TenantId, Name = "users.write", Resource = "users", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000010-0000-0000-0000-000000000010"), TenantId = TenantId, Name = "finances.read", Resource = "finances", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000011-0000-0000-0000-000000000011"), TenantId = TenantId, Name = "finances.write", Resource = "finances", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000012-0000-0000-0000-000000000012"), TenantId = TenantId, Name = "reports.read", Resource = "reports", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000013-0000-0000-0000-000000000013"), TenantId = TenantId, Name = "reports.export", Resource = "reports", Action = "export", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000014-0000-0000-0000-000000000014"), TenantId = TenantId, Name = "settings.read", Resource = "settings", Action = "read", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000015-0000-0000-0000-000000000015"), TenantId = TenantId, Name = "settings.write", Resource = "settings", Action = "write", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000016-0000-0000-0000-000000000016"), TenantId = TenantId, Name = "Resource.View", Resource = "Resource", Action = "View", Description = "View resources", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000017-0000-0000-0000-000000000017"), TenantId = TenantId, Name = "Resource.Assign", Resource = "Resource", Action = "Assign", Description = "Assign resources to projects", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000018-0000-0000-0000-000000000018"), TenantId = TenantId, Name = "Resource.Allocate", Resource = "Resource", Action = "Allocate", Description = "Allocate resources", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000019-0000-0000-0000-000000000019"), TenantId = TenantId, Name = "Resource.Manage", Resource = "Resource", Action = "Manage", Description = "Manage resources", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000020-0000-0000-0000-000000000020"), TenantId = TenantId, Name = "Resource.ManageRates", Resource = "Resource", Action = "ManageRates", Description = "Manage resource rate cards", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000021-0000-0000-0000-000000000021"), TenantId = TenantId, Name = "Resource.ViewCostRate", Resource = "Resource", Action = "ViewCostRate", Description = "View resource cost rates", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000022-0000-0000-0000-000000000022"), TenantId = TenantId, Name = "Resource.EditCostRate", Resource = "Resource", Action = "EditCostRate", Description = "Edit resource cost rates", CreatedBy = "System" }
            ,new() { Id = Guid.Parse("b0000023-0000-0000-0000-000000000023"), TenantId = TenantId, Name = "manager.handover", Resource = "manager", Action = "handover", Description = "Request and respond to manager handovers", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000101-0000-0000-0000-000000000101"), TenantId = TenantId, Name = "Project.View", Resource = "Project", Action = "View", Description = "View projects", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000102-0000-0000-0000-000000000102"), TenantId = TenantId, Name = "Project.Create", Resource = "Project", Action = "Create", Description = "Create projects", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000103-0000-0000-0000-000000000103"), TenantId = TenantId, Name = "Project.Edit", Resource = "Project", Action = "Edit", Description = "Edit project data", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000104-0000-0000-0000-000000000104"), TenantId = TenantId, Name = "Project.Delete", Resource = "Project", Action = "Delete", Description = "Delete projects", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000105-0000-0000-0000-000000000105"), TenantId = TenantId, Name = "ChangeRequest.View", Resource = "ChangeRequest", Action = "View", Description = "View project change requests", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000106-0000-0000-0000-000000000106"), TenantId = TenantId, Name = "ChangeRequest.Manage", Resource = "ChangeRequest", Action = "Manage", Description = "Create, update, and cancel project change requests", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000107-0000-0000-0000-000000000107"), TenantId = TenantId, Name = "ChangeRequest.Approve", Resource = "ChangeRequest", Action = "Approve", Description = "Approve or reject project change requests", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000108-0000-0000-0000-000000000108"), TenantId = TenantId, Name = "Handover.View", Resource = "Handover", Action = "View", Description = "View project and manager handovers", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000109-0000-0000-0000-000000000109"), TenantId = TenantId, Name = "Handover.Manage", Resource = "Handover", Action = "Manage", Description = "Request or cancel project and manager handovers", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000110-0000-0000-0000-000000000110"), TenantId = TenantId, Name = "Handover.Approve", Resource = "Handover", Action = "Approve", Description = "Accept or reject project and manager handovers", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000111-0000-0000-0000-000000000111"), TenantId = TenantId, Name = "Risk.View", Resource = "Risk", Action = "View", Description = "View project risks", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000112-0000-0000-0000-000000000112"), TenantId = TenantId, Name = "Risk.Create", Resource = "Risk", Action = "Create", Description = "Create project risks", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000113-0000-0000-0000-000000000113"), TenantId = TenantId, Name = "Risk.Edit", Resource = "Risk", Action = "Edit", Description = "Edit project risks and follow-through", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000114-0000-0000-0000-000000000114"), TenantId = TenantId, Name = "Risk.Delete", Resource = "Risk", Action = "Delete", Description = "Delete risk children", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000115-0000-0000-0000-000000000115"), TenantId = TenantId, Name = "Risk.Mitigate", Resource = "Risk", Action = "Mitigate", Description = "Transition and mitigate project risks", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000116-0000-0000-0000-000000000116"), TenantId = TenantId, Name = "Planning.View", Resource = "Planning", Action = "View", Description = "View project planning data", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000117-0000-0000-0000-000000000117"), TenantId = TenantId, Name = "Planning.Manage", Resource = "Planning", Action = "Manage", Description = "Manage project planning data", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000118-0000-0000-0000-000000000118"), TenantId = TenantId, Name = "Planning.Lifecycle", Resource = "Planning", Action = "Lifecycle", Description = "Manage planning working and baseline lifecycle", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000119-0000-0000-0000-000000000119"), TenantId = TenantId, Name = "Portfolio.View", Resource = "Portfolio", Action = "View", Description = "View portfolios", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000120-0000-0000-0000-000000000120"), TenantId = TenantId, Name = "Portfolio.Manage", Resource = "Portfolio", Action = "Manage", Description = "Manage portfolios", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000121-0000-0000-0000-000000000121"), TenantId = TenantId, Name = "Program.View", Resource = "Program", Action = "View", Description = "View programs", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000122-0000-0000-0000-000000000122"), TenantId = TenantId, Name = "Program.Manage", Resource = "Program", Action = "Manage", Description = "Manage programs", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000123-0000-0000-0000-000000000123"), TenantId = TenantId, Name = "WorkItem.View", Resource = "WorkItem", Action = "View", Description = "View task and issue work items", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000124-0000-0000-0000-000000000124"), TenantId = TenantId, Name = "WorkItem.Create", Resource = "WorkItem", Action = "Create", Description = "Create task and issue work items", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000125-0000-0000-0000-000000000125"), TenantId = TenantId, Name = "WorkItem.Edit", Resource = "WorkItem", Action = "Edit", Description = "Edit task and issue work items", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000126-0000-0000-0000-000000000126"), TenantId = TenantId, Name = "WorkItem.Delete", Resource = "WorkItem", Action = "Delete", Description = "Delete task and issue work items", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000127-0000-0000-0000-000000000127"), TenantId = TenantId, Name = MeetingPermissions.View, Resource = "Meeting", Action = "View", Description = "View meetings and meeting minutes", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000128-0000-0000-0000-000000000128"), TenantId = TenantId, Name = MeetingPermissions.Create, Resource = "Meeting", Action = "Create", Description = "Create meetings", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000129-0000-0000-0000-000000000129"), TenantId = TenantId, Name = MeetingPermissions.Edit, Resource = "Meeting", Action = "Edit", Description = "Edit meetings and attendees", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000130-0000-0000-0000-000000000130"), TenantId = TenantId, Name = MeetingPermissions.Manage, Resource = "Meeting", Action = "Manage", Description = "Manage minutes, comments, and work item links", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000131-0000-0000-0000-000000000131"), TenantId = TenantId, Name = MeetingPermissions.Delete, Resource = "Meeting", Action = "Delete", Description = "Delete meetings", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000132-0000-0000-0000-000000000132"), TenantId = TenantId, Name = "Stakeholders.Read", Resource = "Stakeholders", Action = "Read", Description = "View project stakeholders", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000133-0000-0000-0000-000000000133"), TenantId = TenantId, Name = "Stakeholders.Manage", Resource = "Stakeholders", Action = "Manage", Description = "Manage project stakeholders", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000134-0000-0000-0000-000000000134"), TenantId = TenantId, Name = DocumentPermissions.View, Resource = "Document", Action = "View", Description = "View documents and templates", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000135-0000-0000-0000-000000000135"), TenantId = TenantId, Name = DocumentPermissions.Create, Resource = "Document", Action = "Create", Description = "Upload documents", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000136-0000-0000-0000-000000000136"), TenantId = TenantId, Name = DocumentPermissions.Edit, Resource = "Document", Action = "Edit", Description = "Upload document versions", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000137-0000-0000-0000-000000000137"), TenantId = TenantId, Name = DocumentPermissions.Archive, Resource = "Document", Action = "Archive", Description = "Archive documents", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000138-0000-0000-0000-000000000138"), TenantId = TenantId, Name = DocumentPermissions.TemplateManage, Resource = "Document", Action = "TemplateManage", Description = "Manage reusable document templates", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000139-0000-0000-0000-000000000139"), TenantId = TenantId, Name = "Workflow.View", Resource = "Workflow", Action = "View", Description = "View workflow definitions and versions", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000140-0000-0000-0000-000000000140"), TenantId = TenantId, Name = "Workflow.Manage", Resource = "Workflow", Action = "Manage", Description = "Edit workflow drafts", CreatedBy = "System" },
            new() { Id = Guid.Parse("b0000141-0000-0000-0000-000000000141"), TenantId = TenantId, Name = "Workflow.Publish", Resource = "Workflow", Action = "Publish", Description = "Publish and retire workflow versions", CreatedBy = "System" }
            ,new() { Id = Guid.Parse("b0000142-0000-0000-0000-000000000142"), TenantId = TenantId, Name = TimeTrackingPermissions.ViewOwn, Resource = "TimeTracking", Action = "ViewOwn", Description = "View own manual time entries", CreatedBy = "System" }
            ,new() { Id = Guid.Parse("b0000143-0000-0000-0000-000000000143"), TenantId = TenantId, Name = TimeTrackingPermissions.ManageOwn, Resource = "TimeTracking", Action = "ManageOwn", Description = "Create, update, and delete own manual time entries", CreatedBy = "System" }
            ,new() { Id = Guid.Parse("b0000144-0000-0000-0000-000000000144"), TenantId = TenantId, Name = TimeTrackingPermissions.ViewTeam, Resource = "TimeTracking", Action = "ViewTeam", Description = "View team manual time entries within management scope", CreatedBy = "System" }
            ,new() { Id = Guid.Parse("b0000145-0000-0000-0000-000000000145"), TenantId = TenantId, Name = TimeTrackingPermissions.ManageTeam, Resource = "TimeTracking", Action = "ManageTeam", Description = "Manage team manual time entries within management scope", CreatedBy = "System" }
        };

        var permissionsByKey = permissions.ToDictionary(p => $"{p.Resource}|{p.Action}");
        foreach (var permission in permissions)
        {
            if (!existingPermissions.Any(e => e.Resource == permission.Resource && e.Action == permission.Action))
                context.Permissions.Add(permission);
        }

        // Roles with their permission sets.
        var roles = new List<Role>
        {
            new() { Id = AdminRoleId, TenantId = TenantId, Name = "admin", Description = "Full system access", Category = "System", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = ExecutiveRoleId, TenantId = TenantId, Name = "executive", Description = "Executive leadership access", Category = "Leadership", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = PortfolioManagerRoleId, TenantId = TenantId, Name = "portfolio.manager", Description = "Portfolio management access", Category = "Management", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = ProjectManagerRoleId, TenantId = TenantId, Name = "project.manager", Description = "Project management access", Category = "Management", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = TeamMemberRoleId, TenantId = TenantId, Name = "team.member", Description = "Team member access", Category = "Operations", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = PmoAdminRoleId, TenantId = TenantId, Name = "pmo.admin", Description = "PMO administration access", Category = "PMO", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = FinanceControllerRoleId, TenantId = TenantId, Name = "finance.controller", Description = "Financial management access", Category = "Finance", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() },
            new() { Id = ResourceManagerRoleId, TenantId = TenantId, Name = "resource.manager", Description = "Resource management access", Category = "Management", IsSystem = true, CreatedBy = "System", Permissions = new List<Permission>() }
        };

        // Permission-to-role mapping by role name.
        var rolePermissionKeys = new Dictionary<string, List<string>>
        {
            ["admin"] = new() { "projects|read", "projects|write", "projects|delete", "portfolios|read", "portfolios|write", "resources|read", "resources|write", "users|read", "users|write", "finances|read", "finances|write", "reports|read", "reports|export", "settings|read", "settings|write", "manager|handover", "Resource|View", "Resource|Assign", "Resource|Allocate", "Resource|Manage", "Resource|ManageRates", "Resource|ViewCostRate", "Resource|EditCostRate", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "WorkItem|Delete", "Stakeholders|Read", "Stakeholders|Manage", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, MeetingPermissions.ManageKey, MeetingPermissions.DeleteKey },
            ["executive"] = new() { "projects|read", "projects|write", "projects|delete", "portfolios|read", "portfolios|write", "resources|read", "resources|write", "users|read", "users|write", "finances|read", "finances|write", "reports|read", "reports|export", "Resource|View", "Resource|Assign", "Resource|Allocate", "Resource|Manage", "Resource|ManageRates", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|View", "Project|Create", "Project|Edit", "Project|Delete", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "ChangeRequest|Approve", "Handover|View", "Handover|Manage", "Handover|Approve", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Manage", "Planning|Lifecycle", "Portfolio|View", "Portfolio|Manage", "Program|View", "Program|Manage", "WorkItem|View", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, DocumentPermissions.View },
            ["portfolio.manager"] = new() { "portfolios|read", "portfolios|write", "projects|read", "projects|write", "reports|read", "manager|handover", "Project|View", "Project|Edit", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "Handover|View", "Handover|Manage", "Handover|Approve", "Planning|View", "Risk|View", "Portfolio|View", "Portfolio|Manage", "Program|View", "Program|Manage", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, DocumentPermissions.View, DocumentPermissions.Create, DocumentPermissions.Edit },
            ["project.manager"] = new() { "projects|read", "projects|write", "resources|read", "reports|read", "manager|handover", "Resource|View", "Resource|Allocate", "Project|View", "Project|Create", "Project|Edit", "Project|Delete", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "Handover|View", "Handover|Manage", "Handover|Approve", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Manage", "Program|View", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, MeetingPermissions.ManageKey, MeetingPermissions.DeleteKey, DocumentPermissions.View, DocumentPermissions.Create, DocumentPermissions.Edit },
            ["team.member"] = new() { "projects|read", "Project|View", "Stakeholders|Read", "Risk|View", "Planning|View", "Portfolio|View", "Program|View", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", MeetingPermissions.ViewKey, DocumentPermissions.View, DocumentPermissions.Create },
            ["pmo.admin"] = new() { "projects|read", "projects|write", "projects|delete", "portfolios|read", "portfolios|write", "resources|read", "resources|write", "users|read", "users|write", "finances|read", "finances|write", "reports|read", "reports|export", "settings|read", "settings|write", "manager|handover", "Resource|View", "Resource|Assign", "Resource|Allocate", "Resource|Manage", "Resource|ManageRates", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|View", "Project|Create", "Project|Edit", "Project|Delete", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "ChangeRequest|Approve", "Handover|View", "Handover|Manage", "Handover|Approve", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Manage", "Planning|Lifecycle", "Portfolio|View", "Portfolio|Manage", "Program|View", "Program|Manage", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "WorkItem|Delete", "Workflow|View", "Workflow|Manage", "Workflow|Publish", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, MeetingPermissions.ManageKey, MeetingPermissions.DeleteKey, DocumentPermissions.View, DocumentPermissions.Create, DocumentPermissions.Edit, DocumentPermissions.Archive, DocumentPermissions.TemplateManage },
            ["finance.controller"] = new() { "finances|read", "finances|write", "reports|read", "reports|export", "projects|read", "Project|View", "Stakeholders|Read", "Portfolio|View", "Program|View", DocumentPermissions.View },
            ["resource.manager"] = new() { "resources|read", "resources|write", "projects|read", "Resource|View", "Resource|Assign", "Resource|Allocate", "Resource|Manage", "Resource|ManageRates", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|View", "Stakeholders|Read", "Risk|View", "Planning|View", "WorkItem|View", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, DocumentPermissions.View }
        };

        foreach (var role in roles)
        {
            var existing = existingRoles.FirstOrDefault(r => r.Name == role.Name);
            var roleEntity = existing ?? role;

            if (existing is null)
                context.Roles.Add(role);

            // The admin role always receives every permission defined in the system so the
            // System Administrator account has full visibility regardless of future seed changes.
            var keys = role.Name == "admin"
                ? permissions.Select(p => $"{p.Resource}|{p.Action}").ToList()
                : rolePermissionKeys[role.Name];

            foreach (var key in keys)
            {
                if (!permissionsByKey.TryGetValue(key, out var permission))
                    continue;

                var permissionEntity = existingPermissions.FirstOrDefault(e => e.Resource == permission.Resource && e.Action == permission.Action)
                    ?? permission;

                if (existing is null)
                {
                    roleEntity.Permissions.Add(permissionEntity);
                }
                else
                {
                    if (!roleEntity.Permissions.Any(p => p.Resource == permission.Resource && p.Action == permission.Action))
                        roleEntity.Permissions.Add(permissionEntity);
                }
            }
        }

        await context.SaveChangesAsync();

        // Users: only the single administrator account. Demo role accounts (executive,
        // portfolio.manager, project.manager, team.member, pmo.admin, finance.controller,
        // resource.manager), the named Project Manager accounts and the generated
        // user1..user80 accounts are intentionally not seeded, so a fresh tenant has no
        // user accounts beyond the administrator.
        var users = new List<User>
        {
            new() { Id = AdminUserId, TenantId = TenantId, Email = "admin@nexus.com", PasswordHash = passwordHash, FirstName = "Alex", LastName = "Admin", JobTitle = "System Administrator", IsActive = true, CreatedBy = "System" }
        };

        foreach (var user in users)
        {
            // Match by deterministic Id first so re-seeding is idempotent even if the
            // email changed between seed revisions; fall back to email for legacy rows.
            var existing = existingUsers.FirstOrDefault(u => u.Id == user.Id)
                ?? existingUsers.FirstOrDefault(u => u.Email == user.Email);
            if (existing is null)
            {
                context.Users.Add(user);
                continue;
            }

            // Keep deterministic seed values authoritative when re-seeding.
            existing.Email = user.Email;
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.JobTitle = user.JobTitle;
            existing.IsActive = true;

            // Migrate legacy BCrypt hashes to the ASP.NET Core PasswordHasher format
            // so existing Development databases keep working after the auth upgrade.
            var currentHash = existing.PasswordHash;
            if (string.IsNullOrEmpty(currentHash) ||
                currentHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                currentHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                currentHash.StartsWith("$2y$", StringComparison.Ordinal))
            {
                existing.PasswordHash = passwordHasher.HashPassword(existing, DemoPassword);
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();

        // User-role assignments (deterministic IDs, idempotent). Only the administrator
        // gets a seeded role; the other roles stay defined so they can be assigned later.
        var assignments = new List<(Guid id, Guid userId, Guid roleId)>
        {
            (Guid.Parse("f1000001-0000-0000-0000-000000000001"), AdminUserId, AdminRoleId)
        };

        foreach (var (id, userId, roleId) in assignments)
        {
            if (existingAssignments.Any(a => a.UserId == userId && a.RoleId == roleId))
                continue;

            context.UserRoles.Add(new UserRole
            {
                Id = id,
                TenantId = TenantId,
                UserId = userId,
                RoleId = roleId,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();

        // Purge every account created by an earlier seed revision so demo users cannot
        // survive a re-seed. Matching is done on the deterministic seed id ranges
        // (80000001-* generated, 90000001-* named PMs) and the legacy "pmNN@nexus.com"
        // shape. The administrator is always kept, and the pattern for legacy accounts
        // is anchored so pmo.admin@nexus.com is never matched.
        var legacyPmEmailPattern = new System.Text.RegularExpressions.Regex(@"^pm\d+@nexus\.com$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        var demoRoleUserIds = new[]
        {
            ExecutiveUserId, PortfolioManagerUserId, ProjectManagerUserId, TeamMemberUserId,
            PmoAdminUserId, FinanceControllerUserId, ResourceManagerUserId
        };

        var namedPmUserIds = Enumerable.Range(0, 5)
            .Select(i => Guid.Parse($"90000001-{i:D4}-0000-0000-000000000000"))
            .ToList();

        var generatedUserIds = Enumerable.Range(0, 80)
            .Select(i => Guid.Parse($"80000001-{i:D4}-0000-0000-000000000000"))
            .ToList();

        var demoUsers = existingUsers
            .Where(u => u.Id != AdminUserId &&
                        (demoRoleUserIds.Contains(u.Id) || namedPmUserIds.Contains(u.Id) || generatedUserIds.Contains(u.Id) || legacyPmEmailPattern.IsMatch(u.Email)))
            .ToList();

        foreach (var demo in demoUsers)
        {
            var stale = existingAssignments.Where(a => a.UserId == demo.Id).ToList();
            context.UserRoles.RemoveRange(stale);
            context.Users.Remove(demo);
        }

        if (demoUsers.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task EnsureDocumentCatalogAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000001"), Code = "GOV", Name = "Governance", Description = "Charters, decisions, and governance records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000002"), Code = "TECH", Name = "Technical", Description = "Architecture, design, and engineering records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000003"), Code = "FIN", Name = "Financial", Description = "Budgets, forecasts, and financial records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000004"), Code = "MEET", Name = "Meeting", Description = "Agendas, minutes, and meeting outputs" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000005"), Code = "RISK", Name = "Risk", Description = "Risk and assurance records" }
        };
        var existing = await context.DocumentCategories.IgnoreQueryFilters().Where(c => c.TenantId == TenantId).ToListAsync();
        foreach (var definition in definitions)
        {
            if (existing.Any(c => c.Id == definition.Id)) continue;
            context.DocumentCategories.Add(new DocumentCategory { Id = definition.Id, TenantId = TenantId, Code = definition.Code, Name = definition.Name, Description = definition.Description, SortOrder = Array.IndexOf(definitions, definition), CreatedBy = "System" });
        }
        await context.SaveChangesAsync();
    }
}
