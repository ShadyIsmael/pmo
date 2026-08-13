"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Form, FormField, FormItem, FormLabel, FormControl, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useCreateProjectChangeRequest } from "@/hooks/projects/useProjectChangeRequests";
import type { ProjectDto } from "@/hooks/projects/useProjects";
import { AlertCircle, Loader2 } from "lucide-react";
import { ProjectChangeRequestTypeHelper, ProjectChangeRequestType } from "@/lib/enums";

// Define the Zod schema using the centralized enum so labels and values stay in sync
const changeRequestSchema = z.object({
  type: z.nativeEnum(ProjectChangeRequestType, { message: "Select a change type" }),
  reason: z.string().trim().min(1, "Reason is required").max(500, "Reason cannot exceed 500 characters"),
  justification: z.string().trim().max(2000, "Justification cannot exceed 2000 characters").optional(),
  impact: z.string().trim().max(2000, "Impact cannot exceed 2000 characters").optional(),
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
  const createRequest = useCreateProjectChangeRequest(project.id);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const form = useForm<ChangeRequestFormData>({
    resolver: zodResolver(changeRequestSchema),
    defaultValues: { type: ProjectChangeRequestType.Administrative, reason: "", justification: "", impact: "", attachments: "" },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({ type: ProjectChangeRequestType.Administrative, reason: "", justification: "", impact: "", attachments: "" });
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
        attachments: data.attachments?.trim() || null,
      });
      onOpenChange(false);
      form.reset();
    } catch (err: any) {
      const apiData = err?.data as { error?: string; message?: string } | undefined;
      setErrorMessage(apiData?.error ?? apiData?.message ?? err?.message ?? t("changeRequestFailed"));
    }
  };

  // Get the options from the centralized helper
  const typeOptions = ProjectChangeRequestTypeHelper.allOptions();

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

            {/* ... rest of the form fields remain the same ... */}
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