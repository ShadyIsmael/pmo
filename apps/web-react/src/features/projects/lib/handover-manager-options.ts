import type { ProjectHandoverManagerDto } from "../api/projects";

export interface HandoverManagerLabels {
  projectManager: string;
  portfolioManager: string;
  programManager: string;
}

export interface HandoverManagerOption {
  value: string;
  label: string;
  keywords: string[];
}

export function toHandoverManagerOptions(
  managers: readonly ProjectHandoverManagerDto[] | undefined,
  labels: HandoverManagerLabels,
): HandoverManagerOption[] {
  return (managers ?? []).map((manager) => {
    const managerTypes = manager.managerTypes.map((type) => {
      if (type === "PortfolioManager") return labels.portfolioManager;
      if (type === "ProgramManager") return labels.programManager;
      return labels.projectManager;
    });

    return {
      value: manager.id,
      label: `${manager.fullName} · ${managerTypes.join(" / ")} · ${manager.email}`,
      keywords: [manager.jobTitle ?? "", ...managerTypes],
    };
  });
}
