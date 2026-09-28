import { api, type PagedResult } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";

export interface DocumentListItem {
  id: string; name: string; description?: string; fileName?: string; fileType?: string; fileSize?: number;
  contentType?: string; projectId?: string; categoryId?: string; categoryName?: string; status?: string;
  version: number; ownerId?: string; isTemplate: boolean; isArchived: boolean; sourceTemplateId?: string; sourceTemplateVersionNumber?: number; createdAt: string; updatedAt?: string;
}
export interface DocumentVersion { id: string; versionNumber: number; fileName?: string; fileSize: number; contentType?: string; changeNotes?: string; uploadedBy?: string; uploadedAt: string; isCurrent: boolean; }
export interface DocumentDetail { document: DocumentListItem; versions: DocumentVersion[]; }
export interface DocumentCategory { id: string; name: string; code?: string; description?: string; sortOrder: number; }
export interface DocumentListQuery { page?: number; pageSize?: number; projectId?: string; categoryId?: string; templatesOnly?: boolean; includeArchived?: boolean; search?: string; }

export const documentKeys = { all: ["documents"] as const, list: (query: DocumentListQuery) => ["documents", "list", query] as const, detail: (id: string) => ["documents", "detail", id] as const, categories: ["documents", "categories"] as const };

export const documentApi = {
  list: (query: DocumentListQuery = {}) => api.get<PagedResult<DocumentListItem>>(endpoints.documents.root, { ...query }),
  detail: (id: string) => api.get<DocumentDetail>(endpoints.documents.detail(id)),
  categories: () => api.get<DocumentCategory[]>(endpoints.documents.categories),
  upload: (data: FormData) => api.upload<DocumentDetail>(endpoints.documents.root, data),
  uploadVersion: (id: string, data: FormData) => api.upload<DocumentDetail>(endpoints.documents.versions(id), data),
  archive: (id: string) => api.post<void>(endpoints.documents.archive(id), {}),
  download: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.download(id)}${version ? `?version=${version}` : ""}`),
  preview: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.preview(id)}${version ? `?version=${version}` : ""}`),
};

export function documentContentUrl(id: string, preview = false, version?: number) {
  const path = preview ? endpoints.documents.preview(id) : endpoints.documents.download(id);
  return `${path}${version ? `?version=${version}` : ""}`;
}
