import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PlanningImportSheet } from "../features/projects/components/PlanningImportSheet";
import { planningApiFull, type PlanImportSessionDto, type PlanSummaryDto } from "../features/projects/api/planningFull";
import { planningImportNoticeMessage } from "../features/projects/lib/planning-import-messages";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";

const t = (key: string, params?: Record<string, string | number>) => key.replace(/\{(\w+)\}/g, (_match, name: string) => String(params?.[name] ?? ""));
function localizedT(locale: "en" | "ar") {
  return (key: string, params?: Record<string, string | number>) => {
    const path = key.startsWith("planning.") ? `projects.${key}` : key;
    const value = path.split(".").reduce<unknown>((current, segment) => current && typeof current === "object" ? (current as Record<string, unknown>)[segment] : undefined, locale === "ar" ? arMessages : enMessages);
    return typeof value === "string" ? value.replace(/\{(\w+)\}/g, (_match, name: string) => String(params?.[name] ?? "")) : key;
  };
}
const plans: PlanSummaryDto[] = [{ id: "plan-existing", projectId: "project-1", planType: "Execution", name: "Existing delivery plan", status: "InProgress", versionCount: 1, createdAt: "2026-10-01T00:00:00Z" }];
const session: PlanImportSessionDto = {
  id: "import-1",
  projectId: "project-1",
  fileName: "source-plan.mpp",
  format: "MPP",
  status: "ReadyToCommit",
  suggestedPlanName: "Imported plan",
  blockingIssueCount: 0,
  unresolvedResourceCount: 0,
  ignoredAssignmentCount: 0,
  statistics: { tasks: 2, milestones: 1, wbsLevels: 2, dependencies: 1, resources: 1, assignments: 1, calendars: 1, actualDates: 0 },
  warnings: [],
  unsupportedFields: [],
  issues: [],
  dependencies: [],
  tasks: [],
  resources: [],
  assignments: [],
  calendars: [],
};

function renderSheet(commit = vi.fn().mockResolvedValue({ planId: "plan-imported", versionNumber: 2, statistics: { tasks: 2, milestones: 1 } }), translator = t) {
  const onOpenChange = vi.fn();
  const onImported = vi.fn();
  const commitMutation = { mutateAsync: commit, isPending: false };
  render(<PlanningImportSheet projectId="project-1" plans={plans} selectedPlanId="plan-existing" commitMutation={commitMutation} t={translator} onOpenChange={onOpenChange} onImported={onImported} />);
  return { commit, onOpenChange, onImported };
}

async function chooseFileAndPreview(locale: "key" | "en" | "ar" = "key") {
  const file = new File(["mpp content"], "source-plan.mpp", { type: "application/vnd.ms-project" });
  fireEvent.change(screen.getByLabelText(locale === "ar" ? "ملف Microsoft Project" : locale === "en" ? "Microsoft Project file" : "planning.import.fileLabel"), { target: { files: [file] } });
  fireEvent.click(screen.getByRole("button", { name: locale === "ar" ? "معاينة الاستيراد" : locale === "en" ? "Preview import" : "planning.import.preview" }));
  await waitFor(() => expect(planningApiFull.importSession).toHaveBeenCalled());
  await screen.findByRole("button", { name: locale === "ar" ? "استيراد إصدار العمل" : locale === "en" ? "Import Working version" : "planning.import.confirm" });
}

describe("planning import sheet", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue(session);
  });

  it("imports into the selected existing plan and opens the returned plan", async () => {
    const { commit, onImported, onOpenChange } = renderSheet();
    await chooseFileAndPreview();
    fireEvent.click(screen.getByRole("button", { name: "planning.import.confirm" }));

    await waitFor(() => expect(commit).toHaveBeenCalledWith({ sessionId: "import-1", body: { targetPlanId: "plan-existing" } }));
    expect(onImported).toHaveBeenCalledWith("plan-imported");
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("allows creating a new plan and commits the chosen name and type", async () => {
    const { commit, onImported } = renderSheet();
    fireEvent.click(screen.getByRole("button", { name: "planning.import.newPlan" }));
    fireEvent.change(screen.getByLabelText("planning.import.planName"), { target: { value: "  New approved plan  " } });
    await chooseFileAndPreview();
    fireEvent.change(screen.getByLabelText("planning.import.planName"), { target: { value: "  New approved plan  " } });
    fireEvent.click(screen.getByRole("button", { name: "planning.import.confirm" }));

    await waitFor(() => expect(commit).toHaveBeenCalledWith({ sessionId: "import-1", body: { newPlanName: "New approved plan", newPlanType: "Custom" } }));
    expect(planningApiFull.importSession).toHaveBeenCalledWith("project-1", expect.any(File), undefined);
    expect(onImported).toHaveBeenCalledWith("plan-imported");
  });

  it("shows import mismatches and API errors in Arabic when Arabic is the active locale", async () => {
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({
      ...session,
      status: "NeedsReview",
      blockingIssueCount: 1,
      warnings: [{ code: "missing-parent", message: "The parent of task 'Imported child' was missing; the task was imported at the root level.", count: 1 }],
      issues: [{ id: "issue-1", severity: "Error", category: "Resource", entityType: "Resource", sourceId: "res-1", code: "resource-resolution-required", message: "Resource 'Imported analyst' must be mapped, created, or ignored before commit.", count: 1, isBlocking: true, resolution: "Unresolved" }],
    });
    const commit = vi.fn().mockRejectedValue(new Error("A Plan of this type already exists for the project. Import into that Plan or choose another type."));
    renderSheet(commit, localizedT("ar"));
    await chooseFileAndPreview("ar");

    expect(screen.getByText("لم يتوفر عنصر أب للمهمة «Imported child»، لذلك تم استيرادها في المستوى الأعلى. (1)")).toBeInTheDocument();
    expect(screen.getByText("يجب مطابقة المورد «Imported analyst» أو إنشاؤه أو تجاهله قبل متابعة الاستيراد. (1)")).toBeInTheDocument();
    expect(screen.queryByText(/The parent of task|must be mapped/)).not.toBeInTheDocument();

    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({ ...session, status: "ReadyToCommit" });
    cleanup();
    renderSheet(commit, localizedT("ar"));
    await chooseFileAndPreview("ar");
    fireEvent.click(screen.getByRole("button", { name: "استيراد إصدار العمل" }));
    expect(await screen.findByText("يوجد بالفعل في هذا المشروع خطة من هذا النوع. استورد إلى تلك الخطة أو اختر نوعًا آخر.")).toBeInTheDocument();
  });

  it("translates assignment review statuses in the Arabic import preview", async () => {
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({
      ...session,
      assignments: [{ sourceId: "assignment-1", taskSourceId: "task-1", resourceSourceId: "resource-1", plannedHours: 8, workConversionStatus: "NeedsReview", status: "NeedsReview" }],
    });
    renderSheet(undefined, localizedT("ar"));
    await chooseFileAndPreview("ar");
    fireEvent.click(screen.getByRole("tab", { name: "التعيينات (1)" }));

    expect(await screen.findByText("تحتاج إلى مراجعة")).toBeInTheDocument();
    expect(screen.queryByText("NeedsReview")).not.toBeInTheDocument();
  });

  it("shows an ignored resource as an applied decision and updates the resolution summary", async () => {
    const reviewSession: PlanImportSessionDto = {
      ...session,
      status: "NeedsReview",
      blockingIssueCount: 1,
      unresolvedResourceCount: 1,
      resources: [{ sourceId: "resource-source-1", name: "Imported Analyst", email: "analyst@example.com", matchStatus: "Unmatched", assignmentCount: 1, resolution: "Unresolved", candidates: [] }],
      assignments: [{ sourceId: "assignment-source-1", taskSourceId: "task-source-1", resourceSourceId: "resource-source-1", plannedHours: 8, workConversionStatus: "Ready", status: "NeedsResource" }],
      issues: [{ id: "issue-resource-1", severity: "Error", category: "Resource", entityType: "Resource", sourceId: "resource-source-1", code: "resource-resolution-required", message: "Review this resource", count: 1, isBlocking: true, resolution: "Unresolved" }],
    };
    const ignoredSession: PlanImportSessionDto = {
      ...reviewSession,
      status: "ReadyToCommit",
      blockingIssueCount: 0,
      unresolvedResourceCount: 0,
      ignoredAssignmentCount: 1,
      resources: [{ ...reviewSession.resources[0], resolution: "Ignore" }],
      assignments: [{ ...reviewSession.assignments[0], status: "IgnoredByResource" }],
      issues: [],
    };
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue(reviewSession);
    const resolveResource = vi.spyOn(planningApiFull, "resolveImportResource").mockResolvedValue(ignoredSession);
    renderSheet(undefined, localizedT("en"));
    await chooseFileAndPreview("en");
    fireEvent.click(screen.getByRole("tab", { name: "Resources (1)" }));
    fireEvent.click(screen.getByRole("button", { name: "Ignore" }));

    await waitFor(() => expect(resolveResource).toHaveBeenCalledWith("project-1", "import-1", "resource-source-1", { resolution: "Ignore" }));
    expect(await screen.findByText("Ignored — won't be imported")).toBeInTheDocument();
    expect(screen.getAllByText("1 ignored")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Ignored" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Import Working version" })).toBeEnabled();
  });

  it("clarifies that a selected new resource will be created when the import is committed", async () => {
    const reviewSession: PlanImportSessionDto = {
      ...session,
      resources: [{ sourceId: "resource-source-2", name: "Imported Engineer", matchStatus: "Unmatched", assignmentCount: 0, resolution: "Unresolved", candidates: [] }],
    };
    const createSelectedSession: PlanImportSessionDto = {
      ...reviewSession,
      resources: [{ ...reviewSession.resources[0], resolution: "CreateNew" }],
    };
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue(reviewSession);
    const resolveResource = vi.spyOn(planningApiFull, "resolveImportResource").mockResolvedValue(createSelectedSession);
    renderSheet(undefined, localizedT("en"));
    await chooseFileAndPreview("en");
    fireEvent.click(screen.getByRole("tab", { name: "Resources (1)" }));
    fireEvent.click(screen.getByRole("button", { name: "Create new" }));

    await waitFor(() => expect(resolveResource).toHaveBeenCalledWith("project-1", "import-1", "resource-source-2", expect.objectContaining({ resolution: "CreateNew" })));
    expect(await screen.findAllByText("Will create on import")).toHaveLength(2);
    expect(screen.getByText("1 to create")).toBeInTheDocument();
  });

  it("renders assignment task and resource names instead of source identifiers", async () => {
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({
      ...session,
      tasks: [{ sourceId: "source-task-17", code: "2.4", name: "Build risk controls", outlineLevel: 2, sortOrder: 4, isMilestone: false, schedulingMode: "Automatic", constraintType: "AsSoonAsPossible", durationDays: 3, predecessors: [], resources: [] }],
      resources: [{ sourceId: "source-resource-9", name: "Imported Scheduler", email: "scheduler@example.com", matchStatus: "Matched", assignmentCount: 1, resolution: "MapExisting", nexusResourceId: "resource-1", candidates: [] }],
      assignments: [{ sourceId: "assignment-source-4", taskSourceId: "source-task-17", resourceSourceId: "source-resource-9", plannedHours: 24, allocationPercent: 50, start: "2026-10-14T00:00:00Z", finish: "2026-10-16T00:00:00Z", workConversionStatus: "Ready", status: "Ready" }],
    });
    renderSheet(undefined, localizedT("en"));
    await chooseFileAndPreview("en");
    fireEvent.click(screen.getByRole("tab", { name: "Assignments (1)" }));

    expect(await screen.findByText("Build risk controls")).toBeInTheDocument();
    expect(screen.getByText("Imported Scheduler")).toBeInTheDocument();
    expect(screen.getByText("24 h")).toBeInTheDocument();
    expect(screen.getByText("50%")).toBeInTheDocument();
    expect(screen.getByText("2026-10-14 – 2026-10-16")).toBeInTheDocument();
    expect(screen.queryByText("source-task-17")).not.toBeInTheDocument();
    expect(screen.queryByText("source-resource-9")).not.toBeInTheDocument();
  });

  it("jumps from a blocking assignment issue to the assignment row that needs review", async () => {
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({
      ...session,
      status: "NeedsReview",
      blockingIssueCount: 1,
      tasks: [{ sourceId: "task-source-1", code: "1", name: "Prepare site", outlineLevel: 1, sortOrder: 1, isMilestone: false, schedulingMode: "Automatic", constraintType: "AsSoonAsPossible", durationDays: 1, predecessors: [], resources: [] }],
      resources: [{ sourceId: "resource-source-1", name: "Site Planner", matchStatus: "Matched", assignmentCount: 1, resolution: "MapExisting", nexusResourceId: "resource-1", candidates: [] }],
      assignments: [{ sourceId: "assignment-source-1", taskSourceId: "task-source-1", resourceSourceId: "resource-source-1", workConversionStatus: "NeedsReview", status: "NeedsReview" }],
      issues: [{ id: "issue-assignment-1", severity: "Error", category: "Assignment", entityType: "Assignment", sourceId: "assignment-source-1", code: "assignment-work-review-required", message: "Review this assignment", count: 1, isBlocking: true, resolution: "NeedsReview" }],
    });
    renderSheet(undefined, localizedT("en"));
    await chooseFileAndPreview("en");
    fireEvent.click(screen.getByRole("button", { name: "Review" }));

    const assignmentTab = screen.getByRole("tab", { name: "Assignments (1)" });
    await waitFor(() => expect(assignmentTab).toHaveAttribute("aria-selected", "true"));
    await waitFor(() => expect((document.activeElement as HTMLElement).textContent).toContain("Prepare site"));
  });

  it("jumps from a blocking calendar issue to its explicit resolution selector", async () => {
    vi.spyOn(planningApiFull, "importSession").mockResolvedValue({
      ...session,
      status: "NeedsReview",
      blockingIssueCount: 1,
      calendars: [{ sourceId: "calendar-source-1", name: "Holiday calendar", isDefault: true, workingDays: 5, holidays: 2, resolution: "Pending", requiresReview: true }],
      issues: [{ id: "issue-calendar-1", severity: "Error", category: "Calendar", entityType: "Calendar", sourceId: "calendar-source-1", code: "calendar-review-required", message: "Review this calendar", count: 1, isBlocking: true, resolution: "Pending" }],
    });
    renderSheet(undefined, localizedT("en"));
    await chooseFileAndPreview("en");
    fireEvent.click(screen.getByRole("button", { name: "Review" }));

    const calendarTab = screen.getByRole("tab", { name: "Calendars (1)" });
    await waitFor(() => expect(calendarTab).toHaveAttribute("aria-selected", "true"));
    await waitFor(() => expect((document.activeElement as HTMLElement).textContent).toContain("Holiday calendar"));
    expect(screen.getByText("Needs review — choose an action")).toBeInTheDocument();
  });

  it("has Arabic messages for every stable import warning and review issue code", () => {
    const codes = [
      "duplicate-task-id", "fractional-duration-rounded", "task-notes-truncated", "actual-duration", "baseline-fields",
      "constraints-imported", "task-calendar-links", "task-text-formatting", "scheduling-fields", "fractional-lag-rounded",
      "dependency-skipped", "assignment-skipped", "assignment-work-mapped", "assignment-work-conversion", "calendar-features",
      "working-version-replaced", "unmatched-resource-assignments", "actual-finish-without-start", "missing-parent", "unmatched-resources",
      "resource-resolution-required", "resource-mapping-invalid", "assignment-work-review-required", "assignment-resource-unresolved", "calendar-review-required",
    ];
    const arabicT = localizedT("ar");
    for (const code of codes) {
      const translated = planningImportNoticeMessage({ code, message: "Source message", count: 1 }, arabicT);
      expect(translated, code).not.toBe(arabicT("planning.import.unknownNotice"));
      expect(translated, code).not.toBe(`planning.import.notices.${code}`);
    }
  });
});
