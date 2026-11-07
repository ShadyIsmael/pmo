import identity from "../../brand.json";
import versionMeta from "../../../../version.json";

// Presentation only: preserve authentication, storage, API and database identifiers.
export const brand = identity;

export function brandCopy(locale: "en" | "ar") {
  return locale === "ar" ? brand.ar : brand.product;
}

/**
 * Centralized FOPX PM product version metadata.
 *
 * This is the single authoritative source for the product version,
 * shared by the frontend, backend, build metadata, and user-facing
 * About/System Information screens.
 */
export const productVersion = {
  version: versionMeta.version ?? "1.0.0-rc.1",
  buildDate: versionMeta.buildDate ?? null,
  gitCommit: versionMeta.gitCommit ?? null,
  status: versionMeta.status ?? "release-candidate",
  releaseNotesPath: versionMeta.releaseNotes ?? "docs/releases/1.0.0.md",
  changelogPath: versionMeta.changelog ?? "CHANGELOG.md",
  versioningPolicyPath: versionMeta.versioningPolicy ?? "docs/releases/VERSIONING-POLICY.md",
  releaseHistoryPath: versionMeta.releaseHistory ?? "docs/releases/RELEASE-HISTORY.md",
};

export function shortGitCommit(commit: string | null | undefined): string {
  if (!commit) return "unknown";
  return commit.length > 7 ? commit.slice(0, 7) : commit;
}

export function buildDateDisplay(date: string | null | undefined): string {
  return date || "unknown";
}

export function statusDisplay(status: string | null | undefined): string {
  return status || "release-candidate";
}