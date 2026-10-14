import { describe, expect, it } from "vitest";
import { toHandoverManagerOptions } from "@features/projects/lib/handover-manager-options";

describe("toHandoverManagerOptions", () => {
  it("shows translated manager kinds for project, portfolio, and program candidates", () => {
    const options = toHandoverManagerOptions([
      { id: "project", fullName: "Pat Project", email: "pat@example.test", managerTypes: ["ProjectManager"] },
      { id: "portfolio", fullName: "Polly Portfolio", email: "polly@example.test", managerTypes: ["PortfolioManager"] },
      { id: "program", fullName: "Parker Program", email: "parker@example.test", jobTitle: "Delivery Lead", managerTypes: ["ProgramManager"] },
      { id: "multi", fullName: "Morgan Mixed", email: "morgan@example.test", managerTypes: ["ProjectManager", "ProgramManager"] },
    ], {
      projectManager: "Project Manager",
      portfolioManager: "Portfolio Manager",
      programManager: "Program Manager",
    });

    expect(options.map((option) => option.label)).toEqual([
      "Pat Project · Project Manager · pat@example.test",
      "Polly Portfolio · Portfolio Manager · polly@example.test",
      "Parker Program · Program Manager · parker@example.test",
      "Morgan Mixed · Project Manager / Program Manager · morgan@example.test",
    ]);
    expect(options[2].keywords).toContain("Delivery Lead");
  });

  it("returns an empty list when the eligible manager query has no results", () => {
    expect(toHandoverManagerOptions(undefined, {
      projectManager: "Project Manager",
      portfolioManager: "Portfolio Manager",
      programManager: "Program Manager",
    })).toEqual([]);
  });
});
