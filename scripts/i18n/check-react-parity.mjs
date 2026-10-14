import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = process.cwd();
const localeDir = path.join(root, "apps", "web-react", "src", "i18n", "messages");
const en = JSON.parse(fs.readFileSync(path.join(localeDir, "en.json"), "utf8"));
const ar = JSON.parse(fs.readFileSync(path.join(localeDir, "ar.json"), "utf8"));

function leaves(value, prefix = "", output = new Map()) {
  if (!value || typeof value !== "object") {
    if (typeof value === "string") output.set(prefix, value);
    return output;
  }
  for (const [key, child] of Object.entries(value)) leaves(child, prefix ? `${prefix}.${key}` : key, output);
  return output;
}

const enLeaves = leaves(en);
const arLeaves = leaves(ar);
const missingInAr = [...enLeaves.keys()].filter((key) => !arLeaves.has(key));
const missingInEn = [...arLeaves.keys()].filter((key) => !enLeaves.has(key));

if (missingInAr.length || missingInEn.length) {
  if (missingInAr.length) console.error(`Missing in React AR (${missingInAr.length}):\n${missingInAr.join("\n")}`);
  if (missingInEn.length) console.error(`Missing in React EN (${missingInEn.length}):\n${missingInEn.join("\n")}`);
  process.exitCode = 1;
} else {
  console.log(`PASS: React EN↔AR parity (${enLeaves.size} leaf keys)`);
}
