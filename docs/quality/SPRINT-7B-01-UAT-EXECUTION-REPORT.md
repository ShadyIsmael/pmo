# Sprint 7B-01 — Core Runtime UAT Execution Report

Date: 2026-11-01 · Environment: Development · Database: SQL Server `Shady` / `NexusPmo` · API `http://localhost:5000` · Web `http://localhost:4100`

Scope of this sprint: controlled Development UAT hierarchy created through application APIs, authenticated Projects/Portfolio/Program API matrix, core Project/Portfolio/Program browser journeys, narrow defect repair with retest, Master UAT Checklist update, and a preparation-only financial permission proposal. Execution stopped at 7B-01.

## 1. Environment and identities

| Item | Value |
|---|---|
| Database | `Shady` / `NexusPmo`, 8 applied migrations at execution time |
| Schema, migration, and seed changes made by this sprint | None. Identity/role/permission configuration was not modified. |
| API | Development build `net10.0`, rebuilt with the D-01 fix (0 warnings, 0 errors), restarted; `/health` = Healthy |
| Web | React/Vite dev server on port 4100 |
| Tenant | `11111111-1111-1111-1111-111111111111` |
| Administrator | `admin@nexus.com` |
| Project Manager identity | `uat7b.pm@nexus.com` — role `project.manager`; current Program Manager of `PGM-0001`; Project Manager of `UAT7B-A` |
| Portfolio Manager identity | `uat7b.pfm@nexus.com` — role `portfolio.manager`; current Portfolio Manager of `PF-0001`; Project Manager of `UAT7B-B` |
| Team Member identity | `uat7b.member@nexus.com` — role `team.member`; active 50% Project Team member of `UAT7B-A` |

Credential values and bearer tokens are intentionally excluded from this report.

### Controlled hierarchy

| Entity | Identifier | Created by | Result |
|---|---|---|---|
| Portfolio | `6658d8ee-e69d-4078-af1b-bd59037a562a` (`PF-0001`) | API | 201 |
| Program | `ba5774b0-0398-4982-897c-1e34cd608c25` (`PGM-0001`) | API | 201 |
| Program Manager assignment | request `8a1e9874-0336-40f8-a538-d4f0f1854c69`, accepted by the PM | Admin-initiated assignment + approval | Assignment effective, capability/history endpoints verified |
| Portfolio Manager assignment | request `0278eafd-6f8d-4c17-897b-685df3847497`, accepted by the PFM | Admin-initiated assignment + approval | Assignment effective |
| Project A | internal `2e906f75-aadb-453a-81fc-189ab21d05b7`, public `ac9ee2c8-73b5-434a-9e9b-3645d8390bf6` (`UAT7B-A`) under the Program | PM (program scope) | 201 |
| Project B | internal `b7ab226d-790e-46cf-b55a-ec8ea5aed0ad`, public `ef9de2b7-3d02-4575-8054-f2af850c0da2` (`UAT7B-B`) direct to Portfolio | PFM (portfolio scope) | 201 |
| Project Team assignment | `53c1ae2f-4934-481b-8497-2ecece83905b` (member resource, 50%, Team Member, Active) | PM | 201 |

Fixture resources used for allocation checks were created through `POST /api/resources`; their Project Team allocation rows created during the matrix were removed afterwards, leaving only the intended member assignment. No financial records were fabricated.

## 2. API matrix result — 65 of 65 passed

Artifact: `uat-api-matrix-results-final.json` (65 entries with method, path, expected, actual, and response sample).

| Group | IDs | Count | Result |
|---|---|---|---|
| Authentication, identity, perspectives, malformed token | API-01…API-13, API-05b | 14 | 14 PASS |
| Project read, scope masking, shell, overview, manager history | PRJ-01…PRJ-14 | 14 | 14 PASS |
| Project Team read/add/deny and allocation guards | TMA-01…TMA-09 | 9 | 9 PASS |
| Portfolio read, scope, create denial | PRT-01…PRT-08 | 8 | 8 PASS |
| Program read, scope, create denial, portfolio filter | PRG-01…PRG-12 | 12 | 12 PASS |
| Manager assignment capabilities, history, approvals | HND-01…HND-08 | 8 | 8 PASS |

Behavior confirmed by the matrix:

- Unauthenticated, malformed-token, and invalid-credential requests are rejected with 401; every identity resolves to the correct user and perspective set.
- Project scope is enforced by masked 404 for out-of-scope projects (PM→Project B, member→Project B), not by an enumerable 403.
- Portfolio and Program reads follow the documented scope rules, including the empty-but-200 list behavior for `team.member` (`Portfolio|View`, no portfolio scope) and masked 404 detail reads.
- Write denials are explicit 403 where the permission is missing (`Project.Create`, `Resource.Allocate`, `Program.Manage`, `Portfolio.Manage`, `Handover.Approve`).
- Team allocation guards hold: invalid percentages and inverted date ranges are rejected 400; a resource already carrying 100% on another project is rejected 400 with the conflicting project named; a second role within the same project is accepted because the documented rule uses the highest overlapping dedication per project (TMA-06), while cross-project totals still combine.
- Program scenario expectations `PRG-02/PRG-03` assert the **currently enforced** 403 (see finding F-01); they are enforcement passes, not proof that a Program Manager can open a Program page.

## 3. Browser journeys — 8 of 8 passed

| Journey | Observed result | Evidence |
|---|---|---|
| PM project list | Only `UAT7B-A` listed, with status, budget, and named Project Manager | `evidence/uat7b-01-pm-team.png` (page context) |
| PM project shell and Overview | Loads by public ID; Overview and capacity summary render | `evidence/uat7b-01-pm-team.png` |
| PM Project Team | Member row visible with email, 50%, `Active`; manager row synthesized from the assignment — direct proof of the D-01 fix | `evidence/uat7b-01-pm-team-final.png` |
| PFM portfolio list | `UAT7B Controlled Portfolio` (`PF-0001`, USD) with program/direct-project counts | `evidence/uat7b-01-pfm-program.png` (page context) |
| PFM portfolio detail | Program listed and navigable | `evidence/uat7b-01-pfm-program.png` |
| PFM program detail | Child project `UAT7B-A` listed in the program workspace | `evidence/uat7b-01-pfm-program.png` |
| Member project list and detail | Only the assigned project listed; Overview opens | `evidence/uat7b-01-member-projects.png` |
| Member portfolio/program scope | Localized empty-scope states; no foreign hierarchy disclosed | `evidence/uat7b-01-member-programs.png` |

Console review: 0 errors on successful pages except one expected denial described in F-02.

## 4. Defects, fixes, retests

### D-01 — Project Team membership persisted with an empty TenantId (FIXED)

- **Symptom:** A member added through `POST /api/projects/{publicId}/team` returned 201 but never appeared in `GET .../team`; the member could not open the assigned project (404); over-allocation checks were blind to the new row.
- **Root cause:** `ProjectService.AddTeamMemberAsync` used `CreateProjectMemberDto.ToEntity(userId)`, which leaves `TenantId` unset. The global tenant query filter then hid the row from team reads, management-scope resolution, and the conflict query. Read-only SQL confirmed four rows with `TenantId = 00000000-0000-0000-0000-000000000000`; the other membership-creation paths (manager, sponsor, people service) all set the project tenant.
- **Fix:** One line in `ProjectService.cs`: `member.TenantId = project.TenantId;` with a comment explaining the filter interaction. No API contract, schema, migration, or permission change.
- **Build:** API rebuilt Debug, 0 warnings / 0 errors; Development API restarted; `/health` Healthy.
- **Retest:** Team read returns the member; member project read 200 (PRJ-07); member team read 200 (TMA-02); member add denied 403 (TMA-03); duplicate/over-allocation denied 400 with the conflicting project named; browser Team tab shows the member.
- **Cleanup:** Four orphan `ProjectId/TenantId`-defective rows created earlier by the same defect were deleted; the remaining membership is the intended 50% assignment.
- **Regression:** Full 65-scenario matrix re-run after the fix — 0 regressions. Note: the fix is uncommitted in the shared worktree (`apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs`).
- **Automated regression test:** `AddTeamMemberAsync_PersistsProjectTenantIdAndIsVisibleToTeamReads` was added to `ProjectPlanningPersistenceTests` (asserts the persisted `TenantId` equals the project tenant and that the row is visible to the team read). It compiles, but this sprint could not execute it: the shared SQLite integration fixture fails at construction because `EnsureCreated` runs the SQL Server migration scripts (`SQLite Error 1: near "Provisioning": syntax error`). The same failure affects pre-existing tests in that suite (`GlobalQueryFilterTests`), so it is a pre-existing environment/test-infrastructure blocker and not caused by the fix. Executed regression evidence for this sprint is therefore the 65-scenario API matrix re-run plus the browser team-journey retest.

### F-01 — Live `project.manager` grants diverge from the repository seed (OPEN, not repaired)

- **Observation:** `PRG-02`, `PRG-03`, and the PM portfolio-filtered program request return 403 because the live `project.manager` role has no `Program|View`. The repository seed still grants it; `docs/architecture/permission-model.md` lists only `Project.*`, `Resource.View/Allocate`, `Report.View` for Project Manager. A concurrent `Role Updated` audit entry exists at 2026-10-31 21:12, and the live grant set also contains finance/user/resource grants absent from the seed.
- **Impact:** A user who is the current Program Manager but holds only `project.manager` cannot open Program pages or the Program financial rollup that requires `Program.View`.
- **Decision taken:** No permission was granted during UAT. The divergence is reported for the owning sprint to reconcile seed, documentation, and runtime configuration, then re-run the program scenarios.

### F-02 — Member Project Overview calls a capacity endpoint it cannot access (OPEN, low)

- **Observation:** Opening Project Overview as `team.member` issues `GET /api/projects/{publicId}/overview/team-capacity`, which requires `Resource.View`; the API returns 403 and the browser logs one console error. The page still renders correctly and discloses no data.
- **Recommendation:** Gate the call with the same capability the API requires, consistent with the frontend data-integrity rules.

## 5. Blocked and not-tested scenarios

| Area | State | Reason |
|---|---|---|
| Financial workflows (Annual Budget approval, Settlement, Reversal, Year-End Closing, Carry Forward) | Not tested — preparation only | Out of 7B-01 execution scope; the eight sensitive permission keys have zero role grants by design and no approved delegation exists. See `SPRINT-7B-01-FINANCIAL-PERMISSION-MATRIX-PROPOSAL.md`. |
| PM program-page capability | Blocked by environment authorization drift | Finding F-01 |
| Automated SQLite persistence suite (including the D-01 regression test) | Blocked — pre-existing, suite-wide | The fixture's `EnsureCreated` executes the SQL Server migration scripts against SQLite and fails at schema creation (`near "Provisioning": syntax error`); unrelated tests in the same suite fail identically. The added test compiles and is expected to run once the fixture is corrected. |
| Legacy stakeholder invitation, team-target allocation, consolidated concurrency/financial UAT | Not in this sprint | Already tracked as deferred sections in the Master UAT Checklist |

## 6. Metrics

| Metric | Value |
|---|---|
| In-Scope Total | 73 (65 API + 8 browser journeys) |
| Executed | 73 |
| Passed | 73 |
| Failed | 0 |
| Blocked | 0 (one capability blocked by drift is reported as F-01, not counted as executed) |
| Not Tested | 0 within 7B-01 core scope; financial workflows excluded by scope |
| Defects found / fixed / retested | 1 / 1 / 1 |
| Open findings | 2 (F-01 medium, F-02 low) |
| Regressions | 0 |

## 7. Concurrency and environment notes

- The worktree and database are shared with concurrent sprint work. During this execution the worktree gained substantial unrelated changes (financial persistence, meetings, settings), the database advanced to 8 migrations, and role/permission configuration changed externally at 2026-10-31 21:12.
- Two concurrent events directly affected test execution: the PM/PFM credentials were changed mid-run (repaired only through the supported admin temporary-password + change-initial-password flow), and the `project.manager` grant set changed (recorded as F-01).
- The Portfolio Manager identity holds only `portfolio.manager`; it has no `Resource.Allocate`/`Resource.Assign` claim, so resource allocation denials for that identity are 403 (permission), not 400 (conflict). The allocation-conflict negatives were therefore executed by the Project Manager.
- No EF migration, schema object, seed, role grant, or user permission was created or modified by this sprint, apart from deleting the four orphan membership rows and the allocation fixtures this sprint itself created.

## 8. Artifacts

| Artifact | Location |
|---|---|
| Final API matrix results (65 scenarios) | `C:\Users\shady\AppData\Local\Temp\opencode\uat-7b-01\uat-api-matrix-results-final.json` |
| Matrix script (v3) | `C:\Users\shady\AppData\Local\Temp\opencode\uat-7b-01\uat-api-matrix-v3.ps1` |
| Controlled hierarchy state (identifiers only) | `C:\Users\shady\AppData\Local\Temp\opencode\uat-7b-01\uat-state.json` |
| Browser screenshots | `C:\Users\shady\AppData\Local\Temp\opencode\uat-7b-01\evidence\*.png` |
| Checklist update | `docs/quality/MASTER-UAT-CHECKLIST.md` — section “Sprint 7B-01 Core Runtime UAT — executed 2026-11-01” |
| Financial permission proposal | `docs/quality/SPRINT-7B-01-FINANCIAL-PERMISSION-MATRIX-PROPOSAL.md` |
| Code fix (uncommitted) | `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` — `AddTeamMemberAsync` |
