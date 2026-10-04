import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface MeetingDto { id: string; title: string; startTime: string; endTime: string; status?: string; projectName?: string; }
export interface WorkItemDto { id: string; title: string; type: string; status: string; priority?: string; projectName?: string; dueDate?: string; }
export interface DocumentDto { id: string; name: string; status: string; updatedAt?: string; ownerName?: string; }
export interface PortfolioDto { id: string; name: string; code?: string; status?: string; }
export interface ProgramDto { id: string; name: string; code?: string; status?: string; portfolioId?: string; }

export function useMeetings(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["meetings", params], queryFn: () => api.get<{ items: MeetingDto[]; totalCount: number }>(endpoints.meetings.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function useMeeting(id: string, enabled = true) {
  return useQuery({ queryKey: ["meetings", "detail", id], queryFn: () => api.get<MeetingDto>(endpoints.meetings.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useWorkItemsList(params: Record<string, string | number | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["workItems", params], queryFn: () => api.get<{ items: WorkItemDto[]; totalCount: number }>(endpoints.workItems.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function useWorkItem(id: string, enabled = true) {
  return useQuery({ queryKey: ["workItems", "detail", id], queryFn: () => api.get<WorkItemDto>(endpoints.workItems.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useDocuments(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["documents", params], queryFn: () => api.get<{ items: DocumentDto[]; totalCount: number }>(endpoints.documents.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function usePortfolios(enabled = true) {
  return useQuery({ queryKey: ["portfolios"], queryFn: () => api.get<{ items: PortfolioDto[] }>(endpoints.portfolios.root, { pageSize: 100 }), enabled, staleTime: 30_000 });
}
export function usePortfolio(id: string, enabled = true) {
  return useQuery({ queryKey: ["portfolios", "detail", id], queryFn: () => api.get<PortfolioDto>(endpoints.portfolios.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function usePrograms(enabled = true) {
  return useQuery({ queryKey: ["programs"], queryFn: () => api.get<{ items: ProgramDto[] }>(endpoints.programs.root, { pageSize: 100 }), enabled, staleTime: 30_000 });
}
