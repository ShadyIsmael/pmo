import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { describe, expect, it } from "vitest";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiSourceRoot = path.resolve(testDirectory, "..", "..", "..", "..", "apps", "api", "src");
const client = readFileSync(path.resolve(testDirectory, "..", "api", "client.ts"), "utf8");
const authController = readFileSync(path.join(apiSourceRoot, "NEXUS.Modules.Identity", "Api", "AuthController.cs"), "utf8");
const identityRegistration = readFileSync(path.join(apiSourceRoot, "NEXUS.Modules.Identity", "ModuleRegistration", "IdentityModuleRegistration.cs"), "utf8");
const loginResponseDto = readFileSync(path.join(apiSourceRoot, "NEXUS.Modules.Identity", "Application", "LoginResponseDto.cs"), "utf8");

function methodBody(source: string, signature: string): string {
  const signatureIndex = source.indexOf(signature);
  if (signatureIndex < 0) throw new Error(`Method signature not found: ${signature}`);
  const bodyStart = source.indexOf("{", signatureIndex);
  if (bodyStart < 0) throw new Error(`Method body not found: ${signature}`);

  let depth = 0;
  for (let index = bodyStart; index < source.length; index += 1) {
    if (source[index] === "{") depth += 1;
    if (source[index] === "}") depth -= 1;
    if (depth === 0) return source.slice(bodyStart + 1, index);
  }
  throw new Error(`Method body is not balanced: ${signature}`);
}

describe("P0-07 browser authentication security contract", () => {
  it("keeps access and refresh JWTs out of localStorage and sessionStorage", () => {
    expect(client).not.toContain("sessionStorage");
    expect(client).not.toMatch(/(?:localStorage|sessionStorage)\.(?:getItem|setItem)\([^\n)]*(?:ACCESS_TOKEN_KEY|REFRESH_TOKEN_KEY)/);
    expect(client).toContain("let accessToken: string | null = null");
    expect(client).toContain("window.localStorage.setItem(SESSION_ID_KEY, clientSessionId)");
    expect(client).toContain("window.localStorage.removeItem(ACCESS_TOKEN_KEY)");
    expect(client).toContain("window.localStorage.removeItem(REFRESH_TOKEN_KEY)");
  });

  it("sets a host-only refresh cookie with secure browser attributes", () => {
    expect(authController).toContain('private const string RefreshCookieName = "__Host-nexus-refresh";');

    const setCookie = methodBody(authController, "private bool TrySetRefreshCookie(");
    const setOptions = setCookie.match(/new CookieOptions\s*\{([\s\S]*?)\}/)?.[1] ?? "";
    expect(setOptions).toContain("HttpOnly = true");
    expect(setOptions).toContain("Secure = true");
    expect(setOptions).toContain("SameSite = SameSiteMode.Strict");
    expect(setOptions).toContain('Path = "/"');
    expect(setOptions).not.toMatch(/\bDomain\s*=/);

    const deleteOptions = authController.match(/private void DeleteRefreshCookie\(\)[\s\S]*?new CookieOptions\s*\{([^}]*)\}/)?.[1] ?? "";
    expect(deleteOptions).toContain("HttpOnly = true");
    expect(deleteOptions).toContain("Secure = true");
    expect(deleteOptions).toContain("SameSite = SameSiteMode.Strict");
    expect(deleteOptions).toContain('Path = "/"');
    expect(deleteOptions).not.toMatch(/\bDomain\s*=/);
  });

  it("requires the antiforgery cookie and header for browser session transitions", () => {
    expect(identityRegistration).toContain('options.HeaderName = "X-Nexus-CSRF"');
    expect(identityRegistration).toContain('options.Cookie.Name = "__Host-nexus-csrf"');
    expect(identityRegistration).toContain("options.Cookie.HttpOnly = true");
    expect(identityRegistration).toContain("CookieSecurePolicy.Always");
    expect(identityRegistration).toContain("SameSiteMode.Strict");
    expect(identityRegistration).toContain('options.Cookie.Path = "/"');

    for (const signature of [
      "public async Task<IActionResult> Login(",
      "public async Task<IActionResult> Refresh(",
      "public async Task<IActionResult> Logout(",
    ]) {
      expect(methodBody(authController, signature)).toContain(
        "if (cookieMode && !await ValidateAntiforgeryRequestAsync(antiforgery)) return InvalidCsrfToken();",
      );
    }
  });

  it("sends refresh credentials only through the browser cookie and preserves legacy clients", () => {
    const refresh = methodBody(authController, "public async Task<IActionResult> Refresh(");
    const login = methodBody(authController, "public async Task<IActionResult> Login(");
    const authMode = methodBody(authController, "private bool TryGetAuthMode(");
    const authResponseType = client.match(/export interface AuthResponse\s*\{([^}]*)\}/)?.[1] ?? "";

    expect(refresh).toContain("Request.Cookies[RefreshCookieName]");
    expect(refresh).toContain("request?.RefreshToken");
    expect(refresh).toContain("result.Value.RefreshToken = null");
    expect(login).toContain("result.Value.RefreshToken = null");
    expect(loginResponseDto).toMatch(/JsonIgnore\(Condition\s*=\s*JsonIgnoreCondition\.WhenWritingNull\)[\s\S]*?public string\? RefreshToken/);
    expect(authResponseType).not.toMatch(/refreshToken/i);
    expect(authMode).toContain('!Request.Headers.ContainsKey("Origin") && !Request.Headers.ContainsKey("Sec-Fetch-Site")');
  });

  it("disables caching for every auth endpoint that returns account or session data", () => {
    const authMethods = [
      "public IActionResult GetCsrfToken(",
      "public async Task<IActionResult> Login(",
      "public async Task<IActionResult> Refresh(",
      "public async Task<IActionResult> Logout(",
      "public async Task<IActionResult> GetCurrentUser(",
      "public async Task<IActionResult> ChangeInitialPassword(",
      "public IActionResult GetInitialPasswordPolicy(",
      "public async Task<IActionResult> SwitchRole(",
    ];

    for (const signature of authMethods) {
      expect(methodBody(authController, signature)).toContain("DisableCaching();");
    }
    expect(authController).toContain('Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";');
    expect(authController).toContain('Response.Headers.Pragma = "no-cache";');
  });
});
