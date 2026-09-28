"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Form, FormField, FormItem, FormLabel, FormControl, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  useCreateProject,
  useUpdateProject,
  type ProjectDto,
  type CreateProjectDto,
  type UpdateProjectDto,
} from "@/hooks/projects/useProjects";
import {
  useUserLookup,
  usePortfolioLookup,
  useProgramLookup,
  useDepartmentLookup,
  toUserSelectOptions,
} from "@/hooks/projects/useProjectLookups";
import { useCurrencies, type CurrencyDto } from "@/hooks/useCurrencies";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { AlertCircle, Loader2 } from "lucide-react";
import { ApiError } from "@/lib/api";
import { PROJECT_HEALTHS, PROJECT_PRIORITIES, PROJECT_STATUSES } from "@/lib/enums";
import { toDateInput } from "@/lib/utils";

const projectSchema = z
  .object({
    name: z.string().trim().min(1, "Name is required").max(200, "Name cannot exceed 200 characters"),
    code: z.string().trim().max(50, "Code cannot exceed 50 characters").optional().or(z.literal("")),
    description: z.string().trim().optional(),
    programId: z.string().optional(),
    status: z.enum(PROJECT_STATUSES).default("Initiation"),
    priority: z.enum(PROJECT_PRIORITIES).default("Medium"),
    confidenceLevel: z.enum(PROJECT_HEALTHS).default("Green"),
    category: z.string().optional(),
    projectManagerId: z.string().optional(),
    sponsorId: z.string().optional(),
    portfolioId: z.string().optional(),
    departmentId: z.string().optional(),
    startDate: z.string().optional(),
    endDate: z.string().optional(),
    currency: z.string().trim().length(3, "Currency must be a 3-letter code"),
    budget: z.preprocess(
      (value) => (value === "" || value === null || value === undefined ? undefined : value),
      z.coerce.number().min(0, "Budget must be non-negative").optional()
    ),
    businessCase: z.string().trim().optional(),
    notes: z.string().trim().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.startDate && data.endDate && data.endDate < data.startDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["endDate"],
        message: "End date must be on or after the start date",
      });
    }
  });

type ProjectFormData = z.infer<typeof projectSchema>;

interface ProjectSheetProps {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  project?: ProjectDto | null;
}

interface CreateProjectSheetProps {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
}

interface EditProjectSheetProps extends CreateProjectSheetProps {
  project: ProjectDto | null;
}

function generateAutoProjectCode() {
  return `PRJ-${Date.now().toString().slice(-6)}`;
}


function createDefaultValues(defaultCurrency = ""): ProjectFormData {
  return {
    name: "",
    code: generateAutoProjectCode(),
    description: "",
    programId: "",
    status: "Initiation",
    priority: "Medium",
    confidenceLevel: "Green",
    category: "",
    projectManagerId: "",
    sponsorId: "",
    portfolioId: "",
    departmentId: "",
    startDate: "",
    endDate: "",
    currency: defaultCurrency,
    budget: undefined,
    businessCase: "",
    notes: "",
  };
}

function toEditDefaultValues(project: ProjectDto): ProjectFormData {
  return {
    name: project.name,
    code: project.code || "",
    description: project.description ?? "",
    programId: project.programId ?? "",
    status: (project.status as ProjectFormData["status"]) || "Initiation",
    priority: (project.priority as ProjectFormData["priority"]) || "Medium",
    confidenceLevel: (project.confidenceLevel as ProjectFormData["confidenceLevel"]) || "Green",
    projectManagerId: project.projectManagerId ?? "",
    sponsorId: project.sponsorId ?? "",
    portfolioId: project.portfolioId ?? "",
    departmentId: project.departmentId ?? "",
    startDate: toDateInput(project.startDate),
    endDate: toDateInput(project.endDate),
    currency: project.currency || "",
    budget: project.budget ?? undefined,
    businessCase: project.businessCase ?? "",
    notes: project.notes ?? "",
  };
}

export function ProjectSheet({ open, onOpenChange, project }: ProjectSheetProps) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const router = useRouter();
  const isEdit = !!project;
  const createMutation = useCreateProject();
  const updateMutation = useUpdateProject(project?.id ?? "");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const isSubmittingRef = useRef(false);

  const { data: userLookup, isLoading: usersLoading } = useUserLookup();
  const { data: projectManagers, isLoading: managersLoading } = useUserLookup("project.manager");
  const { data: portfolioPage } = usePortfolioLookup();
  const { data: programPage } = useProgramLookup();
  const { data: departmentPage } = useDepartmentLookup();
  const { data: currencies, isLoading: currenciesLoading } = useCurrencies();

  const userOptions = toUserSelectOptions(userLookup ?? []);
  const projectManagerOptions = toUserSelectOptions(projectManagers ?? []);
  const portfolioOptions = (portfolioPage?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const programOptions = (programPage?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const portfolioCurrencyMap = useMemo(() => Object.fromEntries((portfolioPage?.items ?? []).map((p) => [p.id, p.currency ?? ""])), [portfolioPage]);
  const programLookupMap = useMemo(() => Object.fromEntries((programPage?.items ?? []).map((p) => [p.id, p])), [programPage]);
  const departmentOptions = (departmentPage?.items ?? []).map((d) => ({ value: d.id, label: d.name }));
  const baseCurrencyCode = currencies?.find((currency) => currency.isBase)?.code ?? currencies?.[0]?.code ?? "";
  const currentProjectCurrency = project?.currency ?? "";
  const currencyOptions = useMemo<CurrencyDto[]>(() => {
    const options = currencies ?? [];
    if (currentProjectCurrency && !options.some((currency) => currency.code === currentProjectCurrency)) {
      return [
        {
          id: currentProjectCurrency,
          code: currentProjectCurrency,
          name: currentProjectCurrency,
          symbol: "",
          exchangeRate: 1,
          isBase: false,
        },
        ...options,
      ];
    }
    return options;
  }, [currencies, currentProjectCurrency]);

  const form = useForm<ProjectFormData>({
    resolver: zodResolver(projectSchema),
    defaultValues: isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode),
  });
  const selectedProgramId = form.watch("programId");

  useEffect(() => {
    if (!open) return;
    form.reset(isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode));
  }, [open, project, form, isEdit, baseCurrencyCode]);

  const onSubmit = async (data: ProjectFormData) => {
    if (isSubmittingRef.current) return;
    if (!isEdit && !data.projectManagerId) {
      form.setError("projectManagerId", { type: "manual", message: t("projectManagerRequired") });
      return;
    }
    isSubmittingRef.current = true;
    setErrorMessage(null);
    form.clearErrors();
    try {
      const base = {
        description: data.description || undefined,
        programId: data.programId || undefined,
        category: data.category || undefined,
        sponsorId: data.sponsorId || undefined,
        portfolioId: data.portfolioId || undefined,
        departmentId: data.departmentId || undefined,
        startDate: data.startDate || undefined,
        endDate: data.endDate || undefined,
        code: data.code?.trim() || undefined,
        currency: data.currency,
        budget: data.budget === undefined || data.budget === null ? undefined : Number(data.budget),
        businessCase: data.businessCase || undefined,
        notes: data.notes || undefined,
      };

      if (isEdit && project) {
        const payload: UpdateProjectDto = {
          ...base,
          name: data.name,
          status: data.status,
          priority: data.priority,
          projectManagerId: data.projectManagerId || undefined,
          category: data.category || undefined,
          sponsorId: data.sponsorId || undefined,
          departmentId: data.departmentId || undefined,
          confidenceLevel: data.confidenceLevel,
        };
        await updateMutation.mutateAsync(payload);
      } else {
        const payload: CreateProjectDto = {
          ...base,
          name: data.name,
          priority: data.priority,
          projectManagerId: data.projectManagerId || undefined,
        };
        const result = await createMutation.mutateAsync(payload);
        if (result?.id) {
          onOpenChange(false);
          form.reset();
          router.push(`/projects/${result.id}`);
          return;
        }
      }

      onOpenChange(false);
      form.reset();
    } catch (err) {
      const apiData = err instanceof ApiError ? (err.data as { error?: string; message?: string } | undefined) : undefined;
      const apiMessage = apiData?.error ?? apiData?.message ?? (err instanceof Error ? err.message : "Failed to save project");
      const normalized = apiMessage.toLowerCase();
      if (
        normalized.includes("code") &&
        (normalized.includes("exist") || normalized.includes("duplicate") || normalized.includes("already"))
      ) {
        form.setError("code", { type: "server", message: apiMessage });
      } else {
        setErrorMessage(apiMessage);
      }
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const { statusLabel, priorityLabel, healthLabel } = useProjectEnumLabels();

  const statusOptions = PROJECT_STATUSES.map((value) => ({ value, label: statusLabel(value) }));
  const priorityOptions = PROJECT_PRIORITIES.map((value) => ({ value, label: priorityLabel(value) }));
  const confidenceOptions = PROJECT_HEALTHS.map((value) => ({ value, label: healthLabel(value) }));

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  return (
    <FormSheet
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t("edit") : t("create")}
      description={isEdit ? t("editDescription") : t("createDescription")}
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

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("basicInformation")}</h3>
                <p className="text-xs text-muted-foreground">{t("basicInformationDescription")}</p>
              </div>

              <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="code"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("code")}</FormLabel>
                    <FormControl>
                      <Input
                        placeholder={t("codePlaceholder")}
                        value={field.value ?? ""}
                        onChange={field.onChange}
                        disabled={!isEdit}
                        readOnly={!isEdit}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("name")} <span className="text-destructive">*</span></FormLabel>
                    <FormControl>
                      <Input placeholder={t("namePlaceholder")} value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              </div>

              <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("projectDescription")}</FormLabel>
                  <FormControl>
                    <Textarea placeholder={t("descriptionPlaceholder")} rows={3} value={field.value ?? ""} onChange={field.onChange} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
              />
            </section>

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("classification")}</h3>
                <p className="text-xs text-muted-foreground">{t("classificationDescription")}</p>
              </div>

              <div className="grid gap-5 sm:grid-cols-2">
              {isEdit && <FormField
                control={form.control}
                name="status"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("status")}</FormLabel>
                    <Select value={field.value ?? "Initiation"} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder={t("selectStatus")} />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {statusOptions.map((opt) => (
                          <SelectItem key={opt.value} value={opt.value}>
                            {opt.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />}
              <FormField
                control={form.control}
                name="priority"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("priority")}</FormLabel>
                    <Select value={field.value ?? "Medium"} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder={t("selectPriority")} />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {priorityOptions.map((opt) => (
                          <SelectItem key={opt.value} value={opt.value}>
                            {opt.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="category"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("category")}</FormLabel>
                    <FormControl>
                      <Input placeholder={t("categoryPlaceholder")} value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              </div>
            </section>

            {isEdit && <FormField
              control={form.control}
              name="confidenceLevel"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("confidenceLevel")}</FormLabel>
                  <Select value={field.value ?? "Green"} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder={t("selectConfidenceLevel")} />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {confidenceOptions.map((opt) => (
                        <SelectItem key={opt.value} value={opt.value}>
                          {opt.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />}

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("ownershipHierarchy")}</h3>
                <p className="text-xs text-muted-foreground">{t("ownershipHierarchyDescription")}</p>
              </div>

              <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="projectManagerId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("projectManager")} {!isEdit && <span className="text-destructive">*</span>}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={projectManagerOptions}
                        value={field.value ?? ""}
                        onValueChange={field.onChange}
                        placeholder={t("selectProjectManager")}
                        searchPlaceholder={t("searchManagerPlaceholder")}
                        loading={managersLoading}
                        emptyText={t("noEligibleManagers")}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="sponsorId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("sponsor")}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={userOptions}
                        value={field.value ?? ""}
                        onValueChange={field.onChange}
                        placeholder={t("selectSponsor")}
                        searchPlaceholder={t("searchSponsorPlaceholder")}
                        loading={usersLoading}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="portfolioId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("portfolio")}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={portfolioOptions}
                        value={field.value ?? ""}
                        onValueChange={(value) => {
                          field.onChange(value);
                          if (!isEdit && !form.getValues("programId")) form.setValue("currency", portfolioCurrencyMap[value] || baseCurrencyCode, { shouldValidate: true });
                        }}
                        placeholder={t("selectPortfolio")}
                        searchPlaceholder={t("searchPortfolioPlaceholder")}
                        disabled={!!selectedProgramId}
                      />
                    </FormControl>
                    {selectedProgramId && <p className="text-xs text-muted-foreground">{t("portfolioDerivedFromProgram")}</p>}
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="programId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("program")}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={programOptions}
                        value={field.value ?? ""}
                        onValueChange={(value) => {
                          field.onChange(value);
                          const selected = programLookupMap[value];
                          if (selected) {
                            form.setValue("portfolioId", selected.portfolioId, { shouldValidate: true });
                            if (!isEdit) form.setValue("currency", selected.currency || portfolioCurrencyMap[selected.portfolioId] || baseCurrencyCode, { shouldValidate: true });
                          }
                        }}
                        placeholder={t("selectProgram")}
                        searchPlaceholder={t("searchProgramPlaceholder")}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="departmentId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("department")}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={departmentOptions}
                      value={field.value ?? ""}
                      onValueChange={field.onChange}
                      placeholder={t("selectDepartment")}
                      searchPlaceholder={t("searchDepartmentPlaceholder")}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
              />
            </section>

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("scheduleSection")}</h3>
                <p className="text-xs text-muted-foreground">{t("scheduleSectionDescription")}</p>
              </div>

              <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="startDate"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("plannedStartDate")}</FormLabel>
                    <FormControl>
                      <Input type="date" value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="endDate"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("plannedEndDate")}</FormLabel>
                    <FormControl>
                      <Input type="date" value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              </div>
            </section>

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("financialFoundation")}</h3>
                <p className="text-xs text-muted-foreground">{t("financialFoundationDescription")}</p>
              </div>

              <div className="grid gap-5 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="currency"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("currency")}</FormLabel>
                    <Select
                      value={field.value ?? ""}
                      onValueChange={field.onChange}
                      disabled={currenciesLoading || currencyOptions.length === 0}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder={currenciesLoading ? t("loadingCurrencies") : t("selectCurrency")} />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {currencyOptions.map((currency) => (
                          <SelectItem key={currency.id} value={currency.code}>
                            {currency.code} — {currency.name}{currency.symbol ? ` (${currency.symbol})` : ""}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="budget"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("budgetCeiling")}</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        step="0.01"
                        min="0"
                        placeholder="0"
                        value={field.value ?? ""}
                        onChange={(event) => field.onChange(event.target.value === "" ? undefined : event.target.value)}
                        disabled={isEdit}
                        readOnly={isEdit}
                      />
                    </FormControl>
                    <p className="text-xs text-muted-foreground">{t("budgetCeilingHint")}</p>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            </section>

            <section className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-sm font-semibold">{t("initiationSummary")}</h3>
                <p className="text-xs text-muted-foreground">{t("initiationSummaryDescription")}</p>
              </div>

              <FormField
                control={form.control}
                name="businessCase"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("businessCase")}</FormLabel>
                    <FormControl>
                      <Textarea placeholder={t("businessCasePlaceholder")} rows={3} value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {isEdit && <FormField
                control={form.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t("notes")}</FormLabel>
                    <FormControl>
                      <Textarea placeholder={t("notesPlaceholder")} rows={2} value={field.value ?? ""} onChange={field.onChange} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />}
            </section>
          </FormSheetBody>

          <FormSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {common("cancel")}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {isEdit ? t("updating") : common("creating")}
                </>
              ) : (
                isEdit ? t("update") : t("create")
              )}
            </Button>
          </FormSheetFooter>
        </form>
      </Form>
    </FormSheet>
  );
}

export function CreateProjectSheet({ open, onOpenChange }: CreateProjectSheetProps) {
  return <ProjectSheet open={open} onOpenChange={onOpenChange} />;
}

export function EditProjectSheet({ open, onOpenChange, project }: EditProjectSheetProps) {
  return <ProjectSheet open={open} onOpenChange={onOpenChange} project={project} />;
}
