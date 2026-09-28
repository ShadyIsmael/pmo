"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Form, FormField, FormItem, FormLabel, FormControl, FormMessage } from "@/components/ui/form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useCreateProjectChangeRequest } from "@/hooks/projects/useProjectChangeRequests";
import type { ProjectDto } from "@/hooks/projects/useProjects";
import { AlertCircle, Loader2 } from "lucide-react";
import { ProjectChangeRequestTypeHelper, ProjectChangeRequestType } from "@/lib/enums";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { Input } from "@/components/ui/input";

// Define the Zod schema using the centralized enum so labels and values stay in sync
const changeRequestSchema = z.object({
  type: z.nativeEnum(ProjectChangeRequestType, { message: "Select a change type" }),
  reason: z.string().trim().min(1, "Reason is required").max(500, "Reason cannot exceed 500 characters"),
  justification: z.string().trim().max(2000, "Justification cannot exceed 2000 characters").optional(),
  impact: z.string().trim().max(2000, "Impact cannot exceed 2000 characters").optional(),
  budgetChangeAmount: z.coerce.number().optional().nullable(),
  attachments: z.string().trim().max(4000, "Attachments cannot exceed 4000 characters").optional(),
});

type ChangeRequestFormData = z.infer<typeof changeRequestSchema>;

interface ProjectChangeRequestSheetProps {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  project: ProjectDto;
}

export function ProjectChangeRequestSheet({ open, onOpenChange, project }: ProjectChangeRequestSheetProps) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const tEnums = useTranslations();
  const { user } = useAuth();
  const canManageChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage));
  const createRequest = useCreateProjectChangeRequest(project.id);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const form = useForm<ChangeRequestFormData>({
    resolver: zodResolver(changeRequestSchema),
    defaultValues: { type: ProjectChangeRequestType.Administrative, reason: "", justification: "", impact: "", budgetChangeAmount: null, attachments: "" },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({ type: ProjectChangeRequestType.Administrative, reason: "", justification: "", impact: "", budgetChangeAmount: null, attachments: "" });
    setErrorMessage(null);
  }, [open, form]);

  const onSubmit = async (data: ChangeRequestFormData) => {
    setErrorMessage(null);
    try {
      await createRequest.mutateAsync({
        projectId: project.id,
        type: data.type,
        reason: data.reason,
        justification: data.justification?.trim() || null,
        impact: data.impact?.trim() || null,
        budgetChangeAmount: data.budgetChangeAmount ?? null,
        attachments: data.attachments?.trim() || null,
      });
      onOpenChange(false);
      form.reset();
    } catch (err: unknown) {
      const error = err as { data?: { error?: string; message?: string }; message?: string };
      setErrorMessage(error?.data?.error ?? error?.data?.message ?? error?.message ?? t("changeRequestFailed"));
    }
  };

  // Get the options from the centralized helper
  const typeOptions = ProjectChangeRequestTypeHelper.allOptions();

  if (!canManageChangeRequests) return null;

  return (
    <FormSheet open={open} onOpenChange={onOpenChange} title={t("changeRequestTitle")} description={t("changeRequestDescription")}>
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-0 flex-1 flex-col">
          <FormSheetBody className="space-y-5">
            {errorMessage && (
              <div className="flex items-center gap-2 p-3 bg-destructive/10 border border-destructive/20 rounded-md text-sm text-destructive">
                <AlertCircle className="h-4 w-4 flex-shrink-0" />
                {errorMessage}
              </div>
            )}

            <FormField
              control={form.control}
              name="type"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("changeType")} <span className="text-destructive">*</span></FormLabel>
                  <FormControl>
                    <Select value={field.value ?? ProjectChangeRequestType.Administrative} onValueChange={field.onChange}>
                      <SelectTrigger>
                        <SelectValue placeholder={t("selectChangeType")} />
                      </SelectTrigger>
                      <SelectContent>
                        {/* Use the centralized options to populate the select */}
                        {typeOptions.map(option => (
                          <SelectItem key={option.value} value={option.value}>
                            {tEnums(option.labelKey)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="reason"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("changeReason")} <span className="text-destructive">*</span></FormLabel>
                  <FormControl>
                    <Input {...field} placeholder={t("changeReasonPlaceholder")} maxLength={500} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="budgetChangeAmount"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("budgetChangeAmount")}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      step="0.01"
                      {...field}
                      value={field.value ?? ""}
                      onChange={(e) => field.onChange(e.target.value === "" ? null : parseFloat(e.target.value))}
                      placeholder={t("budgetChangeAmountPlaceholder")}
                    />
                  </FormControl>
                  <p className="text-[10px] text-muted-foreground">{t("budgetChangeAmountHint")}</p>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="justification"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("justification")}</FormLabel>
                  <FormControl>
                    <textarea {...field} rows={3} maxLength={2000} className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50" placeholder={t("justificationPlaceholder")} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="impact"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("impactDescription")}</FormLabel>
                  <FormControl>
                    <textarea {...field} rows={3} maxLength={2000} className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50" placeholder={t("impactPlaceholder")} />
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
                t("submitChangeRequest")
              )}
            </Button>
          </FormSheetFooter>
        </form>
      </Form>
    </FormSheet>
  );
}
