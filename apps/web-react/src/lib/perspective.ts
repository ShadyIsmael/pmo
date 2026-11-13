export const MANAGEMENT_PERSPECTIVES = {
  ProjectManager: "ProjectManager",
  ProgramManager: "ProgramManager",
  PortfolioManager: "PortfolioManager",
} as const;

export type ManagementPerspective = (typeof MANAGEMENT_PERSPECTIVES)[keyof typeof MANAGEMENT_PERSPECTIVES];
export type ManagementViewMode = "Native" | "ReadOnlyViewAs";

export interface ManagementEntityReference {
  id: string;
  name: string;
  code?: string;
}

export interface PerspectiveAvailability {
  perspective: ManagementPerspective;
  isNative: boolean;
  requiresEntityContext: boolean;
  contexts: ManagementEntityReference[];
}

export interface ManagementScope {
  portfolioIds: string[];
  programIds: string[];
  projectIds: string[];
  organizationIds?: string[];
  organizationReferences?: ManagementEntityReference[];
}

export interface PerspectiveSnapshot {
  nativePerspectives: ManagementPerspective[];
  availablePerspectives: PerspectiveAvailability[];
  defaultPerspective?: ManagementPerspective;
  canViewAs: boolean;
  scope: ManagementScope;
  selectedContext?: {
    selectedPerspective: ManagementPerspective;
    viewMode: ManagementViewMode;
    selectedPortfolioId?: string;
    selectedProgramId?: string;
    selectedProjectId?: string;
    scope?: ManagementScope;
    effectiveReadOnly: boolean;
  };
}

export interface ManagementRequestContext {
  perspective: ManagementPerspective;
  viewMode: ManagementViewMode;
  portfolioId?: string;
  programId?: string;
  projectId?: string;
}

export const perspectiveOrder: ManagementPerspective[] = [
  MANAGEMENT_PERSPECTIVES.PortfolioManager,
  MANAGEMENT_PERSPECTIVES.ProgramManager,
  MANAGEMENT_PERSPECTIVES.ProjectManager,
];
