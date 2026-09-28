"use client";

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { KpiCard } from "@/components/ui/kpi-card";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import {
  FormSheet,
  FormSheetBody,
  FormSheetFooter,
} from "@/components/ui/form-sheet";
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from "@/components/ui/form";
import { SearchableMultiSelect } from "@/components/ui/searchable-select";
import {
  AlertCircle,
  Loader2,
  Plus,
  Filter,
  Search,
  Users,
  UserRoundCheck,
  Crown,
  Pencil,
} from "lucide-react";
import { getInitials } from "@/lib/utils";
import {
  useAdminUsers,
  useRoles,
  useCreateUser,
  useUpdateUser,
  type UserAdminDto,
} from "@/hooks/administration/useUsers";
import { useToast } from "@/hooks/use-toast";

const userSchema = z.object({
  firstName: z.string().trim().min(1, "First name is required").max(100),
  lastName: z.string().trim().min(1, "Last name is required").max(100),
  email: z.string().trim().email("Valid email is required").max(256),
  jobTitle: z.string().trim().max(200).optional().or(z.literal("")),
  phone: z.string().trim().max(50).optional().or(z.literal("")),
  password: z
    .string()
    .refine((v) => v === "" || v.length >= 6, "Password must be at least 6 characters")
    .optional(),
  isActive: z.boolean(),
  roles: z.array(z.string()).default([]),
});

type UserFormData = z.infer<typeof userSchema>;

interface UserFormSheetProps {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  user?: UserAdminDto | null;
}

function UserFormSheet({ open, onOpenChange, user }: UserFormSheetProps) {
  const t = useTranslations("administration.users");
  const common = useTranslations("common");
  const isEdit = !!user;
  const { toast } = useToast();
  const createMutation = useCreateUser();
  const updateMutation = useUpdateUser();
  const { data: roles } = useRoles();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const roleOptions = (roles ?? []).map((r) => ({
    value: r.name,
    label: r.name,
    keywords: [r.description ?? "", r.category ?? ""],
  }));

  const form = useForm<UserFormData>({
    resolver: zodResolver(userSchema),
    defaultValues: {
      firstName: "",
      lastName: "",
      email: "",
      jobTitle: "",
      phone: "",
      password: "",
      isActive: true,
      roles: [],
    },
  });

  useEffect(() => {
    if (!open) return;
    setErrorMessage(null);
    if (isEdit && user) {
      form.reset({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        jobTitle: user.jobTitle ?? "",
        phone: user.phone ?? "",
        password: "",
        isActive: user.isActive,
        roles: user.roles,
      });
    } else {
      form.reset({
        firstName: "",
        lastName: "",
        email: "",
        jobTitle: "",
        phone: "",
        password: "",
        isActive: true,
        roles: [],
      });
    }
    form.clearErrors();
  }, [open, user, isEdit, form]);

  const onSubmit = async (data: UserFormData) => {
    setErrorMessage(null);
    try {
      if (isEdit && user) {
        await updateMutation.mutateAsync({
          id: user.id,
          dto: {
            email: data.email,
            password: data.password || undefined,
            firstName: data.firstName,
            lastName: data.lastName,
            jobTitle: data.jobTitle || undefined,
            phone: data.phone || undefined,
            isActive: data.isActive,
            roles: data.roles,
          },
        });
        toast({ variant: "success", title: t("updateSuccess") });
      } else {
        await createMutation.mutateAsync({
          email: data.email,
          password: data.password || "Demo123!",
          firstName: data.firstName,
          lastName: data.lastName,
          jobTitle: data.jobTitle || undefined,
          phone: data.phone || undefined,
          isActive: data.isActive,
          roles: data.roles,
        });
        toast({ variant: "success", title: t("createSuccess") });
      }
      onOpenChange(false);
    } catch (err: unknown) {
      const error = err as { data?: { error?: string; message?: string }; message?: string };
      const message = error?.data?.error ?? error?.data?.message ?? error?.message;
      if (message && /email/i.test(message)) {
        form.setError("email", { type: "server", message });
      } else {
        setErrorMessage(message ?? t("error"));
      }
    }
  };

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  return (
    <FormSheet
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t("editUser") : t("addUser")}
      description={isEdit ? user?.email : undefined}
    >
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-0 flex-1 flex-col">
          <FormSheetBody className="space-y-5">
            {errorMessage && (
              <div className="flex items-center gap-2 p-3 bg-destructive/10 border border-destructive/20 rounded-md text-sm text-destructive">
                <AlertCircle className="h-4 w-4 flex-shrink-0" />
                {errorMessage}
              </div>
            )}

            <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="firstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("firstName")} <span className="text-destructive">*</span></FormLabel>
                    <FormControl>
                      <Input placeholder="First name" value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="lastName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("lastName")} <span className="text-destructive">*</span></FormLabel>
                    <FormControl>
                      <Input placeholder="Last name" value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("email")} <span className="text-destructive">*</span></FormLabel>
                  <FormControl>
                    <Input type="email" placeholder="name@nexus.com" value={field.value ?? ""} onChange={field.onChange} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="jobTitle"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("jobTitle")}</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g. Project Manager" value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("phone")}</FormLabel>
                    <FormControl>
                      <Input placeholder="+44 ..." value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="password"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("password")} {!isEdit && <span className="text-destructive">*</span>}</FormLabel>
                  <FormControl>
                    <Input type="password" placeholder={isEdit ? "••••••••" : "Min 6 characters"} value={field.value ?? ""} onChange={field.onChange} />
                  </FormControl>
                  {isEdit && <p className="text-xs text-muted-foreground">{t("passwordHint")}</p>}
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="roles"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("rolesLabel")}</FormLabel>
                  <FormControl>
                    <SearchableMultiSelect
                      options={roleOptions}
                      values={field.value ?? []}
                      onSelect={field.onChange}
                      placeholder={t("selectRolesPlaceholder")}
                      searchPlaceholder={common("search")}
                      loading={!roles}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center gap-2">
                  <FormControl>
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input accent-primary"
                      checked={field.value}
                      onChange={(e) => field.onChange(e.target.checked)}
                    />
                  </FormControl>
                  <FormLabel className="mb-0">{t("isActive")}</FormLabel>
                </FormItem>
              )}
            />
          </FormSheetBody>

          <FormSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {common("cancel")}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {isEdit ? t("saving") : t("creating")}
                </>
              ) : (
                common("save")
              )}
            </Button>
          </FormSheetFooter>
        </form>
      </Form>
    </FormSheet>
  );
}

export default function AdminUsersPage() {
  const t = useTranslations("administration.users");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [sheetOpen, setSheetOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserAdminDto | null>(null);

  const { data, isLoading, isError } = useAdminUsers({ page: 1, pageSize: 200, search: debouncedSearch });

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const users = useMemo(() => data?.items ?? [], [data]);
  const totalUsers = data?.totalCount ?? 0;
  const activeCount = users.filter((u) => u.isActive).length;
  const pmCount = users.filter((u) => u.roles.includes("project.manager")).length;
  const executiveCount = users.filter((u) => u.roles.includes("executive")).length;

  const openCreate = () => {
    setEditingUser(null);
    setSheetOpen(true);
  };

  const openEdit = (user: UserAdminDto) => {
    setEditingUser(user);
    setSheetOpen(true);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={<Button size="sm" onClick={openCreate}><Plus className="mr-2 h-4 w-4" />{t("addUser")}</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label={t("totalUsers")} value={totalUsers} icon={Users} semantic="primary" />
        <KpiCard label={t("activeUsers")} value={activeCount} icon={Users} semantic="green" accent="success" />
        <KpiCard label={t("projectManagers")} value={pmCount} icon={UserRoundCheck} semantic="indigo" />
        <KpiCard label={t("executives")} value={executiveCount} icon={Crown} semantic="amber" />
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1"><Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input placeholder={t("searchPlaceholder")} className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} /></div>
        <Button variant="outline" size="sm" disabled><Filter className="mr-2 h-4 w-4" />{t("filter")}</Button>
      </div>

      <div className="rounded-md border">
        <div className="nexus-table-header grid grid-cols-12 gap-2">
          <div className="col-span-3">{t("name")}</div>
          <div className="col-span-2">{t("email")}</div>
          <div className="col-span-2">{t("role")}</div>
          <div className="col-span-2">{t("jobTitle")}</div>
          <div className="col-span-2">{t("status")}</div>
          <div className="col-span-1"></div>
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : isError ? (
          <ErrorState title={t("error")} />
        ) : users.length === 0 ? (
          <EmptyState title={t("noUsers")} icon={Users} />
        ) : (
          users.map((user) => (
            <div key={user.id} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
              <div className="col-span-3 flex items-center gap-2 font-medium">
                <Avatar className="h-7 w-7"><AvatarFallback className="text-[10px]">{getInitials(user.fullName)}</AvatarFallback></Avatar>
                {user.fullName}
              </div>
              <div className="col-span-2 text-muted-foreground">{user.email}</div>
              <div className="col-span-2 flex flex-wrap gap-1">
                {user.roles.length === 0 ? (
                  <span className="text-muted-foreground">-</span>
                ) : (
                  user.roles.map((role) => (
                    <Badge key={role} variant={role === "project.manager" ? "default" : "outline"} className="text-[10px]">
                      {role}
                    </Badge>
                  ))
                )}
              </div>
              <div className="col-span-2 text-muted-foreground">{user.jobTitle || "-"}</div>
              <div className="col-span-2">
                <Badge status={user.isActive ? "Active" : "Inactive"} className="text-[10px]">
                  {user.isActive ? t("active") : t("inactive")}
                </Badge>
              </div>
              <div className="col-span-1">
                <Button variant="ghost" size="sm" onClick={() => openEdit(user)}>
                  <Pencil className="mr-1 h-3.5 w-3.5" />{t("edit")}
                </Button>
              </div>
            </div>
          ))
        )}
      </div>

      <UserFormSheet open={sheetOpen} onOpenChange={setSheetOpen} user={editingUser} />
    </div>
  );
}
