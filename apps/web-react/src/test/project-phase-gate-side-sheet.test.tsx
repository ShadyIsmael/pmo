import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ProjectPhaseGateSideSheet } from "@features/projects/components/ProjectPhaseGateSideSheet";
import type { ProjectDto } from "@features/projects/api/projects";

const mocks = vi.hoisted(() => ({
  readiness: undefined as any,
  refetch: vi.fn(),
  onOpenChange: vi.fn(),
  changePhase: vi.fn(),
  link: vi.fn(),
  unlink: vi.fn(),
  upload: vi.fn(),
  canMutate: true,
  effectiveReadOnly: false,
  permissions: ["Project.View", "Document.View", "ProjectPhase.Change"] as string[],
  lifecycleOptionsCalls: [] as Array<Record<string, unknown>>,
  readinessCalls: [] as unknown[][],
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: mocks.permissions } }),
}));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }) }));
vi.mock("@i18n/messages", () => ({
  useI18n: () => ({
    locale: "en",
    t: (key: string) => key.split(".").at(-1) ?? key,
    formatNumber: (value: number) => String(value),
  }),
}));
vi.mock("@hooks/use-toast", () => ({ toast: vi.fn() }));
vi.mock("@features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: (options: Record<string, unknown>) => {
    mocks.lifecycleOptionsCalls.push(options);
    return ({
    data: { phases: [{ id: "phase-initiation", code: "initiation", nameEn: "Initiation", nameAr: "Initiation", displayOrder: 10, isDefault: false, isActive: true }, { id: "phase-closure", code: "closure", nameEn: "Closure", nameAr: "Closure", displayOrder: 20, isDefault: false, isActive: true }], statuses: [] },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
    });
  },
  projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string; code: string } | undefined, locale: string, fallback?: string | null) => option ? (locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code) : fallback || "",
}));
vi.mock("@features/projects/api/documents", () => ({
  useProjectDocument: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectDocuments: () => ({ data: { items: [] }, isLoading: false, isError: false, refetch: vi.fn() }),
  useUploadDocument: () => ({ mutateAsync: mocks.upload, isPending: false, isError: false }),
}));
vi.mock("@features/projects/api/project-phase-gates", () => ({
  useProjectPhaseGateReadiness: (...args: unknown[]) => {
    mocks.readinessCalls.push(args);
    return { data: mocks.readiness, isLoading: false, isError: false, refetch: mocks.refetch };
  },
  useChangeProjectPhase: () => ({ mutateAsync: mocks.changePhase, isPending: false }),
  useLinkPhaseGateDocument: () => ({ mutateAsync: mocks.link, isPending: false, isError: false }),
  useUnlinkPhaseGateDocument: () => ({ mutateAsync: mocks.unlink, isPending: false, isError: false }),
}));
vi.mock("@components/ui/SideSheet", () => ({
  SideSheet: ({ open, title, description, children }: any) => open
    ? <section><header>{title}{description}</header>{children}</section>
    : null,
  SideSheetBody: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  SideSheetFooter: ({ children, ...props }: any) => <footer {...props}>{children}</footer>,
}));

describe("Project Phase Gate side sheet", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.permissions = ["Project.View", "Document.View", "ProjectPhase.Change"];
    mocks.lifecycleOptionsCalls = [];
    mocks.readinessCalls = [];
    mocks.readiness = {
      projectId: "project-id",
      organizationId: "organization-id",
      currentPhase: "pre-initiation",
      currentPhaseLabelEn: "Pre-Initiation",
      currentPhaseLabelAr: "ما قبل التأسيس",
      targetPhaseId: "phase-initiation",
      targetPhase: "initiation",
      targetPhaseLabelEn: "Initiation",
      targetPhaseLabelAr: "التأسيس",
      snapshotId: "snapshot-id",
      configurationId: "configuration-id",
      configurationVersionId: "configuration-version-id",
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
        },
      ],
    };
  });

  it("shows pinned requirements and keeps an authorized phase change available when a mandatory document is missing or mismatched", () => {
    render(<ProjectPhaseGateSideSheet
      open
      onOpenChange={mocks.onOpenChange}
      project={{ id: "project-id", publicId: "project-public-id", name: "Bridge Renewal", phase: "pre-initiation", rowVersion: "row-version" } as ProjectDto}
    />);

    expect(screen.getByText("Business Case")).toBeInTheDocument();
    expect(screen.getByText("Supporting Note")).toBeInTheDocument();
    expect(screen.getByText("configurationVersion")).toBeInTheDocument();
    expect(screen.getByText("transitionNonBlocking")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "changePhaseAction" })).toBeEnabled();
    expect(mocks.lifecycleOptionsCalls.at(-1)).toEqual({ enabled: true, projectId: "project-public-id" });
    expect(mocks.readinessCalls.at(-1)).toEqual(["project-public-id", undefined, true]);
    fireEvent.change(screen.getByRole("combobox", { name: "targetPhase" }), { target: { value: "phase-closure" } });
    expect(mocks.readinessCalls.at(-1)).toEqual(["project-public-id", undefined, true]);
  });

  it("does not expose phase mutation to a read-only viewer", () => {
    mocks.effectiveReadOnly = true;
    render(<ProjectPhaseGateSideSheet
      open
      onOpenChange={mocks.onOpenChange}
      project={{ id: "project-id", publicId: "project-public-id", name: "Bridge Renewal", phase: "pre-initiation", rowVersion: "row-version" } as ProjectDto}
    />);
    expect(screen.getByText("Business Case")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "changePhaseAction" })).not.toBeInTheDocument();
  });

  it("allows a direct phase change when no organization document gate applies", async () => {
    mocks.readiness = {
      projectId: "project-id",
      currentPhase: "pre-initiation",
      targetPhaseId: "phase-initiation",
      targetPhase: "initiation",
      readinessStatus: "NotApplicable",
      phaseTransitionStatus: "NotApplicable",
      mandatoryRequirementCount: 0,
      satisfiedMandatoryCount: 0,
      missingMandatoryCount: 0,
      optionalRequirementCount: 0,
      satisfiedOptionalCount: 0,
      projectRowVersion: "row-version",
      configurationVersionId: null,
      configurationVersionNumber: null,
      requirements: [],
    };
    render(<ProjectPhaseGateSideSheet
      open
      onOpenChange={mocks.onOpenChange}
      project={{ id: "project-id", publicId: "project-public-id", name: "Bridge Renewal", phase: "pre-initiation", rowVersion: "row-version" } as ProjectDto}
    />);

    const changeButton = screen.getByRole("button", { name: "changePhaseAction" });
    expect(screen.queryByText("mandatoryDocuments")).not.toBeInTheDocument();
    expect(changeButton).toBeEnabled();
    fireEvent.click(changeButton);
    await waitFor(() => expect(mocks.changePhase).toHaveBeenCalledWith(expect.objectContaining({ targetPhaseId: "phase-initiation", expectedProjectVersionToken: "row-version" })));
  });

  it("allows a user with Project.View and ProjectPhase.Change but no Document.View to change phase without exposing document details", async () => {
    mocks.permissions = ["Project.View", "ProjectPhase.Change"];
    mocks.readiness.requirements[0] = {
      ...mocks.readiness.requirements[0],
      documentId: "restricted-document-id",
      documentVersionId: "restricted-document-version-id",
      documentName: "Restricted Business Case",
      complianceFindings: [{ code: "MISSING_SECTION", messageEn: "Restricted finding", messageAr: "تفصيل مقيّد" }],
    };

    render(<ProjectPhaseGateSideSheet
      open
      onOpenChange={mocks.onOpenChange}
      project={{ id: "project-id", publicId: "project-public-id", name: "Bridge Renewal", phase: "pre-initiation", rowVersion: "row-version" } as ProjectDto}
    />);

    expect(screen.getByText("documentDetailsRestricted")).toBeInTheDocument();
    expect(screen.queryByText("Business Case")).not.toBeInTheDocument();
    expect(screen.queryByText("Restricted Business Case")).not.toBeInTheDocument();
    expect(screen.queryByText("complianceDetails")).not.toBeInTheDocument();
    expect(screen.queryByText("Restricted finding")).not.toBeInTheDocument();

    const changeButton = screen.getByRole("button", { name: "changePhaseAction" });
    expect(changeButton).toBeEnabled();
    fireEvent.click(changeButton);
    await waitFor(() => expect(mocks.changePhase).toHaveBeenCalledWith(expect.objectContaining({ targetPhaseId: "phase-initiation", expectedProjectVersionToken: "row-version" })));
  });
});
