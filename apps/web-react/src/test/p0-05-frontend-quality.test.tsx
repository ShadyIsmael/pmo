import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { describe, expect, it } from "vitest";

const srcRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const en = JSON.parse(readFileSync(path.resolve(srcRoot, "i18n/messages/en.json"), "utf8"));
const ar = JSON.parse(readFileSync(path.resolve(srcRoot, "i18n/messages/ar.json"), "utf8"));

describe("p0-05 frontend quality", () => {
  it("localizes login email placeholder in EN and AR catalogs", () => {
    expect(en.auth?.emailPlaceholder).toBeTypeOf("string");
    expect(en.auth.emailPlaceholder.length).toBeGreaterThan(0);
    expect(ar.auth?.emailPlaceholder).toBeTypeOf("string");
    expect(ar.auth.emailPlaceholder.length).toBeGreaterThan(0);
  });
});