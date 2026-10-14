import { describe, expect, it } from "vitest";
import {
  getProjectPersonSubmitAction,
  hasActiveProjectPerson,
  hasInvalidProjectPersonDateRange,
} from "../features/projects/project-member-rules";
import type { ProjectPersonDto } from "../features/projects/api/projects";

const relationship = (resourceId: string, relationshipType: string, status: string) => ({
  resourceId,
  relationshipType,
  status,
}) as Pick<ProjectPersonDto, "resourceId" | "relationshipType" | "status">;

describe("project member rules", () => {
  it("only blocks an active duplicate of the same relationship", () => {
    const people = [relationship("resource-1", "TeamMember", "Active")];

    expect(hasActiveProjectPerson(people, "resource-1", "TeamMember")).toBe(true);
    expect(hasActiveProjectPerson(people, "resource-1", "Stakeholder")).toBe(false);
    expect(hasActiveProjectPerson([relationship("resource-1", "TeamMember", "Removed")], "resource-1", "TeamMember")).toBe(false);
  });

  it("requires a team member end date to be strictly after the start date", () => {
    expect(hasInvalidProjectPersonDateRange("TeamMember", "2026-10-01", "2026-10-01")).toBe(true);
    expect(hasInvalidProjectPersonDateRange("TeamMember", "2026-10-02", "2026-10-01")).toBe(true);
    expect(hasInvalidProjectPersonDateRange("TeamMember", "2026-10-01", "2026-10-02")).toBe(false);
    expect(hasInvalidProjectPersonDateRange("TeamMember", "2026-10-01", "")).toBe(false);
    expect(hasInvalidProjectPersonDateRange("Stakeholder", "2026-10-02", "2026-10-01")).toBe(false);
  });

  it("uses the source action label for edits, reactivation, re-adding, and sponsors", () => {
    const history = [
      relationship("team-1", "TeamMember", "Removed"),
      relationship("stakeholder-1", "Stakeholder", "Revoked"),
      relationship("other-1", "Other", "Removed"),
    ];

    expect(getProjectPersonSubmitAction(history, true, "team-1", "TeamMember")).toBe("saveProjectPerson");
    expect(getProjectPersonSubmitAction(history, false, "", "TeamMember")).toBe("addToProject");
    expect(getProjectPersonSubmitAction(history, false, "team-1", "TeamMember")).toBe("reactivate");
    expect(getProjectPersonSubmitAction(history, false, "stakeholder-1", "Stakeholder")).toBe("addBackToProject");
    expect(getProjectPersonSubmitAction(history, false, "other-1", "Other")).toBe("addBackToProject");
    expect(getProjectPersonSubmitAction(history, false, "new-sponsor", "Sponsor")).toBe("assignSponsor");
    expect(getProjectPersonSubmitAction(history, false, "new-person", "TeamMember")).toBe("addToProject");
  });
});
