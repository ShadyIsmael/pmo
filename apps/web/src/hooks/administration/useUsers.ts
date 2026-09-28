"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { userKeys } from "@/lib/api/users";

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
    queryKey: userKeys.admin(params),
    queryFn: () =>
      api.get<PagedResult<UserAdminDto>>(endpoints.users.root, {
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        search: params.search || undefined,
      }),
    staleTime: 30_000,
  });
}

export function useRoles() {
  return useQuery({
    queryKey: userKeys.roles,
    queryFn: () => api.get<RoleDto[]>(endpoints.users.roles),
    staleTime: 60_000,
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateUserDto) => api.post<UserAdminDto>(endpoints.users.root, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.all });
    },
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: UpdateUserDto }) =>
      api.put<UserAdminDto>(endpoints.users.detail(id), dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.all });
    },
  });
}
