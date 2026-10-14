export type PlanningImportTranslator = (key: string, params?: Record<string, string | number>) => string;

type ImportMessage = { code: string; message: string; count?: number };

const knownApiErrors: Array<{ matches: (message: string) => boolean; key: string }> = [
  { matches: (message) => message === "only microsoft project .mpp files are supported", key: "unsupportedFile" },
  { matches: (message) => message === "the uploaded file cannot be read", key: "unreadableFile" },
  { matches: (message) => message === "the uploaded mpp file is empty", key: "emptyFile" },
  { matches: (message) => message.startsWith("the file could not be read as a microsoft project mpp file"), key: "invalidMpp" },
  { matches: (message) => message === "the mpp file does not contain any importable tasks", key: "noImportableTasks" },
  { matches: (message) => message === "a project is required", key: "projectRequired" },
  { matches: (message) => message === "the selected plan was not found in this project", key: "planNotFound" },
  { matches: (message) => message === "the import review session was not found", key: "sessionNotFound" },
  { matches: (message) => message === "the imported resource was not found", key: "resourceNotFound" },
  { matches: (message) => message === "the imported assignment was not found", key: "assignmentNotFound" },
  { matches: (message) => message === "the imported calendar was not found", key: "calendarNotFound" },
  { matches: (message) => message === "the selected resource was not found", key: "resourceNotFound" },
  { matches: (message) => message.startsWith("resource resolution must be"), key: "invalidResourceResolution" },
  { matches: (message) => message === "choose an active nexus resource to map", key: "chooseActiveResource" },
  { matches: (message) => message === "inactive resources cannot be mapped to an import", key: "inactiveResource" },
  { matches: (message) => message === "provide the resource details before creating a new resource", key: "resourceDetailsRequired" },
  { matches: (message) => message === "resource type must be staff or vendorresource", key: "invalidResourceType" },
  { matches: (message) => message === "a vendor is required when creating a vendorresource", key: "vendorRequired" },
  { matches: (message) => message === "assignment resolution must be ready or ignore", key: "invalidAssignmentResolution" },
  { matches: (message) => message === "calendar resolution must be import, simplify, or ignore", key: "invalidCalendarResolution" },
  { matches: (message) => message === "the import review session was cancelled", key: "sessionCancelled" },
  { matches: (message) => message === "resolve all blocking import issues before committing", key: "blockingIssues" },
  { matches: (message) => message === "the import project does not match the review session", key: "projectMismatch" },
  { matches: (message) => message === "a plan type is required when creating a new plan", key: "planTypeRequired" },
  { matches: (message) => message === "a mapped resource is missing its id", key: "resourceMappingMissingId" },
  { matches: (message) => message === "a mapped resource is no longer active", key: "inactiveResource" },
  { matches: (message) => message === "a new resource definition is missing", key: "resourceDetailsRequired" },
  { matches: (message) => message.startsWith("a plan of this type already exists for the project"), key: "planTypeConflict" },
  { matches: (message) => message === "the selected plan changed during import. reload and try again.", key: "planChanged" },
  { matches: (message) => message.startsWith("the plan import could not be persisted atomically"), key: "atomicSaveFailed" },
  { matches: (message) => message === "a committed import cannot be cancelled", key: "committedCannotCancel" },
];

export function planningImportNoticeMessage(notice: ImportMessage, t: PlanningImportTranslator): string {
  const key = `planning.import.notices.${notice.code}`;
  const name = notice.message.match(/'([^']+)'/)?.[1] ?? "";
  const translated = t(key, { name });
  return translated === key ? t("projects.planning.import.unknownNotice") : translated;
}

export function planningImportErrorMessage(cause: unknown, fallbackKey: string, t: PlanningImportTranslator): string {
  const rawMessage = cause instanceof Error ? cause.message.trim() : "";
  const normalized = rawMessage.toLowerCase();
  const match = knownApiErrors.find((candidate) => candidate.matches(normalized));
  return match ? t(`planning.import.errors.${match.key}`) : t(fallbackKey);
}
