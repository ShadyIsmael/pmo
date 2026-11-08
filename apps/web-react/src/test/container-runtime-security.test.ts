import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const apiDockerfile = readFileSync(new URL("../../../../apps/api/Dockerfile", import.meta.url), "utf8");
const workerDockerfile = readFileSync(new URL("../../../../apps/api/Dockerfile.worker", import.meta.url), "utf8");
const webDockerfile = readFileSync(new URL("../../Dockerfile", import.meta.url), "utf8");
const compose = readFileSync(new URL("../../../../docker-compose.yml", import.meta.url), "utf8").replace(/\r\n/g, "\n");

function composeService(name: string): string {
  const servicesStart = compose.indexOf("services:");
  const serviceStart = compose.indexOf(`  ${name}:\n`, servicesStart);
  if (serviceStart < 0) return "";
  const bodyStart = serviceStart + name.length + 4;
  const nextService = compose.slice(bodyStart).search(/\n  [a-z][\w-]*:\s*$/m);
  return compose.slice(bodyStart, nextService < 0 ? undefined : bodyStart + nextService);
}

describe("container runtime identities", () => {
  it("runs API and worker processes as the .NET app UID", () => {
    expect(apiDockerfile).toMatch(/^USER \$APP_UID$/m);
    expect(workerDockerfile).toMatch(/^USER \$APP_UID$/m);
    for (const dockerfile of [apiDockerfile, workerDockerfile]) {
      expect(dockerfile).toContain("mkdir -p /app/uploads/documents /app/.isolated-data-protection");
      expect(dockerfile).toContain('chown -R "$APP_UID" /app/uploads/documents /app/.isolated-data-protection');
    }
  });

  it("runs nginx as its unprivileged user with a writable PID path", () => {
    expect(webDockerfile).toMatch(/^USER nginx$/m);
    expect(webDockerfile).toContain("chown -R nginx:nginx /var/cache/nginx");
    expect(webDockerfile).toContain("pid /var/cache/nginx/nginx.pid;");
  });

  it("limits app-container capabilities without changing published ports", () => {
    for (const name of ["api", "worker"]) {
      const service = composeService(name);
      expect(service).toContain("cap_drop:");
      expect(service).toContain("- ALL");
      expect(service).toContain("no-new-privileges:true");
    }

    const web = composeService("web");
    expect(web).toContain("cap_drop:");
    expect(web).toContain("- ALL");
    expect(web).toContain("- NET_BIND_SERVICE");
    expect(web).toContain("no-new-privileges:true");
    expect(web).toContain('"4000:80"');
  });

  it("protects stateful services from privilege escalation while retaining writable data", () => {
    const sqlserver = composeService("sqlserver");
    const redis = composeService("redis");

    expect(sqlserver).toContain("no-new-privileges:true");
    expect(sqlserver).toContain("sqlserver-data:/var/opt/mssql");
    expect(sqlserver).not.toMatch(/^\s+read_only:\s*true\s*$/m);

    expect(redis).toContain("no-new-privileges:true");
    expect(redis).toContain("redis-data:/data");
    expect(redis).not.toMatch(/^\s+read_only:\s*true\s*$/m);
  });
});
