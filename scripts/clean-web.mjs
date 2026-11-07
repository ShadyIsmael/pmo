import { rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const webOutput = resolve(repositoryRoot, "apps", "web-react", "dist");
rmSync(webOutput, { recursive: true, force: true });