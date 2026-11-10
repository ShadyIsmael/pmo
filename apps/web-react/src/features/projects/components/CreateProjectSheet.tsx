import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { AlertCircle, Loader2 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useI18n } from "@i18n/messages";
import { useCreateProject, useUpdateProject, type ProjectDto, type CreateProjectDto, type UpdateProjectDto } from "../api/projects";
import {
  toUserSelectOptions,
  useDepartmentLookup,
  usePortfolioLookup,
  useProjectCurrencyLookup,
  useProjectWorkflowUserLookup,
  useProjectManagerCandidateLookup,
  useProgramLookup,
} from "../lib/project-lookups";
import { useProjectEnumLabels } from "../lib/project-labels";
import { PROJECT_HEALTHS, PROJECT_PRIORITIES, PROJECT_STATUSES } from "@lib/enums";
import { cn, toDateInput } from "@lib/utils";
import { ApiError, getApiFieldError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";

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
    initialAnnualBudgetAmount: z.preprocess(
      (value) => (value === "" || value === null || value === undefined ? undefined : value),
      z.coerce.number().optional()
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
  onOpenChange: (open: boolean) => void;
  project?: ProjectDto | null;
  onCreated?: (publicId: string) => void;
  embedded?: boolean;
  onHandover?: () => void;
  canInitiateHandover?: boolean;
}

function ProjectSheetBody({ embedded, className, children }: { embedded: boolean; className?: string; children: React.ReactNode }) {
  return embedded
    ? <div className={cn("space-y-5 p-4", className)}>{children}</div>
    : <SideSheetBody className={className}>{children}</SideSheetBody>;
}

function ProjectSheetFooter({ embedded, className, children }: { embedded: boolean; className?: string; children: React.ReactNode }) {
  return embedded
    ? <div className={cn("flex shrink-0 flex-col-reverse gap-2 border-t bg-background px-4 py-3 sm:flex-row sm:justify-end", className)}>{children}</div>
    : <SideSheetFooter className={className}>{children}</SideSheetFooter>;
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
    initialAnnualBudgetAmount: undefined,
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
    category: "",
    projectManagerId: project.projectManagerId ?? "",
    sponsorId: project.sponsorId ?? "",
    portfolioId: project.portfolioId ?? "",
    departmentId: project.departmentId ?? "",
    startDate: toDateInput(project.startDate),
    endDate: toDateInput(project.endDate),
    currency: project.currency || "",
    // The lifetime ceiling is not editable after Project creation. Changes go through an approved CR.
    budget: undefined,
    initialAnnualBudgetAmount: undefined,
    businessCase: project.businessCase ?? "",
    notes: project.notes ?? "",
  };
}

function FieldError({ id, message }: { id?: string; message?: string }) {
  if (!message) return null;
  return <p id={id} role="alert" className="text-xs text-destructive">{message}</p>;
}

export function ProjectSheet({ open, onOpenChange, project, onCreated, embedded = false, onHandover, canInitiateHandover = false }: ProjectSheetProps) {
  const { t, formatCurrency } = useI18n();
  const { user } = useAuth();
  const isEdit = !!project;
  const createMutation = useCreateProject();
  const updateMutation = useUpdateProject(project?.publicId ?? "");
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);
  const isSubmittingRef = React.useRef(false);
  const initialAnnualBudgetWasEdited = React.useRef(false);

  const { data: userLookup, isLoading: usersLoading, error: usersError } = useProjectWorkflowUserLookup(open);
  const { data: projectManagers, isLoading: managersLoading, isFetching: managersFetching, error: managersError, refetch: refetchProjectManagers } = useProjectManagerCandidateLookup(open && !isEdit);
  const { data: portfolioData, error: portfoliosError } = usePortfolioLookup(open);
  const { data: programData, error: programsError } = useProgramLookup(open);
  const { data: departmentUnits, error: departmentsError } = useDepartmentLookup(open);
  const { data: currencies, error: currenciesError } = useProjectCurrencyLookup(open);

  const userOptions = toUserSelectOptions(userLookup ?? []);
  const projectManagerOptions = toUserSelectOptions(projectManagers ?? []);
  const managersForbidden = managersError instanceof ApiError && managersError.status === 403;
  const portfolioOptions = (portfolioData?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const programOptions = (programData?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const portfolioCurrencyMap = React.useMemo<Record<string, string>>(() => Object.fromEntries((portfolioData?.items ?? []).map((p) => [p.id, p.currency ?? ""])), [portfolioData]);
  const programLookupMap = React.useMemo(() => Object.fromEntries((programData?.items ?? []).map((p) => [p.id, p])), [programData]);
  const departmentOptions = (departmentUnits ?? []).map((d) => ({ value: d.id, label: d.name }));
  const baseCurrencyCode = currencies?.find((currency) => currency.isBase)?.code ?? "";
  const currentProjectCurrency = project?.currency ?? "";
  const currencyOptions = React.useMemo(() => {
    const options = currencies ?? [];
    if (currentProjectCurrency && !options.some((currency) => currency.code === currentProjectCurrency)) {
      return [{ id: currentProjectCurrency, code: currentProjectCurrency, name: currentProjectCurrency, symbol: "", isBase: false }, ...options];
    }
    return options;
  }, [currencies, currentProjectCurrency]);

  const form = useForm<ProjectFormData>({
    resolver: zodResolver(projectSchema) as never,
    defaultValues: isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode),
  });
  const selectedProgramId = form.watch("programId");
  const budgetValue = form.watch("budget");
  const canCreateInitialAnnualBudget = !isEdit
    && hasPermission(user?.permissions ?? [], Permissions.ProjectAnnualBudget.Create);

  React.useEffect(() => {
    if (!open) return;
    initialAnnualBudgetWasEdited.current = false;
    form.reset(isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, project]);

  React.useEffect(() => {
    if (!open || !canCreateInitialAnnualBudget || initialAnnualBudgetWasEdited.current) return;
    const suggestedAmount = typeof budgetValue === "number" && Number.isFinite(budgetValue)
      ? budgetValue
      : undefined;
    form.setValue("initialAnnualBudgetAmount", suggestedAmount, { shouldDirty: suggestedAmount !== undefined });
  }, [budgetValue, canCreateInitialAnnualBudget, form, open]);

  React.useEffect(() => {
    if (!open || isEdit || !baseCurrencyCode || form.getValues("currency")) return;
    form.setValue("currency", baseCurrencyCode, { shouldValidate: true });
  }, [baseCurrencyCode, form, isEdit, open]);

  const onSubmit = async (data: ProjectFormData) => {
    if (isSubmittingRef.current) return;
    if (!isEdit && data.initialAnnualBudgetAmount !== undefined) {
      if (!currencies?.some(currency => currency.code.toUpperCase() === data.currency.trim().toUpperCase())) {
        form.setError("currency", { type: "manual", message: t("projects.financialCurrencyUnavailable") });
        return;
      }
      if (data.initialAnnualBudgetAmount < 0) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetMustBeNonNegative") });
        return;
      }
      if (data.budget === undefined) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetRequiresCeiling") });
        return;
      }
      if (data.initialAnnualBudgetAmount > data.budget) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetExceedsCeiling") });
        return;
      }
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
        businessCase: data.businessCase || undefined,
        notes: data.notes || undefined,
      };

      if (isEdit && project) {
        const payload: UpdateProjectDto = {
          ...base,
          clearSponsor: Boolean(project.sponsorId && !data.sponsorId),
          name: data.name,
          status: data.status,
          priority: data.priority,
          confidenceLevel: data.confidenceLevel,
        };
        await updateMutation.mutateAsync(payload);
      } else {
        const payload: CreateProjectDto = {
          ...base,
          name: data.name,
          priority: data.priority,
          projectManagerId: data.projectManagerId || undefined,
          budget: data.budget === undefined || data.budget === null ? undefined : Number(data.budget),
          initialAnnualBudgetAmount: canCreateInitialAnnualBudget && data.initialAnnualBudgetAmount !== undefined ? Number(data.initialAnnualBudgetAmount) : undefined,
        };
        const result = await createMutation.mutateAsync(payload);
        if (result?.publicId) {
          onCreated?.(result.publicId);
          onOpenChange(false);
          form.reset();
          return;
        }
      }

      onOpenChange(false);
      form.reset();
    } catch (err) {
      const apiMessage = err instanceof ApiError ? err.message : t("projects.saveFailed");
      const apiFields = [
        "code",
        "name",
        "projectManagerId",
        "startDate",
        "endDate",
        "currency",
        "budget",
        "initialAnnualBudgetAmount",
      ] as const;
      let hasFieldErrors = false;
      if (err instanceof ApiError) {
        for (const field of apiFields) {
          const fieldMessage = getApiFieldError(err, field);
          if (!fieldMessage) continue;
          form.setError(field, { type: "server", message: fieldMessage });
          hasFieldErrors = true;
        }
      }
      setErrorMessage(hasFieldErrors ? null : apiMessage);
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const { statusLabel, priorityLabel, healthLabel } = useProjectEnumLabels();

  const statusOptions = PROJECT_STATUSES.map((value) => ({ value, label: statusLabel(value) }));
  const priorityOptions = PROJECT_PRIORITIES.map((value) => ({ value, label: priorityLabel(value) }));
  const confidenceOptions = PROJECT_HEALTHS.map((value) => ({ value, label: healthLabel(value) }));

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  const formContent = (
    <form onSubmit={form.handleSubmit(onSubmit as never)} className="flex min-h-0 flex-1 flex-col">
        <ProjectSheetBody embedded={embedded} className="space-y-5">
          {errorMessage && (
            <div role="alert" className="flex items-center gap-2 rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">
              <AlertCircle className="h-4 w-4 shrink-0" />
              {errorMessage}
            </div>
          )}
          {open && (usersError || (!isEdit && managersError) || portfoliosError || programsError || departmentsError || currenciesError) && (
            <div role="alert" className="rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">
              {[usersError, managersError, portfoliosError, programsError, departmentsError, currenciesError]
                .find((error): error is Error => error instanceof Error)?.message ?? t("projects.saveFailed")}
            </div>
          )}

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.basicInformation")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.basicInformationDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.code")}</Label>
                <Input placeholder={t("projects.code")} value={form.watch("code") ?? ""} onChange={(e) => form.setValue("code", e.target.value)} disabled={!isEdit} readOnly={!isEdit} aria-invalid={Boolean(form.formState.errors.code)} aria-describedby={form.formState.errors.code ? "project-code-error" : undefined} />
                <FieldError id="project-code-error" message={form.formState.errors.code?.message} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.name")} <span className="text-destructive">*</span></Label>
                <Input placeholder={t("projects.name")} value={form.watch("name") ?? ""} onChange={(e) => form.setValue("name", e.target.value, { shouldValidate: true })} aria-invalid={Boolean(form.formState.errors.name)} aria-describedby={form.formState.errors.name ? "project-name-error" : undefined} />
                <FieldError id="project-name-error" message={form.formState.errors.name?.message} />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label>{t("projects.projectDescription")}</Label>
              <Textarea placeholder={t("projects.projectDescription")} rows={3} value={form.watch("description") ?? ""} onChange={(e) => form.setValue("description", e.target.value)} />
            </div>
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.classification")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.classificationDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              {isEdit && (
                <div className="space-y-1.5">
                  <Label>{t("common.status")}</Label>
                  <Select value={form.watch("status") ?? "Initiation"} onValueChange={(v) => form.setValue("status", v as ProjectFormData["status"])}>
                    <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                    <SelectContent>{statusOptions.map((opt) => (<SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>))}</SelectContent>
                  </Select>
                </div>
              )}
              <div className="space-y-1.5">
                <Label>{t("projects.priority")}</Label>
                <Select value={form.watch("priority") ?? "Medium"} onValueChange={(v) => form.setValue("priority", v as ProjectFormData["priority"])}>
                  <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                  <SelectContent>{priorityOptions.map((opt) => (<SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>))}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.category")}</Label>
                <Input placeholder={t("projects.category")} value={form.watch("category") ?? ""} onChange={(e) => form.setValue("category", e.target.value)} />
              </div>
            </div>
          </section>

          {isEdit && (
            <div className="space-y-1.5">
              <Label>{t("projects.confidenceLevel")}</Label>
              <Select value={form.watch("confidenceLevel") ?? "Green"} onValueChange={(v) => form.setValue("confidenceLevel", v as ProjectFormData["confidenceLevel"])}>
                <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                <SelectContent>{confidenceOptions.map((opt) => (<SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>))}</SelectContent>
              </Select>
            </div>
          )}

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.ownershipHierarchy")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.ownershipHierarchyDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.projectManager")}</Label>
                {isEdit && project ? (
                  <>
                    <div className="flex items-center gap-2">
                      <Input
                        aria-label={t("projects.currentProjectManager")}
                        value={project.projectManagerName || (project.projectManagerId ? t("common.notAvailable") : t("managerHandover.notAssigned"))}
                        readOnly
                      />
                      {canInitiateHandover && onHandover ? (
                        <Button type="button" variant="outline" className="shrink-0" onClick={onHandover}>
                          {project.projectManagerId ? t("projects.handover") : t("managerHandover.initialAssignment")}
                        </Button>
                      ) : null}
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {project.projectManagerId ? t("managerHandover.managerChangeViaHandover") : t("managerHandover.setInitialManager")}
                    </p>
                  </>
                ) : (
                  <>
                    {managersError ? (
                      <div role="alert" className="space-y-2 rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">
                        <p>{managersForbidden ? t("projects.managerCandidatesForbidden") : t("common.errorLoading")}</p>
                        {!managersForbidden ? (
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <span className="text-xs">{t("common.notAvailable")}</span>
                            <Button type="button" variant="outline" size="sm" onClick={() => void refetchProjectManagers()} disabled={managersFetching}>
                              {managersFetching ? t("common.loading") : t("common.retry")}
                            </Button>
                          </div>
                        ) : null}
                      </div>
                    ) : (
                      <>
                        <SearchableSelect options={projectManagerOptions} value={form.watch("projectManagerId") ?? ""} onValueChange={(v) => form.setValue("projectManagerId", v)} placeholder={t("projects.projectManager")} searchPlaceholder={t("projects.projectManager")} loading={managersLoading} emptyText={t("projects.noEligibleProjectManagers")} />
                        {!managersLoading && projectManagerOptions.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.initialManagerResourceIndependence")}</p> : null}
                        <p className="text-xs text-muted-foreground">{t("managerHandover.setInitialManager")}</p>
                      </>
                    )}
                  </>
                )}
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.sponsor")}</Label>
                <div className="flex items-center gap-2">
                  <div className="min-w-0 flex-1"><SearchableSelect options={userOptions} value={form.watch("sponsorId") ?? ""} onValueChange={(v) => form.setValue("sponsorId", v)} placeholder={t("projects.sponsor")} searchPlaceholder={t("projects.sponsor")} loading={usersLoading} emptyText={t("common.noResults")} /></div>
                  {isEdit && form.watch("sponsorId") ? <Button type="button" variant="outline" size="sm" onClick={() => form.setValue("sponsorId", "", { shouldDirty: true })}>{t("projects.clearSponsor")}</Button> : null}
                </div>
                <p className="text-xs text-muted-foreground">{t("projects.sponsorResourceRequirement")}</p>
              </div>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.portfolio")}</Label>
                <SearchableSelect
                  options={portfolioOptions}
                  value={form.watch("portfolioId") ?? ""}
                  onValueChange={(value) => {
                    form.setValue("portfolioId", value);
                    if (!isEdit && !form.getValues("programId")) form.setValue("currency", portfolioCurrencyMap[value] || baseCurrencyCode, { shouldValidate: true });
                  }}
                  placeholder={t("projects.portfolio")}
                  searchPlaceholder={t("projects.portfolio")}
                  disabled={!!selectedProgramId}
                />
                {selectedProgramId ? <p className="text-xs text-muted-foreground">{t("projects.portfolioDerivedFromProgram")}</p> : null}
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.program")}</Label>
                <SearchableSelect
                  options={programOptions}
                  value={form.watch("programId") ?? ""}
                  onValueChange={(value) => {
                    form.setValue("programId", value);
                    const selected = (programLookupMap as Record<string, { portfolioId: string; currency?: string }>)[value];
                    if (selected) {
                      form.setValue("portfolioId", selected.portfolioId, { shouldValidate: true });
                      if (!isEdit) form.setValue("currency", selected.currency || portfolioCurrencyMap[selected.portfolioId] || baseCurrencyCode, { shouldValidate: true });
                    }
                  }}
                  placeholder={t("projects.program")}
                  searchPlaceholder={t("projects.program")}
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label>{t("projects.department")}</Label>
              <SearchableSelect options={departmentOptions} value={form.watch("departmentId") ?? ""} onValueChange={(v) => form.setValue("departmentId", v)} placeholder={t("projects.department")} searchPlaceholder={t("projects.department")} emptyText={t("common.noResults")} />
            </div>
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.scheduleSection")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.scheduleSectionDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.plannedStartDate")}</Label>
                <Input type="date" value={form.watch("startDate") ?? ""} onChange={(e) => form.setValue("startDate", e.target.value)} aria-invalid={Boolean(form.formState.errors.startDate)} aria-describedby={form.formState.errors.startDate ? "project-start-date-error" : undefined} />
                <FieldError id="project-start-date-error" message={form.formState.errors.startDate?.message} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.plannedEndDate")}</Label>
                <Input type="date" value={form.watch("endDate") ?? ""} onChange={(e) => form.setValue("endDate", e.target.value)} aria-invalid={Boolean(form.formState.errors.endDate)} aria-describedby={form.formState.errors.endDate ? "project-end-date-error" : undefined} />
                <FieldError id="project-end-date-error" message={form.formState.errors.endDate?.message} />
              </div>
            </div>
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.financialFoundation")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.financialFoundationDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.currency")}</Label>
                <Select value={form.watch("currency") ?? ""} onValueChange={(v) => form.setValue("currency", v)}>
                  <SelectTrigger aria-invalid={Boolean(form.formState.errors.currency)} aria-describedby={form.formState.errors.currency ? "project-currency-error" : undefined}><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((currency) => (
                      <SelectItem key={currency.id} value={currency.code}>{currency.code} — {currency.name}{currency.symbol ? ` (${currency.symbol})` : ""}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError id="project-currency-error" message={form.formState.errors.currency?.message} />
              </div>
              {isEdit ? (
                <div className="space-y-1.5">
                  <Label>{t("projects.projectLifetimeCeiling")}</Label>
                  <p className="rounded-md border bg-muted/30 px-3 py-2 text-sm tabular-nums" aria-readonly="true">
                    {project?.budget == null ? "—" : formatCurrency(project.budget, project.currency)}
                  </p>
                  <p className="text-xs text-muted-foreground">{t("projects.budgetCeilingHint")}</p>
                </div>
              ) : (
                <div className="space-y-1.5">
                  <Label>{t("projects.projectLifetimeCeiling")}</Label>
                  <Input type="number" step="0.01" min="0" placeholder="0" value={form.watch("budget") ?? ""} onChange={(event) => form.setValue("budget", event.target.value === "" ? undefined : Number(event.target.value) as unknown as number)} aria-invalid={Boolean(form.formState.errors.budget)} aria-describedby={form.formState.errors.budget ? "project-budget-error" : undefined} />
                  <FieldError id="project-budget-error" message={form.formState.errors.budget?.message} />
                  <p className="text-xs text-muted-foreground">{t("projects.budgetCeilingHint")}</p>
                </div>
              )}
            </div>
            {canCreateInitialAnnualBudget ? <div className="rounded-md border p-4">
              <div className="space-y-1">
                <h4 className="text-sm font-medium">{t("projects.initialAnnualBudgetTitle")}</h4>
                <p className="text-xs text-muted-foreground">{t("projects.initialAnnualBudgetDescription")}</p>
                <p className="text-xs text-muted-foreground">{t("projects.initialAnnualBudgetSuggestedFromCeiling")}</p>
              </div>
              <div className="mt-3 max-w-sm space-y-1.5">
                <Label htmlFor="project-initial-annual-budget">{t("projects.annualBudgetFunding")}</Label>
                <Input id="project-initial-annual-budget" type="number" min="0" step="0.0001" value={form.watch("initialAnnualBudgetAmount") ?? ""} onChange={event => { initialAnnualBudgetWasEdited.current = true; form.setValue("initialAnnualBudgetAmount", event.target.value === "" ? undefined : Number(event.target.value), { shouldValidate: true }); }} aria-invalid={Boolean(form.formState.errors.initialAnnualBudgetAmount)} aria-describedby={form.formState.errors.initialAnnualBudgetAmount ? "project-initial-budget-error" : undefined} />
                <FieldError id="project-initial-budget-error" message={form.formState.errors.initialAnnualBudgetAmount?.message} />
                <p className="text-xs text-muted-foreground">{t("projects.initialAnnualBudgetHint", { currency: form.watch("currency") || "" })}</p>
              </div>
            </div> : null}
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.initiationSummary")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.initiationSummaryDescription")}</p>
            </div>

            <div className="space-y-1.5">
              <Label>{t("projects.businessCase")}</Label>
              <Textarea placeholder={t("projects.businessCase")} rows={3} value={form.watch("businessCase") ?? ""} onChange={(e) => form.setValue("businessCase", e.target.value)} />
            </div>

            {isEdit && (
              <div className="space-y-1.5">
                <Label>{t("projects.notes")}</Label>
                <Textarea placeholder={t("projects.notes")} rows={2} value={form.watch("notes") ?? ""} onChange={(e) => form.setValue("notes", e.target.value)} />
              </div>
            )}
          </section>
        </ProjectSheetBody>

        <ProjectSheetFooter embedded={embedded}>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? (<><Loader2 className="mr-2 h-4 w-4 animate-spin" />{isEdit ? t("projects.updating") : t("common.creating")}</>) : isEdit ? t("projects.update") : t("projects.create")}
          </Button>
        </ProjectSheetFooter>
      </form>
  );

  if (embedded)
    return <div className="overflow-hidden rounded-lg border border-border bg-card">{formContent}</div>;

  return (
    <SideSheet
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t("projects.edit") : t("projects.create")}
      description={isEdit ? t("projects.editDescription") : t("projects.createDescription")}
      size="lg"
    >
      {formContent}
    </SideSheet>
  );
}

export interface LegacyProjectSheetValues {
  name: string;
  description: string;
}

export function CreateProjectSheet({ open, onOpenChange, mode, initialValues, onSubmit, onCreated }: { open: boolean; onOpenChange: (open: boolean) => void; mode?: "create" | "edit"; initialValues?: Partial<LegacyProjectSheetValues>; onSubmit?: (values: LegacyProjectSheetValues) => void | Promise<void>; onCreated?: (publicId: string) => void }) {
  // Legacy callers (e.g. ProjectHeader edit) use mode/initialValues/onSubmit.
  // Render a lightweight legacy modal for those, otherwise the full golden sheet.
  if (mode || initialValues || onSubmit) {
    return <LegacyCreateProjectSheet open={open} onOpenChange={onOpenChange} mode={mode} initialValues={initialValues} onSubmit={onSubmit} />;
  }
  return <ProjectSheet open={open} onOpenChange={onOpenChange} onCreated={onCreated} />;
}

function LegacyCreateProjectSheet({ open, onOpenChange, initialValues, onSubmit }: { open: boolean; onOpenChange: (open: boolean) => void; mode?: "create" | "edit"; initialValues?: Partial<LegacyProjectSheetValues>; onSubmit?: (values: LegacyProjectSheetValues) => void | Promise<void> }) {
  const { t } = useI18n();
  const [formData, setFormData] = React.useState({ name: "", description: "", ...initialValues });
  const [saving, setSaving] = React.useState(false);
  React.useEffect(() => {
    if (open) {
      setFormData({ name: "", description: "", ...initialValues });
      setSaving(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);
  if (!open) return null;
  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("projects.edit")} description={t("projects.editDescription")}>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          if (!onSubmit) {
            onOpenChange(false);
            return;
          }
          try {
            setSaving(true);
            await onSubmit({ name: formData.name ?? "", description: formData.description ?? "" });
          } finally {
            setSaving(false);
          }
        }}
        className="flex min-h-0 flex-1 flex-col"
      >
        <SideSheetBody className="space-y-4">
          <div className="space-y-1.5"><Label>{t("projects.name")} <span className="text-destructive">*</span></Label><Input value={formData.name ?? ""} onChange={(e) => setFormData({ ...formData, name: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("projects.projectDescription")}</Label><Textarea rows={4} value={formData.description ?? ""} onChange={(e) => setFormData({ ...formData, description: e.target.value })} /></div>
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={saving}>{saving ? t("common.saving") : t("common.save")}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

export function EditProjectSheet({ open, onOpenChange, project, onHandover, canInitiateHandover }: { open: boolean; onOpenChange: (open: boolean) => void; project: ProjectDto | null; onHandover?: () => void; canInitiateHandover?: boolean }) {
  return <ProjectSheet open={open} onOpenChange={onOpenChange} project={project} onHandover={onHandover} canInitiateHandover={canInitiateHandover} />;
}
