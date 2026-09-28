"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { documentApi, documentKeys, type DocumentListQuery } from "@/lib/api/documents";

export function useDocuments(query: DocumentListQuery = {}, enabled = true) {
  return useQuery({ queryKey: documentKeys.list(query), queryFn: () => documentApi.list(query), enabled, staleTime: 20_000 });
}
export function useDocument(id: string | null) {
  return useQuery({ queryKey: documentKeys.detail(id ?? ""), queryFn: () => documentApi.detail(id!), enabled: !!id, staleTime: 20_000 });
}
export function useDocumentCategories() {
  return useQuery({ queryKey: documentKeys.categories, queryFn: documentApi.categories, staleTime: 60_000 });
}
export function useUploadDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.upload, onSuccess: () => client.invalidateQueries({ queryKey: documentKeys.all }) });
}
export function useUploadDocumentVersion() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, data }: { id: string; data: FormData }) => documentApi.uploadVersion(id, data), onSuccess: (_result, values) => { client.invalidateQueries({ queryKey: documentKeys.all }); client.invalidateQueries({ queryKey: documentKeys.detail(values.id) }); } });
}
export function useArchiveDocument() {
  const client = useQueryClient();
  return useMutation({ mutationFn: documentApi.archive, onSuccess: () => client.invalidateQueries({ queryKey: documentKeys.all }) });
}
