import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ChangePhaseSettings } from "@features/projects/ChangePhaseSettings";
import type { ProjectDto } from "@features/projects/api/projects";

const mocks = vi.hoisted(() => ({
  permissions: [
    "Project.View",
    "Document.View",
    "ProjectPhase.Change",
    "Document.Edit",
    "Document.Create",
  ] as string[],
  canMutate: true,
  effectiveReadOnly: false,
  confirm: vi.fn(),
  changePhase: vi.fn(),
  link: vi.fn(),
  unlink: vi.fn(),
  upload: vi.fn(),
  download: vi.fn(),
  refetchReadiness: vi.fn(),
  refetchHistory: vi.fn(),
  refetchDocuments: vi.fn(),
  refetchLifecycle: vi.fn(),
  lifecycleOptionsCalls: [] as Array<Record<string, unknown>>,
  readinessCalls: [] as unknown[][],
  toast: vi.fn(),
  readiness: undefined as Record<string, unknown> | undefined,
  history: [] as Record<string, unknown>[],
  readinessLoading: false,
  readinessError: false,
  historyLoading: false,
  historyError: false,
  lifecycleLoading: false,
  lifecycleError: false,
  lifecycleData: undefined as Record<string, unknown> | undefined,
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: mocks.permissions } }),
}));
vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }),
}));
vi.mock("@i18n/messages", () => ({
  useI18n: () => ({
    locale: "en",
    t: (key: string, params?: Record<string, string | number>) => {
      const label = key.split(".").at(-1) ?? key;
      return params ? `${label}|${Object.values(params).join("|")}` : label;
    },
    formatNumber: (value: number) => String(value),
    formatDateTime: (value: string) => `dt:${value}`,
  }),
}));
vi.mock("@hooks/use-toast", () => ({ toast: (input: unknown) => mocks.toast(input) }));
vi.mock("@components/ui/ConfirmProvider", () => ({ useConfirm: () => mocks.confirm }));
vi.mock("@features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: (options: Record<string, unknown>) => {
    mocks.lifecycleOptionsCalls.push(options);
    return {
      data: mocks.lifecycleData,
      isLoading: mocks.lifecycleLoading,
      isError: mocks.lifecycleError,
      refetch: mocks.refetchLifecycle,
    };
  },
  projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string; code: string } | undefined, locale: string) =>
    option ? (locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code) : "",
  projectLifecycleDescription: (option: { descriptionEn?: string | null; descriptionAr?: string | null } | undefined, locale: string) =>
    option ? (locale === "ar" ? option.descriptionAr || option.descriptionEn || "" : option.descriptionEn || option.descriptionAr || "") : "",
}));
vi.mock("@features/projects/api/documents", () => ({
  useProjectDocuments: () => ({ data: { items: [] }, isLoading: false, isError: false, refetch: mocks.refetchDocuments }),
  useProjectDocument: () => ({ data: undefined, isLoading: false, isError: false }),
  useUploadDocument: () => ({ mutateAsync: mocks.upload, isPending: false, isError: false }),
  documentApi: { download: mocks.download },
}));
vi.mock("@features/projects/api/project-phase-gates", () => ({
  useProjectPhaseGateReadiness: (...args: unknown[]) => {
    mocks.readinessCalls.push(args);
    return {
      data: mocks.readiness,
      isLoading: mocks.readinessLoading,
      isError: mocks.readinessError,
      refetch: mocks.refetchReadiness,
    };
  },
  useChangeProjectPhase: () => ({ mutateAsync: mocks.changePhase, isPending: false }),
  useLinkPhaseGateDocument: () => ({ mutateAsync: mocks.link, isPending: false, isError: false }),
  useUnlinkPhaseGateDocument: () => ({ mutateAsync: mocks.unlink, isPending: false, isError: false }),
  useProjectPhaseHistory: () => ({
    data: mocks.history,
    isLoading: mocks.historyLoading,
    isError: mocks.historyError,
    refetch: mocks.refetchHistory,
  }),
}));

const lifecyclePhases = [
  { id: "phase-closure", code: "closure", nameEn: "Closure", nameAr: "الإغلاق", descriptionEn: "Approved project closure.", descriptionAr: "إغلاق المشروع المعتمد.", displayOrder: 10, isDefault: false, isActive: true },
  { id: "phase-initiation", code: "initiation", nameEn: "Initiation", nameAr: "التأسيس", displayOrder: 20, isDefault: false, isActive: true },
  { id: "phase-pre-initiation", code: "pre-initiation", nameEn: "Pre-Initiation", nameAr: "ما قبل التأسيس", descriptionEn: "Initial project setup.", descriptionAr: "الإعداد الأولي للمشروع.", displayOrder: 0, isDefault: true, isActive: true },
];

function createReadiness(overrides: Record<string, unknown> = {}) {
  return {
    projectId: "project-id",
    currentPhase: "initiation",
    currentPhaseLabelEn: "Initiation",
    currentPhaseLabelAr: "التأسيس",
    targetPhaseId: "phase-pre-initiation",
    targetPhase: "pre-initiation",
    targetPhaseLabelEn: "Pre-Initiation",
    targetPhaseLabelAr: "ما قبل التأسيس",
    configurationVersionNumber: 4,
    readinessStatus: "Incomplete",
    phaseTransitionStatus: "AwaitingTransition",
    mandatoryRequirementCount: 1,
    satisfiedMandatoryCount: 0,
    missingMandatoryCount: 1,
    optionalRequirementCount: 1,
    satisfiedOptionalCount: 0,
    readinessPercentage: 0,
    projectRowVersion: "row-version",
    requirements: [
      {
        requirementSnapshotId: "mandatory-requirement",
        sourceRequirementId: "source-mandatory",
        templateId: "template-a",
        templateVersionId: "template-version-a",
        templateName: "Business Case",
        category: "Governance",
        templateVersionLabel: "v3",
        requirementType: "Mandatory",
        status: "Missing",
        complianceStatus: "Mismatch",
      },
      {
        requirementSnapshotId: "optional-requirement",
        sourceRequirementId: "source-optional",
        templateId: "template-b",
        templateVersionId: "template-version-b",
        templateName: "Supporting Note",
        category: "Governance",
        templateVersionLabel: "v1",
        requirementType: "Optional",
        status: "OptionalNotProvided",
        complianceStatus: "Missing",
      },
    ],
    ...overrides,
  };
}

function renderSection(overrides: Partial<ProjectDto> = {}, presentation: "inline" | "side-sheet" = "inline") {
  const project = {
    id: "project-id",
    publicId: "project-public-id",
    tenantId: "tenant-id",
    name: "Bridge Renewal",
    code: "BRG-001",
    phaseId: "phase-initiation",
    phase: "initiation",
    phaseNameEn: "Initiation",
    phaseNameAr: "التأسيس",
    rowVersion: "row-version",
    status: "InProgress",
    priority: "Medium",
    currency: "USD",
    progress: 0,
    health: "Green",
    ragStatus: "Green",
    confidenceLevel: "High",
    createdAt: "2026-01-01T00:00:00.000Z",
    createdBy: "user-1",
    isDeleted: false,
    members: [],
    ...overrides,
  } as unknown as ProjectDto;
  return render(<ChangePhaseSettings projectId={project.id} project={project} presentation={presentation} />);
}

function changeButton() {
  return screen.getByRole("button", { name: "changePhaseAction" });
}

describe("Change Phase Project Settings section", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.permissions = ["Project.View", "Document.View", "ProjectPhase.Change", "Document.Edit", "Document.Create"];
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.confirm.mockResolvedValue(true);
    mocks.changePhase.mockResolvedValue({});
    mocks.readiness = createReadiness();
    mocks.history = [];
    mocks.readinessLoading = false;
    mocks.readinessError = false;
    mocks.historyLoading = false;
    mocks.historyError = false;
    mocks.lifecycleLoading = false;
    mocks.lifecycleError = false;
    mocks.lifecycleData = { phases: lifecyclePhases, statuses: [] };
    mocks.lifecycleOptionsCalls = [];
    mocks.readinessCalls = [];
  });

  it("loads target phases by external Project PublicId without guessing an Organization or changing Current Phase checklist scope", () => {
    renderSection();

    expect(mocks.lifecycleOptionsCalls.at(-1)).toEqual({ enabled: true, projectId: "project-public-id" });
    fireEvent.change(screen.getByRole("combobox", { name: "targetPhase" }), { target: { value: "phase-closure" } });
    expect(mocks.readinessCalls.at(-1)).toEqual(["project-public-id", undefined, true]);
  });

  it("renders the current phase and target phase options with Pre-Initiation ordered first", () => {
    renderSection();

    expect(screen.getByText("Initiation")).toBeInTheDocument();
    const select = screen.getByRole("combobox", { name: "targetPhase" });
    const options = Array.from((select as HTMLSelectElement).options).map((option) => option.textContent);
    expect(options).toEqual(["choosePhase", "Pre-Initiation", "Closure"]);
    expect((select as HTMLSelectElement).value).toBe("phase-pre-initiation");
    expect(screen.getByText("Initial project setup.")).toBeInTheDocument();
  });

  it("opens the Change Phase side sheet with the current phase, target catalog, and gate requirements", () => {
    renderSection({}, "side-sheet");

    expect(screen.queryByRole("dialog", { name: "changePhaseTitle" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "openChangePhase" }));

    const sideSheet = screen.getByRole("dialog", { name: "changePhaseTitle" });
    expect(sideSheet).toBeInTheDocument();
    expect(within(sideSheet).getByText("currentPhase")).toBeInTheDocument();
    expect(within(sideSheet).getByText("Initiation")).toBeInTheDocument();
    expect(within(sideSheet).getByRole("combobox", { name: "targetPhase" })).toBeInTheDocument();
    expect(within(sideSheet).getByRole("heading", { name: "mandatoryDocuments" })).toBeInTheDocument();
    expect(within(sideSheet).getByRole("heading", { name: "optionalDocuments" })).toBeInTheDocument();
  });

  it("keeps an authorized phase change available without Document.View while restricting gate details", async () => {
    mocks.permissions = ["Project.View", "ProjectPhase.Change"];
    renderSection();

    expect(mocks.readinessCalls.at(-1)).toEqual(["project-public-id", undefined, false]);
    expect(screen.getByText("documentDetailsRestricted")).toBeInTheDocument();
    fireEvent.click(changeButton());

    await waitFor(() => expect(mocks.changePhase).toHaveBeenCalledWith({
      targetPhaseId: "phase-pre-initiation",
      expectedProjectVersionToken: "row-version",
    }));
  });

  it("does not expose the phase mutation action without ProjectPhase.Change", () => {
    mocks.permissions = ["Project.View", "Document.View"];
    renderSection();

    expect(screen.getByText("changePhasePermissionRequired")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "changePhaseAction" })).not.toBeInTheDocument();
  });

  it("groups Mandatory requirements before Optional and renders structured compliance findings", () => {
    mocks.readiness = createReadiness({
      requirements: [
        {
          requirementSnapshotId: "mandatory-mismatch",
          sourceRequirementId: "source-mandatory",
          templateId: "template-a",
          templateVersionId: "template-version-a",
          templateName: "Business Case",
          category: "Governance",
          templateVersionLabel: "v3",
          requirementType: "Mandatory",
          status: "Satisfied",
          complianceStatus: "Mismatch",
          expectedFileType: "docx",
          detectedFileType: "pdf",
          comparedAt: "2026-01-02T03:04:05.000Z",
          validatorVersion: "1.2.0",
          complianceFindings: [{
            code: "MISSING_SECTION",
            messageEn: "Required section is missing.",
            messageAr: "القسم المطلوب مفقود.",
            expected: "Business Case section",
            detected: "Not found",
          }],
        },
        {
          requirementSnapshotId: "mandatory-not-verified",
          sourceRequirementId: "source-not-verified",
          templateId: "template-c",
          templateVersionId: "template-version-c",
          templateName: "Risk Register",
          category: "Planning",
          templateVersionLabel: "v2",
          requirementType: "Mandatory",
          status: "InvalidOrUnavailable",
          complianceStatus: "NotVerified",
        },
        {
          requirementSnapshotId: "mandatory-missing",
          sourceRequirementId: "source-missing",
          templateId: "template-d",
          templateVersionId: "template-version-d",
          templateName: "Stakeholder Plan",
          category: "Governance",
          templateVersionLabel: "v5",
          requirementType: "Mandatory",
          status: "Missing",
          complianceStatus: "Missing",
        },
        {
          requirementSnapshotId: "optional-requirement",
          sourceRequirementId: "source-optional",
          templateId: "template-b",
          templateVersionId: "template-version-b",
          templateName: "Supporting Note",
          category: "Governance",
          templateVersionLabel: "v1",
          requirementType: "Optional",
          status: "OptionalNotProvided",
          complianceStatus: "Missing",
        },
      ],
    });

    renderSection();

    const mandatoryHeading = screen.getByRole("heading", { name: "mandatoryDocuments" });
    const optionalHeading = screen.getByRole("heading", { name: "optionalDocuments" });
    expect(mandatoryHeading.compareDocumentPosition(optionalHeading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(mandatoryHeading.compareDocumentPosition(screen.getByText("Supporting Note")) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    expect(screen.getByText("complianceStatusMismatch")).toBeInTheDocument();
    expect(screen.getByText("Required section is missing.")).toBeInTheDocument();
    expect(screen.getByText("findingExpectedValue|Business Case section")).toBeInTheDocument();
    expect(screen.getByText("findingDetectedValue|Not found")).toBeInTheDocument();
    expect(screen.getByText("validatorVersion|1.2.0")).toBeInTheDocument();
    expect(screen.getByText("notVerifiedExplanation")).toBeInTheDocument();
    expect(screen.getByText("missingExpectedTemplate|Stakeholder Plan|v5")).toBeInTheDocument();
    expect(screen.getByText("transitionNonBlocking")).toBeInTheDocument();
  });

  it("keeps an authorized Change Phase action enabled when readiness is incomplete, mismatched, or not verified", () => {
    renderSection();

    expect(screen.getByText("transitionNonBlocking")).toBeInTheDocument();
    expect(screen.getByText("complianceStatusMismatch")).toBeInTheDocument();
    expect(changeButton()).toBeEnabled();
  });

  it("asks for confirmation and then changes the phase with the target phase and current row version", async () => {
    renderSection();

    fireEvent.click(changeButton());

    await waitFor(() => expect(mocks.confirm).toHaveBeenCalledTimes(1));
    expect(mocks.confirm).toHaveBeenCalledWith(expect.objectContaining({
      title: "changePhaseConfirmTitle",
      description: "changePhaseConfirmDescription|Initiation|Pre-Initiation",
      confirmLabel: "changePhaseAction",
    }));
    await waitFor(() => expect(mocks.changePhase).toHaveBeenCalledWith({
      targetPhaseId: "phase-pre-initiation",
      expectedProjectVersionToken: "row-version",
    }));
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ title: "transitionSuccess" }));
    expect(await screen.findByRole("status")).toHaveTextContent("transitionSuccess");
  });

  it("does not change the phase when the confirmation is dismissed", async () => {
    mocks.confirm.mockResolvedValue(false);
    renderSection();

    fireEvent.click(changeButton());

    await waitFor(() => expect(mocks.confirm).toHaveBeenCalledTimes(1));
    expect(mocks.changePhase).not.toHaveBeenCalled();
  });

  it("renders the immutable phase change history with actor, outcome, counts, and requirement compliance snapshots", () => {
    mocks.readiness = undefined;
    mocks.history = [
      {
        id: "history-newest",
        projectId: "project-id",
        previousPhase: "initiation",
        newPhase: "closure",
        previousPhaseLabelEn: "Initiation Phase",
        newPhaseLabelEn: "Closure Phase",
        previousPhaseLabelAr: "مرحلة التأسيس",
        newPhaseLabelAr: "مرحلة الإغلاق",
        configurationVersionNumber: 7,
        evaluationOutcome: "Ready",
        mandatoryRequirementCount: 2,
        satisfiedMandatoryCount: 1,
        missingMandatoryCount: 1,
        optionalRequirementCount: 1,
        satisfiedOptionalCount: 0,
        actorName: "Dana Analyst",
        transitionedAt: "2026-02-01T10:00:00.000Z",
        requirements: [{
          requirementSnapshotId: "history-requirement-1",
          templateId: "template-a",
          templateVersionId: "template-version-a",
          templateName: "Business Case",
          category: "Governance",
          templateVersionLabel: "v3",
          requirementType: "Mandatory",
          status: "Satisfied",
          complianceStatus: "Mismatch",
          expectedFileType: "docx",
          detectedFileType: "pdf",
          comparedAt: "2026-02-01T09:00:00.000Z",
          validatorVersion: "1.2.0",
          findings: [{
            code: "MISSING_SECTION",
            messageEn: "Required section is missing.",
            messageAr: "القسم المطلوب مفقود.",
            expected: "Business Case section",
            detected: "Not found",
          }],
        }],
      },
      {
        id: "history-oldest",
        projectId: "project-id",
        previousPhase: "pre-initiation",
        newPhase: "initiation",
        previousPhaseLabelEn: "Pre-Initiation Phase",
        newPhaseLabelEn: "Initiation Phase",
        evaluationOutcome: "Incomplete",
        mandatoryRequirementCount: 1,
        satisfiedMandatoryCount: 1,
        missingMandatoryCount: 0,
        optionalRequirementCount: 0,
        satisfiedOptionalCount: 0,
        actorName: "Omar Lead",
        transitionedAt: "2026-01-01T10:00:00.000Z",
        requirements: [],
      },
    ];

    renderSection();

    expect(screen.getByText("historyTitle")).toBeInTheDocument();
    expect(screen.getByText("historyActor: Dana Analyst")).toBeInTheDocument();
    expect(screen.getByText("historyActor: Omar Lead")).toBeInTheDocument();
    expect(screen.getByText("readinessReady")).toBeInTheDocument();
    expect(screen.getByText("readinessIncomplete")).toBeInTheDocument();
    expect(screen.getByText("dt:2026-02-01T10:00:00.000Z")).toBeInTheDocument();
    expect(screen.getByText("requirementCountSummary|1|2|0|1|1")).toBeInTheDocument();
    expect(screen.getAllByText("historyRequirements")).toHaveLength(2);
    expect(screen.getByText("Business Case")).toBeInTheDocument();
    expect(screen.getByText("complianceStatusMismatch")).toBeInTheDocument();
    expect(screen.getByText("Required section is missing.")).toBeInTheDocument();
    expect(screen.getByText("findingExpectedValue|Business Case section")).toBeInTheDocument();

    const newest = screen.getByText("historyActor: Dana Analyst");
    const oldest = screen.getByText("historyActor: Omar Lead");
    expect(newest.compareDocumentPosition(oldest) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("shows a load error for readiness and still allows the authorized action to remain available", async () => {
    mocks.readiness = undefined;
    mocks.readinessError = true;
    renderSection();

    expect(screen.getByText("loadFailed")).toBeInTheDocument();
    mocks.refetchReadiness.mockClear();
    fireEvent.click(screen.getByRole("button", { name: "retry" }));
    expect(mocks.refetchReadiness).toHaveBeenCalledTimes(1);
    expect(mocks.changePhase).not.toHaveBeenCalled();
  });

  it("shows empty phase and empty history states without inventing lifecycle data", () => {
    // Only the current phase is returned: no valid target phase exists.
    mocks.lifecycleData = { phases: [lifecyclePhases[1]], statuses: [] };
    renderSection();

    expect(screen.getByText("noOtherPhasesAvailable")).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Pre-Initiation" })).not.toBeInTheDocument();
    expect(screen.getByText("historyEmpty")).toBeInTheDocument();
    expect(changeButton()).toBeDisabled();
  });
});
