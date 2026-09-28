"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useTranslations, useLocale } from "next-intl";
import { Button } from "@/components/ui/button";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Form, FormField, FormItem, FormLabel, FormControl, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { AlertCircle, Loader2, UserRoundCheck } from "lucide-react";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import {
  useCreateProjectHandoverRequest,
  type ProjectDto,
  type ProjectHandoverRequestDto,
} from "@/hooks/projects/useProjects";
import { formatDate } from "@/lib/utils";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

const handoverSchema = z.object({
  newProjectManagerId: z.string().min(1, "New project manager is required"),
  effectiveHandoverDate: z.string().min(1, "Effective handover date is required"),
  reason: z.string().trim().min(1, "Handover reason is required").max(500),
  notes: z.string().trim().max(2000).optional(),
});

type HandoverFormData = z.infer<typeof handoverSchema>;

interface ProjectHandoverSheetProps {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  project: ProjectDto;
  requests: ProjectHandoverRequestDto[];
}

export function ProjectHandoverSheet({ open, onOpenChange, project, requests }: ProjectHandoverSheetProps) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const locale = useLocale();
  const { user } = useAuth();
  const canManageHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage));
  const { data: projectManagers, isLoading: managersLoading, isError: managersError } = useUserLookup("project.manager", open && canManageHandover);
  const createRequest = useCreateProjectHandoverRequest(project.id);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const pendingRequest = requests.find((r) => r.status === "Pending");

  const projectManagerOptions = (projectManagers ?? [])
    .filter((u) => u.id !== project.projectManagerId)
    .map((u) => ({
      value: u.id,
      label: `${u.fullName} · ${u.email}`,
      keywords: [u.jobTitle ?? ""],
    }));

  const managersEmpty = !managersLoading && !managersError && (projectManagers ?? []).length === 0;

  const emptyState = managersEmpty ? (
    <div className="space-y-1.5 px-2">
      <p className="text-sm font-medium text-foreground">{t("noEligibleManagers")}</p>
      <p className="text-xs text-muted-foreground">{t("noEligibleManagersHint")}</p>
      <Link
        href={`/${locale}/administration/users`}
        className="mt-1 inline-flex items-center gap-1 text-sm font-medium text-primary underline-offset-4 hover:underline"
      >
        {t("manageUsers")}
      </Link>
    </div>
  ) : undefined;

  const form = useForm<HandoverFormData>({
    resolver: zodResolver(handoverSchema),
    defaultValues: { newProjectManagerId: "", effectiveHandoverDate: "", reason: "", notes: "" },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({ newProjectManagerId: "", effectiveHandoverDate: "", reason: "", notes: "" });
    setErrorMessage(null);
  }, [open, form]);

  const onSubmit = async (data: HandoverFormData) => {
    setErrorMessage(null);
    try {
      await createRequest.mutateAsync({
        newProjectManagerId: data.newProjectManagerId,
        effectiveHandoverDate: data.effectiveHandoverDate,
        reason: data.reason,
        notes: data.notes || undefined,
      });
      onOpenChange(false);
      form.reset();
    } catch (err: unknown) {
      const error = err as { data?: { error?: string; message?: string }; message?: string };
      setErrorMessage(error?.data?.error ?? error?.data?.message ?? error?.message ?? t("handoverFailed"));
    }
  };

  if (pendingRequest) {
    return (
      <FormSheet open={open} onOpenChange={onOpenChange} title={t("handoverTitle")} description={t("handoverDescription")}>
        <FormSheetBody className="space-y-5">
          <div className="flex items-start gap-3 rounded-md border border-amber-300/40 bg-amber-50/50 p-4">
            <UserRoundCheck className="h-5 w-5 text-amber-700" />
            <div className="space-y-1">
              <p className="text-sm font-medium text-amber-900">{t("handoverPendingTitle")}</p>
              <p className="text-xs text-amber-800">
                {t("handoverPendingDescription")}
              </p>
              <div className="pt-2 text-sm text-amber-900">
                <p>
                  <span className="text-muted-foreground">{t("newManager")}: </span>
                  <span className="font-medium">{pendingRequest.newProjectManagerName}</span>
                </p>
                <p>
                  <span className="text-muted-foreground">{t("effectiveHandoverDate")}: </span>
                  {formatDate(pendingRequest.effectiveHandoverDate)}
                </p>
                {pendingRequest.reason && (
                  <p>
                    <span className="text-muted-foreground">{t("handoverReason")}: </span>
                    {pendingRequest.reason}
                  </p>
                )}
              </div>
            </div>
          </div>
        </FormSheetBody>
        <FormSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {common("close")}
          </Button>
        </FormSheetFooter>
      </FormSheet>
    );
  }

  if (!canManageHandover) return null;

  return (
    <FormSheet open={open} onOpenChange={onOpenChange} title={t("handoverTitle")} description={t("handoverDescription")}>
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
              <div className="space-y-1.5">
                <FormLabel>{t("project")}</FormLabel>
                <Input value={project.name} readOnly disabled />
              </div>
              <div className="space-y-1.5">
                <FormLabel>{t("currentProjectManager")}</FormLabel>
                <Input value={project.projectManagerName || "-"} readOnly disabled />
              </div>
            </div>

            <FormField
              control={form.control}
              name="newProjectManagerId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("newManager")} <span className="text-destructive">*</span></FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={projectManagerOptions}
                      value={field.value ?? ""}
                      onValueChange={field.onChange}
                      placeholder={t("selectProjectManager")}
                      searchPlaceholder={t("searchManagerPlaceholder")}
                      loading={managersLoading}
                      emptyText={t("noEligibleManagers")}
                      emptyAction={emptyState}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="effectiveHandoverDate"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("effectiveHandoverDate")} <span className="text-destructive">*</span></FormLabel>
                    <FormControl>
                      <Input
                        type="date"
                        min={project.startDate ? project.startDate.slice(0, 10) : undefined}
                        max={project.endDate ? project.endDate.slice(0, 10) : undefined}
                        value={field.value ?? ""}
                        onChange={field.onChange}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="reason"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("handoverReason")} <span className="text-destructive">*</span></FormLabel>
                  <FormControl>
                    <Input placeholder={t("handoverReasonPlaceholder")} value={field.value ?? ""} onChange={field.onChange} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="notes"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("handoverNotes")}</FormLabel>
                  <FormControl>
                    <Textarea placeholder={t("handoverNotesPlaceholder")} rows={3} value={field.value ?? ""} onChange={field.onChange} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </FormSheetBody>

          <FormSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {common("cancel")}
            </Button>
            <Button type="submit" disabled={createRequest.isPending}>
              {createRequest.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {t("submitting")}
                </>
              ) : (
                t("submitHandover")
              )}
            </Button>
          </FormSheetFooter>
        </form>
      </Form>
    </FormSheet>
  );
}
