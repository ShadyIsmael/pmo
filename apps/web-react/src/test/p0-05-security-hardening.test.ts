import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { describe, expect, it } from "vitest";

// Resolve nginx.conf relative to this test file (apps/web-react/nginx.conf).
const nginxConfPath = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "nginx.conf",
);

const nginxConf = readFileSync(nginxConfPath, "utf8");

function hasHeader(name: string, valueContains?: string): boolean {
  const regex = new RegExp(
    `add_header\\s+${name.replace(/[-/]/g, "\\$&")}\\s+"([^"]+)"\\s*always;`,
  );
  const match = nginxConf.match(regex);
  if (!match) return false;
  if (valueContains === undefined) return true;
  return match[1].includes(valueContains);
}

describe("P0-05 nginx security headers", () => {
  it("serves the SPA shell with baseline hardening headers", () => {
    // Required baseline headers from pentest F-07.
    expect(hasHeader("X-Content-Type-Options")).toBe(true);
    expect(hasHeader("X-Frame-Options")).toBe(true);
    expect(hasHeader("Referrer-Policy")).toBe(true);
    expect(hasHeader("Permissions-Policy")).toBe(true);
    expect(hasHeader("Content-Security-Policy")).toBe(true);
    expect(hasHeader("Cache-Control")).toBe(true);
  });

  it("disables clickjacking via X-Frame-Options DENY", () => {
    expect(hasHeader("X-Frame-Options", "DENY")).toBe(true);
  });

  it("refuses sniffing via X-Content-Type-Options nosniff", () => {
    expect(hasHeader("X-Content-Type-Options", "nosniff")).toBe(true);
  });

  it("uses a conservative Referrer-Policy", () => {
    expect(hasHeader("Referrer-Policy", "strict-origin-when-cross-origin")).toBe(true);
  });

  it("disables sensitive camera/microphone/geolocation/payment/usb capabilities", () => {
    expect(hasHeader("Permissions-Policy")).toBe(true);
    const match = nginxConf.match(
      /add_header\s+Permissions-Policy\s+"([^"]+)"\s*always;/,
    );
    expect(match).not.toBeNull();
    const policy = match![1];
    for (const feature of ["camera=()", "microphone=()", "geolocation=()", "payment=()", "usb=()"]) {
      expect(policy).toContain(feature);
    }
  });

  it("enforces a same-origin CSP suitable for a Vite React SPA", () => {
    const match = nginxConf.match(
      /add_header\s+Content-Security-Policy\s+"([^"]+)"\s*always;/,
    );
    expect(match).not.toBeNull();
    const csp = match![1];
    // Directives that must be present and strict.
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("script-src 'self'");
    expect(csp).toContain("connect-src 'self'");
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).toContain("base-uri 'self'");
    expect(csp).toContain("form-action 'self'");
    expect(csp).toContain("object-src 'none'");
    // Image/font allowlists needed for UI assets and data: URLs.
    expect(csp).toContain("img-src 'self' data: blob:");
    expect(csp).toContain("font-src 'self' data:");
  });

  it("allows only the external hosts required by Firebase web push", () => {
    const match = nginxConf.match(
      /add_header\s+Content-Security-Policy\s+"([^"]+)"\s*always;/,
    );
    expect(match).not.toBeNull();
    const directives = new Map(
      match![1].split(";").map((directive) => {
        const [name, ...sources] = directive.trim().split(/\s+/);
        return [name, sources] as const;
      }),
    );

    expect(directives.get("script-src")).toEqual(["'self'", "https://www.gstatic.com/firebasejs/12.19.0/"]);
    expect(directives.get("connect-src")).toEqual([
      "'self'",
      "https://firebaseinstallations.googleapis.com",
      "https://fcmregistrations.googleapis.com",
    ]);
    expect(match![1]).not.toContain("*.googleapis.com");
  });

  it("relaxes CSP only for runtime styles (unsafe-inline) and documents the tradeoff", () => {
    const match = nginxConf.match(
      /add_header\s+Content-Security-Policy\s+"([^"]+)"\s*always;/,
    );
    expect(match).not.toBeNull();
    // Tailwind/inline styles require 'unsafe-inline'; this is the accepted relaxation.
    expect(match![1]).toContain("style-src 'self' 'unsafe-inline'");
  });

  it("does not enable HSTS/HTTPS redirect that would break local HTTP dev", () => {
    // Strict-Transport-Security must NOT be set: nginx serves plain HTTP (port 80)
    // in Docker and is reachable over http://localhost in dev.
    expect(nginxConf).not.toMatch(/Strict-Transport-Security/);
    expect(nginxConf).not.toMatch(/return\s+301/);
  });

  it("headers are emitted with always so they survive error responses", () => {
    // Every add_header in the config uses the always flag.
    const directives = nginxConf.match(/add_header\s+\S+\s+"[^"]+"\s*always;/g);
    expect(directives).not.toBeNull();
    expect(directives!.length).toBeGreaterThanOrEqual(6);
  });
});
