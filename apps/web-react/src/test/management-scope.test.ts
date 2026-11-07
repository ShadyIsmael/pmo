import { describe, expect, it, vi } from "vitest";
import { api, clearTokens, setManagementRequestContext, setTokens } from "../api/client";

describe("management scope request context", () => {
  it("preserves perspective, read-only mode, and selected project headers", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({}), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access");
    setManagementRequestContext({ perspective: "ProjectManager", viewMode: "ReadOnlyViewAs", projectId: "project-1" });
    await api.get("/api/home/summary");
    const headers = new Headers(fetchMock.mock.calls[0][1].headers);
    expect(headers.get("Authorization")).toBe("Bearer access");
    expect(headers.get("X-Nexus-Perspective")).toBe("ProjectManager");
    expect(headers.get("X-Nexus-View-Mode")).toBe("ReadOnlyViewAs");
    expect(headers.get("X-Nexus-Project-Id")).toBe("project-1");
    setManagementRequestContext(null);
    clearTokens();
  });
});
