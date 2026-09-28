#!/usr/bin/env node
/**
 * EN↔AR translation parity + namespace-shape validator.
 * Operates on apps/web/src/messages/{en,ar}.json (sole runtime source via i18n.ts).
 *
 * Exit codes:
 *   0 — full leaf-key parity, no shape mismatches
 *   1 — onlyEn / onlyAr / shapeMismatch present
 *   2 — load/parse failure
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const messagesDir = join(__dirname, "..", "..", "apps", "web", "src", "messages");

function load(locale) {
  const path = join(messagesDir, `${locale}.json`);
  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (err) {
    console.error(`FATAL: cannot load ${path}: ${err.message}`);
    process.exit(2);
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

/** Flatten to leaf string paths. Objects with no string leaves contribute no paths. */
function flattenLeaves(node, prefix, out) {
  if (typeof node === "string") {
    out.set(prefix, node);
    return out;
  }
  if (!isPlainObject(node)) {
    // numbers/booleans/null are treated as leaves for shape comparison
    out.set(prefix, node);
    return out;
  }
  for (const [key, value] of Object.entries(node)) {
    flattenLeaves(value, prefix ? `${prefix}.${key}` : key, out);
  }
  return out;
}

/** Record path → kind (string | object | other) at every branch for shape checks. */
function flattenShapes(node, prefix, out) {
  if (isPlainObject(node)) {
    out.set(prefix, "object");
    for (const [key, value] of Object.entries(node)) {
      flattenShapes(value, prefix ? `${prefix}.${key}` : key, out);
    }
  } else {
    out.set(prefix, typeof node);
  }
  return out;
}

const en = load("en");
const ar = load("ar");

const enLeaves = flattenLeaves(en, "", new Map());
const arLeaves = flattenLeaves(ar, "", new Map());
const enShapes = flattenShapes(en, "", new Map());
const arShapes = flattenShapes(ar, "", new Map());

const onlyEn = [...enLeaves.keys()].filter((k) => !arLeaves.has(k)).sort();
const onlyAr = [...arLeaves.keys()].filter((k) => !enLeaves.has(k)).sort();

const shapeMismatch = [];
const allShapePaths = new Set([...enShapes.keys(), ...arShapes.keys()]);
for (const path of allShapePaths) {
  const a = enShapes.get(path);
  const b = arShapes.get(path);
  if (a !== undefined && b !== undefined && a !== b) {
    shapeMismatch.push({ path, en: a, ar: b });
  }
}

const enTop = Object.keys(en).sort();
const arTop = Object.keys(ar).sort();
const onlyTopEn = enTop.filter((k) => !arTop.includes(k));
const onlyTopAr = arTop.filter((k) => !enTop.includes(k));

const ok = onlyEn.length === 0 && onlyAr.length === 0 && shapeMismatch.length === 0;

console.log(`en leaf keys: ${enLeaves.size}`);
console.log(`ar leaf keys: ${arLeaves.size}`);
console.log(`onlyEn (${onlyEn.length}):`, onlyEn.length ? onlyEn : "[]");
console.log(`onlyAr (${onlyAr.length}):`, onlyAr.length ? onlyAr : "[]");
console.log(`shapeMismatch (${shapeMismatch.length}):`, shapeMismatch.length ? shapeMismatch : "[]");
if (onlyTopEn.length || onlyTopAr.length) {
  console.log(`top-level onlyEn:`, onlyTopEn);
  console.log(`top-level onlyAr:`, onlyTopAr);
}
console.log(ok ? "PASS: full EN↔AR parity" : "FAIL: parity issues found");
process.exit(ok ? 0 : 1);
