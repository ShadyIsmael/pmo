import type { ProjectPersonDto } from "./api/projects";

type ExistingRelationship = Pick<ProjectPersonDto, "resourceId" | "relationshipType" | "status">;

export type ProjectPersonSubmitAction =
  | "saveProjectPerson"
  | "addToProject"
  | "reactivate"
  | "addBackToProject"
  | "assignSponsor";

export function hasActiveProjectPerson(
  people: ExistingRelationship[],
  resourceId: string,
  relationshipType: string,
): boolean {
  return Boolean(resourceId) && people.some(
    (person) => person.resourceId === resourceId
      && person.relationshipType === relationshipType
      && person.status === "Active",
  );
}

export function hasInvalidProjectPersonDateRange(
  relationshipType: string,
  startDate: string,
  endDate: string,
): boolean {
  return relationshipType === "TeamMember"
    && Boolean(startDate && endDate)
    && endDate <= startDate;
}

export function getProjectPersonSubmitAction(
  people: ExistingRelationship[],
  isEditing: boolean,
  resourceId: string,
  relationshipType: string,
): ProjectPersonSubmitAction {
  if (isEditing) return "saveProjectPerson";
  if (!resourceId) return "addToProject";

  const hasHistoricalRelationship = people.some(
    (person) => person.resourceId === resourceId
      && person.relationshipType === relationshipType
      && person.status !== "Active",
  );

  if (hasHistoricalRelationship) {
    if (relationshipType === "TeamMember") return "reactivate";
    if (relationshipType === "Stakeholder" || relationshipType === "Other") return "addBackToProject";
    if (relationshipType === "Sponsor") return "assignSponsor";
  }

  return relationshipType === "Sponsor" ? "assignSponsor" : "addToProject";
}
