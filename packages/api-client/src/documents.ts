import type { ApiClient } from "./client";
import type { Document, DocumentVersion, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createDocumentsApi(client: ApiClient) {
  return {
    getDocuments: (projectId: string, params?: PaginationParams & { categoryId?: string }) =>
      client.getPaged<Document>(`/projects/${projectId}/documents`, params),

    uploadDocument: (projectId: string, formData: FormData) =>
      client.post<Document>(`/projects/${projectId}/documents`, formData, {}),

    getDocumentVersions: (documentId: string) =>
      client.get<readonly DocumentVersion[]>(`/documents/${documentId}/versions`),
  };
}
