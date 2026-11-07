import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const failures = [];
const minimumPatchedNodeRelease = new Map([
  [22, [22, 23, 4]],
  [24, [24, 22, 1]],
  [26, [26, 12, 1]],
]);

function isPatchedNodeRelease(value) {
  const match = /^(\d+)\.(\d+)\.(\d+)(?:[-+].*)?$/.exec(value);
  if (!match) return false;

  const version = match.slice(1).map(Number);
  const minimum = minimumPatchedNodeRelease.get(version[0]);
  if (!minimum) return false;

  for (let index = 0; index < minimum.length; index += 1) {
    if (version[index] > minimum[index]) return true;
    if (version[index] < minimum[index]) return false;
  }
  return true;
}

function isNextPackage(name) {
  return name === 'next' || name.startsWith('@next/');
}

function inspectManifest(path) {
  if (!existsSync(path)) return;
  let manifest;
  try {
    manifest = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    failures.push(`${relative(repositoryRoot, path)} is not valid JSON: ${error.message}`);
    return;
  }

  for (const section of ['dependencies', 'devDependencies', 'optionalDependencies', 'peerDependencies']) {
    for (const name of Object.keys(manifest[section] ?? {})) {
      if (isNextPackage(name)) failures.push(`${relative(repositoryRoot, path)} declares discontinued package ${name}`);
    }
  }

  for (const [name, command] of Object.entries(manifest.scripts ?? {})) {
    if (/\bnext\s+(?:dev|build|start|export|lint|telemetry)\b/i.test(command)) {
      failures.push(`${relative(repositoryRoot, path)} script "${name}" invokes Next.js`);
    }
  }
}

function inspectLockfile(path) {
  if (!existsSync(path)) return;
  let lockfile;
  try {
    lockfile = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    failures.push(`${relative(repositoryRoot, path)} is not valid JSON: ${error.message}`);
    return;
  }

  const packagePaths = Object.keys(lockfile.packages ?? {});
  for (const packagePath of packagePaths) {
    if (/(?:^|\/)node_modules\/(?:next|@next\/[^/]+)$/.test(packagePath)) {
      failures.push(`${relative(repositoryRoot, path)} retains discontinued package ${packagePath}`);
    }
  }

  const visitDependencies = (dependencies = {}) => {
    for (const [name, entry] of Object.entries(dependencies)) {
      if (isNextPackage(name)) failures.push(`${relative(repositoryRoot, path)} retains discontinued package ${name}`);
      visitDependencies(entry.dependencies);
    }
  };
  visitDependencies(lockfile.dependencies);
}

function collectFiles(directory, allowedExtensions, results = []) {
  if (!existsSync(directory)) return results;
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      if (!['.git', '.next', 'node_modules', 'dist', 'build', 'coverage', 'obj', 'bin'].includes(entry.name.toLowerCase())) {
        collectFiles(path, allowedExtensions, results);
      }
    } else if (
      allowedExtensions.has(extname(entry.name).toLowerCase()) ||
      entry.name.toLowerCase().startsWith('dockerfile') ||
      entry.name.toLowerCase().startsWith('.env')
    ) {
      results.push(path);
    }
  }
  return results;
}

const obsoleteApp = join(repositoryRoot, 'apps', 'web');
if (existsSync(obsoleteApp)) failures.push('apps/web still exists; React/Vite is the only supported frontend');

for (const path of [
  join(repositoryRoot, 'package.json'),
  join(repositoryRoot, 'apps', 'web-react', 'package.json'),
]) inspectManifest(path);

for (const path of [
  join(repositoryRoot, 'package-lock.json'),
  join(repositoryRoot, 'apps', 'web-react', 'package-lock.json'),
]) inspectLockfile(path);

const operationalDirectories = [
  '.github',
  'apps',
  'packages',
  'scripts',
  'deploy',
  'deployment',
  'infra',
].map((path) => join(repositoryRoot, path));
const operationalExtensions = new Set([
  '.json', '.jsonc', '.yaml', '.yml', '.toml', '.js', '.jsx', '.mjs', '.cjs', '.ts', '.tsx', '.sh', '.ps1', '.cmd', '.bat', '.dockerfile',
]);
const operationalFiles = operationalDirectories.flatMap((directory) => collectFiles(directory, operationalExtensions));
for (const entry of readdirSync(repositoryRoot, { withFileTypes: true })) {
  if (
    entry.isFile() &&
    (/^(?:docker-compose.*|compose.*|dockerfile.*)$/i.test(entry.name) ||
      ['package.json', 'package-lock.json'].includes(entry.name.toLowerCase()) ||
      entry.name.toLowerCase().startsWith('.env'))
  ) {
    operationalFiles.push(join(repositoryRoot, entry.name));
  }
}

const trackedFiles = new Set(
  execFileSync('git', ['ls-files', '--cached'], { cwd: repositoryRoot, encoding: 'utf8' })
    .split(/\r?\n/)
    .filter(Boolean),
);

for (const path of operationalFiles) {
  if (resolve(path) === resolve(fileURLToPath(import.meta.url))) continue;

  const basename = path.split(/[\\/]/).at(-1)?.toLowerCase();
  const relativePath = relative(repositoryRoot, path).replaceAll('\\', '/');
  // Local environment files can contain private, untracked settings. CI checks
  // tracked environment templates and deployment files; untracked local files
  // are not part of the application or its deployment configuration.
  if (basename?.startsWith('.env') && !trackedFiles.has(relativePath)) continue;

  if (basename === 'package.json') inspectManifest(path);
  if (basename === 'package-lock.json') inspectLockfile(path);

  const source = readFileSync(path, 'utf8');
  const file = relative(repositoryRoot, path);
  if (/apps[\\/]web(?=[\\/"'\s]|$)/i.test(source)) {
    failures.push(`${file} contains an obsolete apps/web path`);
  }
  if (/\bnext\s+(?:dev|build|start|export|lint|telemetry)\b/i.test(source)) {
    failures.push(`${file} contains a Next.js command`);
  }
  if (/\b(?:from\s+|import\s*|require\s*\()\s*["'](?:next|@next\/)[^"']*["']/i.test(source)) {
    failures.push(`${file} imports the discontinued Next.js framework or its packages`);
  }
  if (/\.next[\\/]/i.test(source) || /next\.config\.(?:js|mjs|cjs|ts)/i.test(source)) {
    failures.push(`${file} contains a Next.js build or configuration path`);
  }
  if (/\bNEXT_PUBLIC_[A-Z0-9_]+\b/.test(source)) {
    failures.push(`${file} contains a Next.js-only public environment variable`);
  }

  for (const match of source.matchAll(/^\s*FROM\s+(?:--platform=\S+\s+)?(?:\S+\/)?node:([^\s]+)(?:\s+AS\s+\S+)?\s*(?:#.*)?$/gim)) {
    const imageVersion = /^(\d+\.\d+\.\d+(?:[-+].*)?)/.exec(match[1])?.[1];
    if (!imageVersion || !isPatchedNodeRelease(imageVersion)) {
      failures.push(`${file} uses an unpinned or unfixed Node.js base image version: ${match[1]}`);
    }
  }

  for (const match of source.matchAll(/^\s*node-version\s*:\s*["']?([^"'#\s]+)["']?\s*(?:#.*)?$/gim)) {
    if (!isPatchedNodeRelease(match[1])) {
      failures.push(`${file} uses an unpinned or unfixed Node.js runner version: ${match[1]}`);
    }
  }
}

if (failures.length) {
  console.error('Supported frontend policy failed:');
  for (const failure of [...new Set(failures)]) console.error(`- ${failure}`);
  process.exitCode = 1;
} else {
  console.log('Supported frontend policy passed: React/Vite only; no Next.js path or unfixed Node.js base/runner found.');
}
