import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = process.cwd();
const sourceRoot = path.join(root, "apps", "web-react", "src");
const messages = JSON.parse(fs.readFileSync(path.join(sourceRoot, "i18n", "messages", "en.json"), "utf8"));

function leaves(value, prefix = "", output = new Set()) {
  if (!value || typeof value !== "object") {
    if (typeof value === "string") output.add(prefix);
    return output;
  }
  for (const [key, child] of Object.entries(value)) leaves(child, prefix ? `${prefix}.${key}` : key, output);
  return output;
}

function files(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) return files(file);
    return /\.(ts|tsx)$/.test(entry.name) ? [file] : [];
  });
}

const used = new Set();
const scopedAliases = new Map([
  [path.join(sourceRoot, "features", "projects", "ProjectPlanningTabFull.tsx"), [["planning.", "projects.planning."]]],
  [path.join(sourceRoot, "features", "projects", "components", "PlanningImportSheet.tsx"), [["planning.", "projects.planning."]]],
  [path.join(sourceRoot, "features", "projects", "components", "PlanningGrid.tsx"), [["planning.", "projects.planning."]]],
  [path.join(sourceRoot, "features", "projects", "components", "PlanningCalendarsPanel.tsx"), [["planning.", "projects.planning."]]],
]);
const helperAliases = new Map([
  [path.join(sourceRoot, "features", "projects", "components", "PlanTimelineGantt.tsx"), [["", "projects.planning."]]],
  [path.join(sourceRoot, "features", "projects", "components", "BudgetRevisionSection.tsx"), [["", "projects.budgetRevisions."]]],
]);
for (const file of files(sourceRoot)) {
  const source = fs.readFileSync(file, "utf8");
  for (const { regex, aliases } of [
    { regex: /\bt\(\s*["']([^"']+)["']/g, aliases: scopedAliases.get(file) ?? [] },
    { regex: /\btr\(\s*["']([^"']+)["']/g, aliases: helperAliases.get(file) ?? [] },
    { regex: /\blabelKey\s*:\s*["']([^"']+)["']/g, aliases: scopedAliases.get(file) ?? [] },
    { regex: /\breasonKey\s*=\s*["']([^"']+)["']/g, aliases: scopedAliases.get(file) ?? [] },
  ]) {
    for (const match of source.matchAll(regex)) {
      const alias = aliases.find(([from]) => match[1].startsWith(from));
      used.add(alias ? `${alias[1]}${match[1].slice(alias[0].length)}` : match[1]);
    }
  }
}

const available = leaves(messages);
const missing = [...used].filter((key) => !available.has(key)).sort();
if (missing.length) {
  console.error(`Missing React EN translation keys (${missing.length}):\n${missing.join("\n")}`);
  process.exitCode = 1;
} else {
  console.log(`PASS: React used-key validation (${used.size} literal keys)`);
}
