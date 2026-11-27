import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearTokens, setTokens } from "../api/client";
import { documentApi } from "../features/projects/api/documents";

describe("Document Category create API contract", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    setTokens("category-create-test-token");
    fetchMock.mockReset().mockResolvedValue(new Response(JSON.stringify({
      id: "category-new",
      organizationId: "org-a",
      nameEn: "Governance",
      nameAr: "الحوكمة",
      isActive: true,
    }), { status: 201, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    clearTokens();
    vi.unstubAllGlobals();
  });

  it("accepts the API's 201 Created response and sends the organization-scoped request body", async () => {
    const input = { organizationId: "org-a", nameEn: "Governance", nameAr: "الحوكمة", description: "Governance documents" };

    await expect(documentApi.createCategory(input)).resolves.toMatchObject({
      id: "category-new",
      organizationId: "org-a",
      nameEn: "Governance",
      nameAr: "الحوكمة",
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    const [requestUrl, request] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(new URL(requestUrl).pathname).toBe("/api/document-categories");
    expect(request?.method).toBe("POST");
    expect(JSON.parse(String(request?.body))).toEqual(input);
  });

  it("loads project upload category options through the narrow lookup using the Project PublicId", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify([
      { id: "category-a", nameEn: "Policies", nameAr: "???????" },
    ]), { status: 200, headers: { "Content-Type": "application/json" } }));

    await expect(documentApi.projectUploadCategoryOptions("project-public-a")).resolves.toEqual([
      { id: "category-a", nameEn: "Policies", nameAr: "???????" },
    ]);

    const [requestUrl, request] = fetchMock.mock.calls.at(-1) as [string, RequestInit];
    const url = new URL(requestUrl);
    expect(url.pathname).toBe("/api/document-categories/project-upload-options");
    expect(url.searchParams.get("projectId")).toBe("project-public-a");
    expect(request?.method).toBe("GET");
  });

  it("permanently deletes by category ID through the dedicated API route", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(documentApi.deleteCategory("category-a")).resolves.toBeUndefined();

    const [requestUrl, request] = fetchMock.mock.calls.at(-1) as [string, RequestInit];
    expect(new URL(requestUrl).pathname).toBe("/api/document-categories/category-a");
    expect(request?.method).toBe("DELETE");
    expect(request?.body).toBeUndefined();
  });
});
