import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface DocumentListItem {
  id: string; name: string; description?: string; fileName?: string; fileType?: string; fileSize?: number;
  contentType?: string; projectId?: string; organizationId?: string; organizationName?: string; categoryId?: string; categoryName?: string; categoryNameAr?: string; status?: string;
  version: number; ownerId?: string; isTemplate: boolean; isMandatory?: boolean; isArchived: boolean; sourceTemplateId?: string; sourceTemplateVersionId?: string; sourceTemplateVersionNumber?: number; createdAt: string; updatedAt?: string;
}
export interface DocumentVersion { id: string; versionNumber: number; fileName?: string; fileSize: number; contentType?: string; changeNotes?: string; uploadedBy?: string; uploadedAt: string; isCurrent: boolean; }
export interface DocumentDetail { document: DocumentListItem; versions: DocumentVersion[]; }
export interface DocumentCategory {
  id: string;
  name?: string | null;
  nameEn?: string | null;
  nameAr?: string | null;
  code?: string | null;
  description?: string;
  sortOrder?: number;
  organizationId?: string | null;
  isActive?: boolean;
  usageCount?: number;
  createdAt?: string;
  updatedAt?: string;
  canManage?: boolean;
}
export interface DocumentCategoryQuery { organizationId?: string; projectId?: string; search?: string; includeInactive?: boolean; }
export interface SaveDocumentCategoryInput { organizationId?: string; nameEn: string; nameAr: string; description?: string; }
export interface TemplateCategoryOption { id: string; nameEn: string; nameAr?: string | null; }
export interface DocumentListQuery { page?: number; pageSize?: number; projectId?: string; forProjectId?: string; libraryForProjectId?: string; organizationId?: string; categoryId?: string; templatesOnly?: boolean; includeArchived?: boolean; search?: string; status?: string; }

export const documentKeys = {
  all: ["documents"] as const,
  list: (query: DocumentListQuery) => ["documents", "list", query] as const,
  detail: (id: string) => ["documents", "detail", id] as const,
  categories: (query: DocumentCategoryQuery = {}) => ["documents", "categories", query] as const,
};

export const documentApi = {
  list: (query: DocumentListQuery = {}) => api.get<{ items: DocumentListItem[]; totalCount: number; page: number; pageSize: number; totalPages: number }>(endpoints.documents.root, { ...query, templatesOnly: query.templatesOnly ?? undefined } as Record<string, string | number | boolean | undefined>),
  detail: (id: string) => api.get<DocumentDetail>(endpoints.documents.detail(id)),
  categories: (query: DocumentCategoryQuery = {}) => api.get<DocumentCategory[]>(endpoints.documents.categories, { ...query } as Record<string, string | number | boolean | undefined>),
  managedCategories: (query: DocumentCategoryQuery) => api.get<DocumentCategory[]>(endpoints.documentCategories.root, { ...query } as Record<string, string | number | boolean | undefined>),
  templateCategoryOptions: (organizationId: string) => api.get<TemplateCategoryOption[]>(endpoints.documentCategories.templateOptions, { organizationId }),
  projectUploadCategoryOptions: (projectPublicId: string) => api.get<TemplateCategoryOption[]>(endpoints.documentCategories.projectUploadOptions, { projectId: projectPublicId }),
  createCategory: (input: SaveDocumentCategoryInput) => api.post<DocumentCategory>(endpoints.documentCategories.root, input),
  updateCategory: ({ id, input }: { id: string; input: Omit<SaveDocumentCategoryInput, "organizationId"> }) => api.put<DocumentCategory>(endpoints.documentCategories.detail(id), input),
  setCategoryStatus: ({ id, isActive }: { id: string; isActive: boolean }) => api.patch<DocumentCategory>(endpoints.documentCategories.status(id), { isActive }),
  deleteCategory: (id: string) => api.delete<void>(endpoints.documentCategories.detail(id)),
  upload: (data: FormData) => api.upload<DocumentDetail>(endpoints.documents.root, data),
  uploadVersion: (id: string, data: FormData) => api.upload<DocumentDetail>(endpoints.documents.versions(id), data),
  archive: (id: string) => api.post<void>(endpoints.documents.archive(id), {}),
  activate: (id: string) => api.post<void>(endpoints.documents.activate(id), {}),
  download: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.download(id)}${version ? `?version=${version}` : ""}`),
  preview: (id: string, version?: number) => api.download<Blob>(`${endpoints.documents.preview(id)}${version ? `?version=${version}` : ""}`),
};

export function useProjectDocuments(query: DocumentListQuery = {}, enabled = true) {
  return useQuery({ queryKey: documentKeys.list(query), queryFn: () => documentApi.list(query), enabled, staleTime: 20_000 });
}
export function useProjectDocument(id: string | null) {
  return useQuery({ queryKey: documentKeys.detail(id ?? ""), queryFn: () => documentApi.detail(id!), enabled: !!id, staleTime: 20_000 });
}
export function useDocumentCategories(queryOrEnabled: DocumentCategoryQuery | boolean = {}, enabled = true) {
  const query = typeof queryOrEnabled === "boolean" ? {} : queryOrEnabled;
  const isEnabled = typeof queryOrEnabled === "boolean" ? queryOrEnabled : enabled;
  return useQuery({ queryKey: documentKeys.categories(query), queryFn: () => documentApi.categories(query), enabled: isEnabled, staleTime: 60_000 });
}
export function useManagedDocumentCategories(query: DocumentCategoryQuery, enabled = true) {
  return useQuery({ queryKey: ["documents", "managed-categories", query], queryFn: () => documentApi.managedCategories(query), enabled, staleTime: 30_000 });
}
export function useTemplateCategoryOptions(organizationId: string, enabled = true) {
  return useQuery({ queryKey: ["documents", "template-category-options", organizationId], queryFn: () => documentApi.templateCategoryOptions(organizationId), enabled: enabled && Boolean(organizationId), staleTime: 30_000 });
}
export function useProjectUploadCategoryOptions(projectPublicId: string, enabled = true) {
  return useQuery({ queryKey: ["documents", "project-upload-category-options", projectPublicId], queryFn: () => documentApi.projectUploadCategoryOptions(projectPublicId), enabled: enabled && Boolean(projectPublicId), staleTime: 30_000 });
}
export function useCreateDocumentCategory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.createCategory, onSuccess: () => { void client.invalidateQueries({ queryKey: ["documents", "managed-categories"] }); void client.invalidateQueries({ queryKey: ["documents", "template-category-options"] }); void client.invalidateQueries({ queryKey: ["documents", "categories"] }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useUpdateDocumentCategory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.updateCategory, onSuccess: () => { void client.invalidateQueries({ queryKey: ["documents", "managed-categories"] }); void client.invalidateQueries({ queryKey: ["documents", "template-category-options"] }); void client.invalidateQueries({ queryKey: ["documents", "categories"] }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useSetDocumentCategoryStatus() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.setCategoryStatus, onSuccess: () => { void client.invalidateQueries({ queryKey: ["documents", "managed-categories"] }); void client.invalidateQueries({ queryKey: ["documents", "template-category-options"] }); void client.invalidateQueries({ queryKey: ["documents", "categories"] }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useDeleteDocumentCategory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.deleteCategory, onSuccess: () => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useUploadDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.upload, onSuccess: () => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useUploadDocumentVersion() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, data }: { id: string; data: FormData }) => documentApi.uploadVersion(id, data), onSuccess: (_result, values) => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: documentKeys.detail(values.id) }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useArchiveDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.archive, onSuccess: () => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
export function useActivateDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.activate, onSuccess: (_result, id) => { void client.invalidateQueries({ queryKey: documentKeys.all }); void client.invalidateQueries({ queryKey: documentKeys.detail(id) }); void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] }); } });
}
