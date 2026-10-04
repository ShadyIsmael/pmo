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

const source = files(sourceRoot).map((file) => fs.readFileSync(file, "utf8")).join("\n");
const used = new Set();
for (const pattern of [
  /\bt\(\s*["']([^"']+)["']/g,
  /\blabelKey\s*:\s*["']([^"']+)["']/g,
  /\breasonKey\s*=\s*["']([^"']+)["']/g,
]) {
  for (const match of source.matchAll(pattern)) used.add(match[1]);
}

const available = leaves(messages);
const missing = [...used].filter((key) => !available.has(key)).sort();
if (missing.length) {
  console.error(`Missing React EN translation keys (${missing.length}):\n${missing.join("\n")}`);
  process.exitCode = 1;
} else {
  console.log(`PASS: React used-key validation (${used.size} literal keys)`);
}
