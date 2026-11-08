import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

const certificateTrust = spawnSync("dotnet", ["dev-certs", "https", "--trust"], {
  cwd: repositoryRoot,
  stdio: "inherit",
});

if (certificateTrust.error || certificateTrust.status !== 0) {
  console.error("Unable to prepare the trusted .NET HTTPS development certificate. The API was not started.");
  process.exit(1);
}

const api = spawn("dotnet", [
  "run",
  "--project",
  "src/NEXUS.Api/NEXUS.Api.csproj",
  "--launch-profile",
  "NEXUS.Api",
], {
  cwd: path.join(repositoryRoot, "apps", "api"),
  stdio: "inherit",
  env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" },
});

process.on("SIGINT", () => api.kill("SIGINT"));
process.on("SIGTERM", () => api.kill("SIGTERM"));

api.once("error", () => {
  console.error("Unable to start the Development API process.");
  process.exitCode = 1;
});

api.once("exit", (code, signal) => {
  process.exitCode = code ?? (signal ? 1 : 0);
});
