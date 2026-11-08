import { execFileSync, spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { mkdtempSync, rmSync, rmdirSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const frontendRoot = path.join(repositoryRoot, "apps", "web-react");
const certificateDirectory = mkdtempSync(path.join(tmpdir(), "nexus-pmo-vite-https-"));
const certificatePath = path.join(certificateDirectory, "localhost.pfx");
const certificatePassword = randomBytes(32).toString("base64url");
let vite;

try {
  execFileSync("dotnet", ["dev-certs", "https", "--trust"], {
    cwd: repositoryRoot,
    stdio: "inherit",
  });
  execFileSync("dotnet", ["dev-certs", "https", "--export-path", certificatePath, "--password", certificatePassword], {
    cwd: repositoryRoot,
    stdio: "inherit",
  });

  const npmCommand = process.platform === "win32" ? "npm.cmd" : "npm";
  vite = spawn(npmCommand, ["run", "dev:server"], {
    cwd: frontendRoot,
    stdio: "inherit",
    shell: process.platform === "win32",
    env: {
      ...process.env,
      NODE_USE_SYSTEM_CA: "1",
      NEXUS_DEV_HTTPS_PFX_PATH: certificatePath,
      NEXUS_DEV_HTTPS_PFX_PASSWORD: certificatePassword,
      VITE_API_URL: "",
    },
  });

  process.on("SIGINT", () => vite.kill("SIGINT"));
  process.on("SIGTERM", () => vite.kill("SIGTERM"));

  const exitCode = await new Promise((resolve) => {
    vite.once("error", () => resolve(1));
    vite.once("exit", (code, signal) => resolve(code ?? (signal ? 1 : 0)));
  });
  process.exitCode = exitCode;
} catch {
  console.error("Unable to prepare the trusted .NET HTTPS development certificate. The React app was not started.");
  process.exitCode = 1;
} finally {
  if (vite && vite.exitCode === null) vite.kill();
  rmSync(certificatePath, { force: true });
  rmdirSync(certificateDirectory);
}
