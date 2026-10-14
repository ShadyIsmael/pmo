import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface DocumentListItem {
  id: string; name: string; description?: string; fileName?: string; fileType?: string; fileSize?: number;
  contentType?: string; projectId?: string; categoryId?: string; categoryName?: string; status?: string;
  version: number; ownerId?: string; isTemplate: boolean; isArchived: boolean; sourceTemplateId?: string; sourceTemplateVersionNumber?: number; createdAt: string; updatedAt?: string;
}
export interface DocumentVersion { id: string; versionNumber: number; fileName?: string; fileSize: number; contentType?: string; changeNotes?: string; uploadedBy?: string; uploadedAt: string; isCurrent: boolean; }
export interface DocumentDetail { document: DocumentListItem; versions: DocumentVersion[]; }
export interface DocumentCategory { id: string; name: string; code?: string; description?: string; sortOrder: number; }
export interface DocumentListQuery { page?: number; pageSize?: number; projectId?: string; categoryId?: string; templatesOnly?: boolean; includeArchived?: boolean; search?: string; }

export const documentKeys = {
  all: ["documents"] as const,
  list: (query: DocumentListQuery) => ["documents", "list", query] as const,
  detail: (id: string) => ["documents", "detail", id] as const,
  categories: ["documents", "categories"] as const,
};

export const documentApi = {
  list: (query: DocumentListQuery = {}) => api.get<{ items: DocumentListItem[]; totalCount: number; page: number; pageSize: number; totalPages: number }>(endpoints.documents.root, { ...query, templatesOnly: query.templatesOnly ?? undefined } as Record<string, string | number | boolean | undefined>),
  detail: (id: string) => api.get<DocumentDetail>(endpoints.documents.detail(id)),
  categories: () => api.get<DocumentCategory[]>(endpoints.documents.categories),
  upload: (data: FormData) => api.upload<DocumentDetail>(endpoints.documents.root, data),
  uploadVersion: (id: string, data: FormData) => api.upload<DocumentDetail>(endpoints.documents.versions(id), data),
  archive: (id: string) => api.post<void>(endpoints.documents.archive(id), {}),
  download: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.download(id)}${version ? `?version=${version}` : ""}`),
  preview: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.preview(id)}${version ? `?version=${version}` : ""}`),
};

export function useProjectDocuments(query: DocumentListQuery = {}, enabled = true) {
  return useQuery({ queryKey: documentKeys.list(query), queryFn: () => documentApi.list(query), enabled, staleTime: 20_000 });
}
export function useProjectDocument(id: string | null) {
  return useQuery({ queryKey: documentKeys.detail(id ?? ""), queryFn: () => documentApi.detail(id!), enabled: !!id, staleTime: 20_000 });
}
export function useDocumentCategories(enabled = true) {
  return useQuery({ queryKey: documentKeys.categories, queryFn: documentApi.categories, enabled, staleTime: 60_000 });
}
export function useUploadDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.upload, onSuccess: () => { void client.invalidateQueries({ queryKey: documentKeys.all }); } });
}
export function useUploadDocumentVersion() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, data }: { id: string; data: FormData }) => documentApi.uploadVersion(id, data), onSuccess: (_result, values) => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: documentKeys.detail(values.id) }); } });
}
export function useArchiveDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.archive, onSuccess: () => { void client.invalidateQueries({ queryKey: documentKeys.all }); } });
}
