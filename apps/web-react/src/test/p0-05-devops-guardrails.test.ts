import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { describe, expect, it } from "vitest";

const workflowPath = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
  "..",
  ".github",
  "workflows",
  "pr-validation.yml",
);

const workflow = readFileSync(workflowPath, "utf8");

describe("P0-05 devops guardrails", () => {
  it("workflow exists and contains key gates", () => {
    expect(workflow).toContain("pull_request");
    expect(workflow).toContain("typecheck");
    expect(workflow).toContain("lint");
    expect(workflow).toContain("i18n:parity");
  });

  it("contains no deployment or production secret keywords", () => {
    const lower = workflow.toLowerCase();
    expect(lower).not.toContain("deploy");
    expect(lower).not.toContain("production");
    expect(lower).not.toContain("azure_credentials");
  });
});
