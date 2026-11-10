# P0 Project Sponsor Visibility Recovery

**Date:** 2026-11-10  
**Status:** Source implementation and focused regressions added; final API compilation remains unverified.

## Confirmed root cause

`ManagementScopeResolver.LoadAssignmentsAsync` built native Project scope from the Project Manager assignment and active ProjectMember-to-Resource links. It did not include the canonical `Project.SponsorId` assignment. `ProjectService` list/detail queries and project-scoped middleware use that resolver scope, so a valid Sponsor was filtered out before Project.View and downstream project APIs could serve the project. This did not require a role-category exception or a duplicate Project Team membership.

## Change

- Added a same-Tenant `Project.SponsorId` assignment to native project scope only when the referenced System User is active and not deleted.
- Kept the existing outer Tenant and project filters. No role-name or role-category bypass was added.
- Added a regression for a Sponsor-only project with no Resource or ProjectMember. It checks native scope, `CanAccessProjectAsync`, `ProjectService.GetPagedAsync`, `GetShellAsync`, and exclusion of a Sponsor assignment on another Tenant's project.

Project visibility still requires canonical `Project.View` at the API/UI authorization layer. Annual Budget data/actions continue to require their own View/Action permissions and project scope.

## Verification and database status

| Check | Result |
|---|---|
| Focused regression | Added in `ManagementScopeResolverTests`; not executed because the single API test/build attempt stopped during API compilation. |
| API build/test attempt | One filtered attempt; it reached `NEXUS.Api` and failed on a missing using directive in the new notification publisher. That directive and a follow-up approval eligibility adjustment were made afterward; neither was recompiled under the one-attempt constraint. |
| Development database identity | Not verified. Read-only SQL connection was blocked by the execution environment's socket policy. |
| Project, approval, and notification rows | Not inspected; no database writes were made. |
| EF model synchronization / pending migrations | Not run / unavailable from live Development. This change adds no entity, mapping, or migration. |
| Target / backup / migrations / seed / physical schema / SQL Server persistence tests | Target identity not verified; backup not requested; no migrations applied; seed not run; physical schema not verified; SQL Server persistence tests not run. |

## Handoff

Source-level Sponsor access now follows the normal native scope and Project.View checks. Product Owner authenticated browser UAT remains pending for the specified Development project, including positive list/detail access and negative cross-Tenant access. API compilation and focused regression execution also need a follow-up verification pass because the one permitted attempt occurred before the missing import was corrected.
