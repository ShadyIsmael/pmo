#!/usr/bin/env node
/**
 * Used-key i18n validator.
 * Scans source files for translation keys used via useTranslations("ns").t("key"), t("key"), etc.
 * Verifies each used key exists in both EN and AR locale trees.
 * Operates on apps/web/src/messages/{en,ar}.json.
 *
 * Exit codes:
 *   0 — all used keys present in both locales
 *   1 — missing keys found
 *   2 — load/parse failure
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, extname, relative, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, "..", "..");
const messagesDir = join(projectRoot, "apps", "web", "src", "messages");
const srcDir = join(projectRoot, "apps", "web", "src");

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

function flattenLeaves(node, prefix, out) {
  if (typeof node === "string") {
    out.set(prefix, node);
    return out;
  }
  if (!isPlainObject(node)) {
    out.set(prefix, node);
    return out;
  }
  for (const [key, value] of Object.entries(node)) {
    flattenLeaves(value, prefix ? `${prefix}.${key}` : key, out);
  }
  return out;
}

function collectSourceFiles(dir, out = []) {
  const entries = readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!entry.name.startsWith(".") && entry.name !== "node_modules" && entry.name !== ".next") {
        collectSourceFiles(full, out);
      }
    } else if (entry.isFile()) {
      const ext = extname(entry.name);
      if ([".ts", ".tsx", ".js", ".jsx"].includes(ext)) {
        out.push(full);
      }
    }
  }
  return out;
}

function extractUsedKeys(content, filePath, enLeaves, arLeaves, hookNamespaces = new Map()) {
  const keys = new Set();

  // Remove import statements to avoid false positives
  const contentWithoutImports = content
    .split('\n')
    .filter(line => !line.trim().startsWith('import ') && !line.trim().startsWith('export '))
    .join('\n');

  // Remove template literals with ${} to avoid dynamic keys
  const contentClean = contentWithoutImports
    .replace(/`[^`]*\$\{[^}]+\}[^`]*`/g, '')
    .replace(/['"`][^'"`]*\$\{[^}]+\}[^'"`]*['"`]/g, '');

  // const t = useTranslations("ns") / const t = useTranslations()
  const bound = new Map();
  const rootBound = new Set();
  const binderRegex = /const\s+([A-Za-z_$][\w$]*)\s*=\s*useTranslations\(\s*(?:['"`]([^'"`]+)['"`]\s*)?\)/g;
  let match;
  while ((match = binderRegex.exec(contentClean)) !== null) {
    if (match[2]) bound.set(match[1], match[2]);
    else rootBound.add(match[1]);
  }

  // const { t, locale } = useDashboardLabels() / const t = useSomeTranslatorHook()
  const destructureRegex = /const\s*\{([^}]+)\}\s*=\s*([A-Za-z_$][\w$]*)\(\s*\)/g;
  while ((match = destructureRegex.exec(contentClean)) !== null) {
    const ns = hookNamespaces.get(match[2]);
    if (!ns) continue;
    for (const part of match[1].split(',')) {
      const id = part.split(':')[0].trim();
      if (id === 't' || id === 'tc' || id === 'tf' || id === 'tw' || id === 'tr') bound.set(id, ns);
    }
  }
  const hookCallRegex = /const\s+([A-Za-z_$][\w$]*)\s*=\s*([A-Za-z_$][\w$]*)\(\s*\)/g;
  while ((match = hookCallRegex.exec(contentClean)) !== null) {
    const ns = hookNamespaces.get(match[2]);
    if (ns) bound.set(match[1], ns);
  }

  const namespaces = new Set(bound.values());
  // Presentational components may receive a namespaced translator as a prop.
  // They can declare that contract without coupling this validator to React
  // component semantics: `// i18n-namespace: administration.organization`.
  const declaredNamespace = contentClean.match(/i18n-namespace:\s*([A-Za-z0-9_.-]+)/)?.[1];
  if (declaredNamespace) namespaces.add(declaredNamespace);
  const useTranslationsRegex = /useTranslations\(\s*['"`]([^'"`]+)['"`]\s*\)/g;
  while ((match = useTranslationsRegex.exec(contentClean)) !== null) {
    namespaces.add(match[1]);
  }

  const exists = (key) => enLeaves.has(key) || arLeaves.has(key);

  function addKey(raw, preferredNs) {
    if (!raw || raw.includes('$') || raw.includes('{')) return;
    // Dynamic concat prefixes like t("planning.days." + value)
    if (raw.endsWith('.')) return;
    if (preferredNs !== undefined && preferredNs !== null) {
      const full = `${preferredNs}.${raw}`;
      if (exists(full)) {
        keys.add(full);
        return;
      }
      // Parameter shadowing: helper receives a different translator than the file-level binding
      for (const ns of namespaces) {
        const alt = `${ns}.${raw}`;
        if (exists(alt)) {
          keys.add(alt);
          return;
        }
      }
      keys.add(full);
      return;
    }
    // Fully-qualified leaf path
    if (raw.includes('.') && exists(raw)) {
      keys.add(raw);
      return;
    }
    // Resolve relative dotted keys against namespaces present in this file
    if (raw.includes('.') && namespaces.size > 0) {
      for (const ns of namespaces) {
        if (exists(`${ns}.${raw}`)) {
          keys.add(`${ns}.${raw}`);
          return;
        }
      }
      // Prefer first namespace so the miss is reported under a real ns
      keys.add(`${[...namespaces][0]}.${raw}`);
      return;
    }
    if (raw.includes('.')) {
      keys.add(raw);
      return;
    }
    // Bare key: expand across file namespaces
    if (namespaces.size > 0) {
      for (const ns of namespaces) keys.add(`${ns}.${raw}`);
      return;
    }
    keys.add(raw);
  }

  // Calls that look like translators (bound ids, root translators, common aliases).
  // Multi-arg form: t("key", { ... }) / t("key")
  const translatorAlias = /^(t|tc|tf|tw|tr|translate|tEnums|meetingT|common|enums)$/;

  const callRe = /(?<![A-Za-z0-9_$.])([A-Za-z_$][\w$]*)\(\s*(['"`])([^'"`\n]+)\2/g;
  while ((match = callRe.exec(contentClean)) !== null) {
    const id = match[1];
    const raw = match[3];
    // Skip the hook itself: useTranslations("ns")
    if (id === 'useTranslations') continue;
    // Skip obvious non-i18n calls with dotted string args (includes, matches, …)
    if (bound.has(id)) {
      addKey(raw, bound.get(id));
      continue;
    }
    if (rootBound.has(id)) {
      addKey(raw, null);
      continue;
    }
    if (translatorAlias.test(id)) {
      // Unbound alias: resolve via file namespaces / existence
      addKey(raw, null);
      continue;
    }
    // Ignore other functions (includes, confirm message strings, etc.)
  }

  return keys;
}

const en = load("en");
const ar = load("ar");

const enLeaves = flattenLeaves(en, "", new Map());
const arLeaves = flattenLeaves(ar, "", new Map());

const sourceFiles = collectSourceFiles(srcDir);

// Discover custom hooks that wrap useTranslations: function useX() { ... useTranslations("ns") ... }
const hookNamespaces = new Map();
for (const file of sourceFiles) {
  try {
    const content = readFileSync(file, "utf8");
    const fnRe = /(?:export\s+)?function\s+(use[A-Za-z0-9_]+)\s*\([^)]*\)\s*\{([\s\S]{0,800}?)\}/g;
    let fnMatch;
    while ((fnMatch = fnRe.exec(content)) !== null) {
      const nsMatch = fnMatch[2].match(/useTranslations\(\s*['"`]([^'"`]+)['"`]\s*\)/);
      if (nsMatch) hookNamespaces.set(fnMatch[1], nsMatch[1]);
    }
  } catch {
    // ignore unreadable files during hook discovery
  }
}

const allUsedKeys = new Set();
const fileKeyMap = new Map();

for (const file of sourceFiles) {
  try {
    const content = readFileSync(file, "utf8");
    const keys = extractUsedKeys(content, file, enLeaves, arLeaves, hookNamespaces);
    if (keys.size > 0) {
      fileKeyMap.set(file, keys);
      for (const key of keys) {
        allUsedKeys.add(key);
      }
    }
  } catch (err) {
    console.warn(`WARN: could not read ${file}: ${err.message}`);
  }
}

const missingInEn = [];
const missingInAr = [];
const missingInBoth = [];

for (const key of allUsedKeys) {
  const inEn = enLeaves.has(key);
  const inAr = arLeaves.has(key);
  
  if (!inEn && !inAr) {
    missingInBoth.push(key);
  } else if (!inEn) {
    missingInEn.push(key);
  } else if (!inAr) {
    missingInAr.push(key);
  }
}

const ok = missingInEn.length === 0 && missingInAr.length === 0 && missingInBoth.length === 0;

console.log(`Scanned ${sourceFiles.length} source files`);
console.log(`Found ${allUsedKeys.size} unique translation keys in source`);
console.log(`EN leaf keys: ${enLeaves.size}`);
console.log(`AR leaf keys: ${arLeaves.size}`);
console.log(`Missing in EN (${missingInEn.length}):`, missingInEn.length ? missingInEn.slice(0, 20) : "[]");
console.log(`Missing in AR (${missingInAr.length}):`, missingInAr.length ? missingInAr.slice(0, 20) : "[]");
console.log(`Missing in BOTH (${missingInBoth.length}):`, missingInBoth.length ? missingInBoth.slice(0, 20) : "[]");

if (!ok) {
  if (missingInBoth.length > 0) {
    console.error("\nERROR: The following keys are used in source but missing from BOTH locales:");
    for (const key of missingInBoth) {
      const files = [...fileKeyMap.entries()].filter(([, keys]) => keys.has(key)).map(([f]) => relative(projectRoot, f));
      console.error(`  ${key}  (in: ${files.join(", ")})`);
    }
  }
  if (missingInEn.length > 0) {
    console.error("\nERROR: The following keys are used in source but missing from EN:");
    for (const key of missingInEn) {
      const files = [...fileKeyMap.entries()].filter(([, keys]) => keys.has(key)).map(([f]) => relative(projectRoot, f));
      console.error(`  ${key}  (in: ${files.join(", ")})`);
    }
  }
  if (missingInAr.length > 0) {
    console.error("\nERROR: The following keys are used in source but missing from AR:");
    for (const key of missingInAr) {
      const files = [...fileKeyMap.entries()].filter(([, keys]) => keys.has(key)).map(([f]) => relative(projectRoot, f));
      console.error(`  ${key}  (in: ${files.join(", ")})`);
    }
  }
  console.error("\nFAIL: used-key validation issues found");
  process.exit(1);
}

console.log("PASS: all used keys exist in both EN and AR");
process.exit(0);
