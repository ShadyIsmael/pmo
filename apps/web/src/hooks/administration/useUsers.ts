"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";

export interface UserAdminDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jobTitle?: string;
  phone?: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
  lastLoginAt?: string;
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string;
  category?: string;
}

export interface CreateUserDto {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  jobTitle?: string;
  phone?: string;
  isActive: boolean;
  roles: string[];
}

export interface UpdateUserDto {
  email: string;
  password?: string;
  firstName: string;
  lastName: string;
  jobTitle?: string;
  phone?: string;
  isActive: boolean;
  roles: string[];
}

export function useAdminUsers(params: { page?: number; pageSize?: number; search?: string } = {}) {
  return useQuery({
    queryKey: ["users", "admin", params.page, params.pageSize, params.search],
    queryFn: () =>
      api.get<PagedResult<UserAdminDto>>("/api/users", {
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        search: params.search || undefined,
      }),
    staleTime: 30_000,
  });
}

export function useRoles() {
  return useQuery({
    queryKey: ["roles"],
    queryFn: () => api.get<RoleDto[]>("/api/roles"),
    staleTime: 60_000,
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateUserDto) => api.post<UserAdminDto>("/api/users", dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users", "admin"] });
      queryClient.invalidateQueries({ queryKey: ["users", "lookup"] });
      queryClient.invalidateQueries({ queryKey: ["users", "lookup", "project.manager"] });
    },
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: UpdateUserDto }) =>
      api.put<UserAdminDto>(`/api/users/${id}`, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users", "admin"] });
      queryClient.invalidateQueries({ queryKey: ["users", "lookup"] });
      queryClient.invalidateQueries({ queryKey: ["users", "lookup", "project.manager"] });
    },
  });
}
