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
import { PROJECT_HEALTHS, PROJECT_PRIORITIES } from "@lib/enums";
import { cn, toDateInput } from "@lib/utils";
import { ApiError, getApiFieldError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { ProjectPhaseGateSideSheet } from "./ProjectPhaseGateSideSheet";
import { projectLifecycleLabel, useChangeProjectStatus, useProjectLifecycleOptions, type ProjectLifecycleOption } from "../api/project-lifecycle";

const projectSchema = z
  .object({
    name: z.string().trim().min(1, "Name is required").max(200, "Name cannot exceed 200 characters"),
    code: z.string().trim().max(50, "Code cannot exceed 50 characters").optional().or(z.literal("")),
    description: z.string().trim().optional(),
    programId: z.string().optional(),
    phaseId: z.string().optional(),
    statusId: z.string().optional(),
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
    phaseId: "",
    statusId: "",
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
    phaseId: project.phaseId ?? "",
    statusId: project.statusId ?? "",
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

// The protected initial phase is identified by its backend code, never by a
// hardcoded label. Codes are normalized so PRE_INITIATION and PRE-INITIATION match.
function isPreInitiationPhase(option: ProjectLifecycleOption) {
  return option.code.trim().toUpperCase().replace(/_/g, "-") === "PRE-INITIATION";
}

function FieldError({ id, message }: { id?: string; message?: string }) {
  if (!message) return null;
  return <p id={id} role="alert" className="text-xs text-destructive">{message}</p>;
}

export function ProjectSheet({ open, onOpenChange, project, onCreated, embedded = false, onHandover, canInitiateHandover = false }: ProjectSheetProps) {
  const { t, locale, formatCurrency } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const isEdit = !!project;
  const canChangeProjectPhase = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectPhase.Change)
    && canMutate && !effectiveReadOnly);
  const canChangeProjectStatus = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectStatus.Change)
    && canMutate && !effectiveReadOnly);
  const canAssignInitialManager = Boolean(user
    && hasPermission(user.permissions, Permissions.Handover.AdminInitiate)
    && canMutate && !effectiveReadOnly);
  const createMutation = useCreateProject();
  const updateMutation = useUpdateProject(project?.publicId ?? "");
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);
  const [showValidationSummary, setShowValidationSummary] = React.useState(false);
  const [phaseGateOpen, setPhaseGateOpen] = React.useState(false);
  const [statusTargetId, setStatusTargetId] = React.useState("");
  const [statusChangeReason, setStatusChangeReason] = React.useState("");
  const [statusChangeError, setStatusChangeError] = React.useState<string | null>(null);
  const [statusChangeSaved, setStatusChangeSaved] = React.useState(false);
  const formRef = React.useRef<HTMLFormElement>(null);
  const isSubmittingRef = React.useRef(false);
  const initialAnnualBudgetWasEdited = React.useRef(false);

  const { data: userLookup, isLoading: usersLoading, error: usersError } = useProjectWorkflowUserLookup(open);
  const { data: projectManagers, isLoading: managersLoading, isFetching: managersFetching, error: managersError, refetch: refetchProjectManagers } = useProjectManagerCandidateLookup(open && !isEdit && canAssignInitialManager);
  const { data: portfolioData, error: portfoliosError } = usePortfolioLookup(open);
  const [lookupPortfolioId, setLookupPortfolioId] = React.useState("");
  const { data: programData, error: programsError } = useProgramLookup(open, lookupPortfolioId);
  const { data: departmentUnits, error: departmentsError } = useDepartmentLookup(open);
  const { data: currencies, error: currenciesError } = useProjectCurrencyLookup(open);
  const canLoadLifecycleOptions = Boolean(user && hasPermission(user.permissions, isEdit ? Permissions.Project.Edit : Permissions.Project.Create));
  const changeProjectStatus = useChangeProjectStatus(project?.publicId ?? "");

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
  const budgetValue = form.watch("budget");
  const watchedPortfolioId = form.watch("portfolioId") ?? "";
  const watchedProgramId = form.watch("programId") ?? "";
  const createPhaseScopeKey = `${watchedPortfolioId}::${watchedProgramId}`;
  const canCreateInitialAnnualBudget = !isEdit
    && hasPermission(user?.permissions ?? [], Permissions.ProjectAnnualBudget.Create);

  // Create mode loads the Tenant-scoped Status catalog immediately and the protected
  // Pre-Initiation phase for the selected Portfolio's Organization when one is chosen.
  // No Organization or project identifier is sent.
  const lifecycleOptions = useProjectLifecycleOptions({
    enabled: open && canLoadLifecycleOptions,
    projectId: isEdit ? project?.publicId : undefined,
    ...(isEdit
      ? {}
      : {
        purpose: "create" as const,
        portfolioId: watchedPortfolioId || undefined,
        programId: watchedProgramId || undefined,
      }),
  });

  // Only the protected Pre-Initiation record is an acceptable initial phase, and only
  // after a Portfolio resolves the Organization that owns the phase catalog.
  const initialPhaseOption = React.useMemo(() => {
    if (isEdit || !watchedPortfolioId) return undefined;
    const activePhases = (lifecycleOptions.data?.phases ?? []).filter(option => option.isActive !== false);
    const protectedPhase = activePhases.find(isPreInitiationPhase);
    if (protectedPhase) return protectedPhase;
    return undefined;
  }, [isEdit, lifecycleOptions.data, watchedPortfolioId]);

  React.useEffect(() => {
    if (!open) return;
    initialAnnualBudgetWasEdited.current = false;
    setShowValidationSummary(false);
    setErrorMessage(null);
    setLookupPortfolioId(isEdit && project ? project.portfolioId ?? "" : "");
    form.reset(isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, project]);

  // Project Status is Tenant-scoped reference data shared by every Organization, so the
  // status catalog does not depend on the selected Portfolio. Keep the form value aligned
  // with the loaded catalog and apply the configured Tenant default when it is needed.
  const activeStatusOptions = React.useMemo(
    () => (lifecycleOptions.data?.statuses ?? []).filter(option => option.isActive !== false),
    [lifecycleOptions.data]);
  const watchedStatusId = form.watch("statusId") ?? "";
  const resolvedCreateStatusId = React.useMemo(() => {
    if (isEdit) return "";
    if (activeStatusOptions.some(option => option.id === watchedStatusId)) return watchedStatusId;
    return activeStatusOptions.find(option => option.isDefault)?.id ?? "";
  }, [activeStatusOptions, isEdit, watchedStatusId]);

  React.useEffect(() => {
    if (!open || isEdit || !lifecycleOptions.data) return;
    const current = form.getValues("statusId") ?? "";
    if (activeStatusOptions.some(option => option.id === current)) return;
    const tenantDefault = activeStatusOptions.find(option => option.isDefault)?.id ?? "";
    if (tenantDefault !== current) form.setValue("statusId", tenantDefault, { shouldValidate: false });
  }, [activeStatusOptions, form, isEdit, lifecycleOptions.data, open]);

  // Keep the real form phaseId aligned with the protected record for the selected scope.
  React.useEffect(() => {
    if (isEdit || !open) return;
    const resolvedPhaseId = initialPhaseOption?.id ?? "";
    if (form.getValues("phaseId") !== resolvedPhaseId) {
      form.setValue("phaseId", resolvedPhaseId, { shouldValidate: false });
    }
  }, [createPhaseScopeKey, form, initialPhaseOption, isEdit, open]);

  React.useEffect(() => {
    if (!open) return;
    setStatusTargetId("");
    setStatusChangeReason("");
    setStatusChangeError(null);
    setStatusChangeSaved(false);
  }, [open, project?.statusId]);

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

  const resolvedCreatePhaseId = isEdit ? "" : initialPhaseOption?.id ?? "";

  const onSubmit = async (data: ProjectFormData) => {
    if (isSubmittingRef.current) return;
    if (!isEdit && !data.portfolioId) {
      form.setError("portfolioId", { type: "manual", message: t("projects.validation.portfolioRequired") });
      setShowValidationSummary(true);
      window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
      return;
    }
    // Never submit the form's possibly-stale id: the payload phase comes from the
    // record resolved for the current Portfolio scope.
    if (!isEdit && !resolvedCreatePhaseId) {
      form.setError("phaseId", { type: "manual", message: t("projects.lifecycle.phaseRequired") });
      setShowValidationSummary(true);
      window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
      return;
    }
    if (!isEdit && !resolvedCreateStatusId) {
      form.setError("statusId", { type: "manual", message: t("projects.lifecycle.statusRequired") });
      setShowValidationSummary(true);
      window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
      return;
    }
    if (!isEdit && data.initialAnnualBudgetAmount !== undefined) {
      if (!currencies?.some(currency => currency.code.toUpperCase() === data.currency.trim().toUpperCase())) {
        form.setError("currency", { type: "manual", message: t("projects.financialCurrencyUnavailable") });
        setShowValidationSummary(true);
        window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
        return;
      }
      if (data.initialAnnualBudgetAmount < 0) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetMustBeNonNegative") });
        setShowValidationSummary(true);
        window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
        return;
      }
      if (data.budget === undefined) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetRequiresCeiling") });
        setShowValidationSummary(true);
        window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
        return;
      }
      if (data.initialAnnualBudgetAmount > data.budget) {
        form.setError("initialAnnualBudgetAmount", { type: "manual", message: t("projects.initialAnnualBudgetExceedsCeiling") });
        setShowValidationSummary(true);
        window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
        return;
      }
    }
    isSubmittingRef.current = true;
    setShowValidationSummary(false);
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
          priority: data.priority,
          confidenceLevel: data.confidenceLevel,
        };
        await updateMutation.mutateAsync(payload);
      } else {
        const payload: CreateProjectDto = {
          ...base,
          name: data.name,
          phaseId: resolvedCreatePhaseId,
          statusId: resolvedCreateStatusId,
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
        "portfolioId",
        "programId",
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
      if (hasFieldErrors) {
        setShowValidationSummary(true);
        window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
      }
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const currentPhaseOption = lifecycleOptions.data?.phases.find(option => option.id === project?.phaseId);
  const currentStatusOption = lifecycleOptions.data?.statuses.find(option => option.id === project?.statusId);
  const currentPhaseLabel = projectLifecycleLabel(currentPhaseOption, locale, project?.phase);
  const currentStatusLabel = projectLifecycleLabel(currentStatusOption, locale, project?.status);
  const availableStatuses = (lifecycleOptions.data?.statuses ?? [])
    .filter(option => option.isActive !== false
      && option.id !== project?.statusId
      && option.code.toLocaleUpperCase() !== project?.status?.toLocaleUpperCase());
  const submitStatusChange = async () => {
    const selectedStatus = availableStatuses.find(option => option.id === statusTargetId);
    if (!project || !selectedStatus || changeProjectStatus.isPending) return;
    setStatusChangeError(null);
    setStatusChangeSaved(false);
    try {
      await changeProjectStatus.mutateAsync({
        targetStatusId: selectedStatus.id,
        ...(statusChangeReason.trim() ? { reason: statusChangeReason.trim() } : {}),
      });
      setStatusTargetId("");
      setStatusChangeReason("");
      setStatusChangeSaved(true);
    } catch {
      setStatusChangeError(t("projects.lifecycle.statusChangeFailed"));
    }
  };

  const { priorityLabel, healthLabel } = useProjectEnumLabels();

  const priorityOptions = PROJECT_PRIORITIES.map((value) => ({ value, label: priorityLabel(value) }));
  const confidenceOptions = PROJECT_HEALTHS.map((value) => ({ value, label: healthLabel(value) }));

  const isSubmitting = createMutation.isPending || updateMutation.isPending;
  const initialPhaseUnresolved = !isEdit
    && Boolean(watchedPortfolioId)
    && (lifecycleOptions.isLoading || lifecycleOptions.isError || !initialPhaseOption);
  // A Create submission must never reach the statusRequired validation while the
  // status Select is disabled: loading, lookup error, or an empty Tenant catalog
  // all leave the user unable to fix the field, so submission stays blocked and
  // the truthful loading/error/empty state explains why.
  const createStatusUnresolved = !isEdit
    && (lifecycleOptions.isLoading || lifecycleOptions.isError || activeStatusOptions.length === 0);

  const focusFirstInvalid = () => window.setTimeout(() => formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), 0);
  const validationMessage = (message?: string) => {
    if (!message) return undefined;
    const keys: Record<string, string> = {
      "Name is required": "projects.validation.nameRequired", "Name cannot exceed 200 characters": "projects.validation.nameTooLong",
      "Code cannot exceed 50 characters": "projects.validation.codeTooLong", "Currency must be a 3-letter code": "projects.validation.currencyInvalid",
      "Budget must be non-negative": "projects.validation.budgetNonNegative", "End date must be on or after the start date": "projects.validation.endDateOrder",
    };
    return keys[message] ? t(keys[message]) : message;
  };
  const formContent = (
    <form noValidate ref={formRef} onSubmit={form.handleSubmit(onSubmit as never, () => { setShowValidationSummary(true); focusFirstInvalid(); })} className="flex min-h-0 flex-1 flex-col">
        <ProjectSheetBody embedded={embedded} className="space-y-5">
          {showValidationSummary && <div role="alert" className="rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">{t("projects.createValidationSummary")}</div>}
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
              <h3 className="text-base font-semibold">{t("projects.createSections.basic")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.createRequiredHint")}</p>
              <p className="text-xs text-muted-foreground">{t("projects.basicInformationDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="project-code">{t("projects.code")}</Label>
                <Input id="project-code" dir="ltr" placeholder={t("projects.code")} value={form.watch("code") ?? ""} onChange={(e) => form.setValue("code", e.target.value)} disabled={!isEdit} readOnly={!isEdit} aria-invalid={Boolean(form.formState.errors.code)} aria-describedby={form.formState.errors.code ? "project-code-error" : undefined} />
                <FieldError id="project-code-error" message={validationMessage(form.formState.errors.code?.message)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="project-name">{t("projects.name")} <span className="text-destructive">*</span></Label>
                <Input id="project-name" aria-required="true" placeholder={t("projects.name")} value={form.watch("name") ?? ""} onChange={(e) => form.setValue("name", e.target.value, { shouldValidate: true })} aria-invalid={Boolean(form.formState.errors.name)} aria-describedby={form.formState.errors.name ? "project-name-error" : undefined} />
                <FieldError id="project-name-error" message={validationMessage(form.formState.errors.name?.message)} />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="project-description">{t("projects.projectDescription")}</Label>
              <Textarea id="project-description" placeholder={t("projects.projectDescription")} rows={3} value={form.watch("description") ?? ""} onChange={(e) => form.setValue("description", e.target.value)} />
            </div>
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.classification")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.classificationDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.priority")}</Label>
                <Select value={form.watch("priority") ?? "Medium"} onValueChange={(v) => form.setValue("priority", v as ProjectFormData["priority"])}>
                  <SelectTrigger aria-label={t("projects.priority")}><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                  <SelectContent>{priorityOptions.map((opt) => (<SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>))}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="project-category">{t("projects.category")}</Label>
                <Input id="project-category" placeholder={t("projects.category")} value={form.watch("category") ?? ""} onChange={(e) => form.setValue("category", e.target.value)} />
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
            <div className="space-y-1"><h3 className="text-base font-semibold">{t("projects.createSections.hierarchy")}</h3><p className="text-xs text-muted-foreground">{t("projects.ownershipHierarchyDescription")}</p></div>
            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.portfolio")} {!isEdit ? <span className="text-destructive">*</span> : null}</Label>
                <SearchableSelect aria-label={t("projects.portfolio")} aria-invalid={Boolean(form.formState.errors.portfolioId)} aria-describedby={form.formState.errors.portfolioId ? "project-portfolio-error" : undefined} emptyText={t("common.noResults")} loadingText={t("common.loading")}
                  options={portfolioOptions}
                  value={form.watch("portfolioId") ?? ""}
                  onValueChange={(value) => {
                    const currentProgram = programLookupMap[form.getValues("programId") ?? ""];
                    form.setValue("portfolioId", value, { shouldDirty: true });
                    form.clearErrors("portfolioId");
                    setLookupPortfolioId(value);
                    if (form.getValues("programId") && (!currentProgram || currentProgram.portfolioId !== value)) form.setValue("programId", "", { shouldDirty: true });
                    if (!isEdit && !form.getValues("programId")) form.setValue("currency", portfolioCurrencyMap[value] || baseCurrencyCode, { shouldValidate: true });
                  }}
                  placeholder={t("projects.portfolio")}
                  searchPlaceholder={t("projects.portfolio")}
                />
                <FieldError id="project-portfolio-error" message={form.formState.errors.portfolioId?.message} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.program")}</Label>
                <SearchableSelect aria-label={t("projects.program")} emptyText={t("common.noResults")} loadingText={t("common.loading")}
                  options={programOptions}
                  value={form.watch("programId") ?? ""}
                  onValueChange={(value) => {
                    form.setValue("programId", value);
                    const selected = (programLookupMap as Record<string, { portfolioId: string; currency?: string }>)[value];
                    if (selected) {
                      form.setValue("portfolioId", selected.portfolioId, { shouldValidate: true });
                      setLookupPortfolioId(selected.portfolioId);
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
              <SearchableSelect aria-label={t("projects.department")} loadingText={t("common.loading")} options={departmentOptions} value={form.watch("departmentId") ?? ""} onValueChange={(v) => form.setValue("departmentId", v)} placeholder={t("projects.department")} searchPlaceholder={t("projects.department")} emptyText={t("common.noResults")} />
            </div>
          </section>

          <section className="space-y-4"><h3 className="text-base font-semibold">{t("projects.createSections.governance")}</h3>
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
                    {!canAssignInitialManager ? (
                      <p className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">{t("managerHandover.adminInitiateRequired")}</p>
                    ) : managersError ? (
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
                        <SearchableSelect aria-label={t("projects.projectManager")} aria-invalid={Boolean(form.formState.errors.projectManagerId)} aria-describedby={form.formState.errors.projectManagerId ? "project-manager-error" : undefined} loadingText={t("common.loading")} options={projectManagerOptions} value={form.watch("projectManagerId") ?? ""} onValueChange={(v) => form.setValue("projectManagerId", v)} placeholder={t("projects.projectManager")} searchPlaceholder={t("projects.projectManager")} loading={managersLoading} emptyText={t("projects.noEligibleProjectManagers")} />
                        <FieldError id="project-manager-error" message={validationMessage(form.formState.errors.projectManagerId?.message)} />
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
                  <div className="min-w-0 flex-1"><SearchableSelect aria-label={t("projects.sponsor")} loadingText={t("common.loading")} options={userOptions} value={form.watch("sponsorId") ?? ""} onValueChange={(v) => form.setValue("sponsorId", v)} placeholder={t("projects.sponsor")} searchPlaceholder={t("projects.sponsor")} loading={usersLoading} emptyText={t("common.noResults")} /></div>
                  {isEdit && form.watch("sponsorId") ? <Button type="button" variant="outline" size="sm" onClick={() => form.setValue("sponsorId", "", { shouldDirty: true })}>{t("projects.clearSponsor")}</Button> : null}
                </div>
                <p className="text-xs text-muted-foreground">{t("projects.sponsorResourceRequirement")}</p>
              </div>
            </div>

          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">{t("projects.createSections.scheduleFinance")}</h3>
              <p className="text-xs text-muted-foreground">{t("projects.scheduleSectionDescription")}</p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="project-start-date">{t("projects.plannedStartDate")}</Label>
                <Input id="project-start-date" dir="ltr" type="date" value={form.watch("startDate") ?? ""} onChange={(e) => form.setValue("startDate", e.target.value)} aria-invalid={Boolean(form.formState.errors.startDate)} aria-describedby={form.formState.errors.startDate ? "project-start-date-error" : undefined} />
                <FieldError id="project-start-date-error" message={validationMessage(form.formState.errors.startDate?.message)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="project-end-date">{t("projects.plannedEndDate")}</Label>
                <Input id="project-end-date" dir="ltr" type="date" value={form.watch("endDate") ?? ""} onChange={(e) => form.setValue("endDate", e.target.value)} aria-invalid={Boolean(form.formState.errors.endDate)} aria-describedby={form.formState.errors.endDate ? "project-end-date-error" : undefined} />
                <FieldError id="project-end-date-error" message={validationMessage(form.formState.errors.endDate?.message)} />
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
                <Label>{t("projects.currency")} <span className="text-destructive">*</span></Label>
                <Select value={form.watch("currency") ?? ""} onValueChange={(v) => form.setValue("currency", v)}>
                  <SelectTrigger aria-label={t("projects.currency")} aria-required="true" aria-invalid={Boolean(form.formState.errors.currency)} aria-describedby={form.formState.errors.currency ? "project-currency-error" : undefined}><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((currency) => (
                      <SelectItem key={currency.id} value={currency.code}>{currency.code} — {currency.name}{currency.symbol ? ` (${currency.symbol})` : ""}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError id="project-currency-error" message={validationMessage(form.formState.errors.currency?.message)} />
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
                  <Label htmlFor="project-budget">{t("projects.projectLifetimeCeiling")}</Label>
                  <Input id="project-budget" dir="ltr" type="number" step="0.01" min="0" placeholder="0" value={form.watch("budget") ?? ""} onChange={(event) => form.setValue("budget", event.target.value === "" ? undefined : Number(event.target.value) as unknown as number)} aria-invalid={Boolean(form.formState.errors.budget)} aria-describedby={form.formState.errors.budget ? "project-budget-error" : undefined} />
                  <FieldError id="project-budget-error" message={validationMessage(form.formState.errors.budget?.message)} />
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
                <Input id="project-initial-annual-budget" dir="ltr" type="number" min="0" step="0.0001" value={form.watch("initialAnnualBudgetAmount") ?? ""} onChange={event => { initialAnnualBudgetWasEdited.current = true; form.setValue("initialAnnualBudgetAmount", event.target.value === "" ? undefined : Number(event.target.value), { shouldValidate: true }); }} aria-invalid={Boolean(form.formState.errors.initialAnnualBudgetAmount)} aria-describedby={form.formState.errors.initialAnnualBudgetAmount ? "project-initial-budget-error" : undefined} />
                <FieldError id="project-initial-budget-error" message={validationMessage(form.formState.errors.initialAnnualBudgetAmount?.message)} />
                <p className="text-xs text-muted-foreground">{t("projects.initialAnnualBudgetHint", { currency: form.watch("currency") || "" })}</p>
              </div>
            </div> : null}
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-base font-semibold">{t("projects.createSections.lifecycle")}</h3>
              {!isEdit ? <p className="text-xs text-muted-foreground">{t("projects.createLifecycleDescription")}</p> : null}
            </div>
            {lifecycleOptions.isLoading ? <p role="status" className="text-sm text-muted-foreground">{isEdit ? `${t("projects.lifecycle.phaseLookupLoading")} · ${t("projects.lifecycle.statusLookupLoading")}` : t("projects.lifecycle.statusLookupLoading")}</p> : null}
            {lifecycleOptions.isError ? <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"><span>{t("projects.lifecycle.lookupError")}</span><Button type="button" size="sm" variant="outline" onClick={() => void lifecycleOptions.refetch()}>{t("projects.lifecycle.retry")}</Button></div> : null}
            {!isEdit && !lifecycleOptions.isError ? <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t("projects.lifecycle.phase")}</Label>
                <input type="hidden" name="phaseId" data-testid="project-phase-id" value={form.watch("phaseId") ?? ""} readOnly />
                {lifecycleOptions.isLoading ? (
                  <p role="status" className="text-sm text-muted-foreground">{t("projects.lifecycle.phaseLookupLoading")}</p>
                ) : !watchedPortfolioId ? (
                  <p className="text-sm text-muted-foreground">{t("projects.lifecycle.selectPortfolioForOptions")}</p>
                ) : initialPhaseOption ? (
                  <p data-testid="project-initial-phase" aria-readonly="true" className="rounded-md border bg-muted/30 px-3 py-2 text-sm font-medium">{projectLifecycleLabel(initialPhaseOption, locale)}</p>
                ) : (
                  <p className="text-sm text-muted-foreground">{t("projects.lifecycle.createPhaseUnavailable")}</p>
                )}
                <p className="text-xs text-muted-foreground">{t("projects.lifecycle.createInitialPhaseHint")}</p>
                <FieldError id="project-phase-error" message={validationMessage(form.formState.errors.phaseId?.message)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="project-status">{t("projects.lifecycle.status")}</Label>
                <Select value={watchedStatusId} onValueChange={(value) => form.setValue("statusId", value, { shouldValidate: true })}>
                  <SelectTrigger id="project-status" aria-label={t("projects.lifecycle.status")} aria-invalid={Boolean(form.formState.errors.statusId)} aria-describedby={form.formState.errors.statusId ? "project-status-error" : undefined} disabled={lifecycleOptions.isLoading || lifecycleOptions.isError || activeStatusOptions.length === 0}><SelectValue placeholder={t("projects.lifecycle.chooseStatus")} /></SelectTrigger>
                  <SelectContent>{activeStatusOptions.map(option => <SelectItem key={option.id} value={option.id}>{projectLifecycleLabel(option, locale)}</SelectItem>)}</SelectContent>
                </Select>
                {!lifecycleOptions.isLoading && !lifecycleOptions.isError && activeStatusOptions.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.lifecycle.noStatusesAvailable")}</p> : null}
                <FieldError id="project-status-error" message={validationMessage(form.formState.errors.statusId?.message)} />
              </div>
            </div> : project ? <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-2 rounded-lg border bg-muted/10 p-4">
                <p className="text-xs text-muted-foreground">{t("projects.lifecycle.currentPhase")}</p>
                <p className="font-medium">{currentPhaseLabel || t("common.notAvailable")}</p>
                {canChangeProjectPhase ? <Button type="button" size="sm" variant="outline" onClick={() => setPhaseGateOpen(true)}>{t("projects.lifecycle.changePhase")}</Button> : null}
              </div>
              <div className="space-y-3 rounded-lg border bg-muted/10 p-4">
                <div><p className="text-xs text-muted-foreground">{t("projects.lifecycle.currentStatus")}</p><p className="font-medium">{currentStatusLabel || t("common.notAvailable")}</p></div>
                {canChangeProjectStatus ? <>
                  <div className="space-y-1.5">
                    <Label htmlFor="project-status-target">{t("projects.lifecycle.changeStatus")}</Label>
                    <Select value={statusTargetId} onValueChange={(value) => { setStatusTargetId(value); setStatusChangeError(null); setStatusChangeSaved(false); }}>
                      <SelectTrigger id="project-status-target" aria-label={t("projects.lifecycle.changeStatus")} disabled={lifecycleOptions.isLoading || lifecycleOptions.isError || availableStatuses.length === 0}><SelectValue placeholder={t("projects.lifecycle.chooseStatus")} /></SelectTrigger>
                      <SelectContent>{availableStatuses.map((status) => <SelectItem key={status.id} value={status.id}>{projectLifecycleLabel(status, locale)}</SelectItem>)}</SelectContent>
                    </Select>
                    {!lifecycleOptions.isLoading && !lifecycleOptions.isError && availableStatuses.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.lifecycle.noOtherStatusesAvailable")}</p> : null}
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="project-status-reason">{t("projects.lifecycle.reason")}</Label>
                    <Textarea id="project-status-reason" rows={2} maxLength={2000} value={statusChangeReason} onChange={(event) => setStatusChangeReason(event.target.value)} placeholder={t("projects.lifecycle.statusChangeReasonHint")} />
                  </div>
                  {statusChangeError ? <p role="alert" className="text-sm text-destructive">{statusChangeError}</p> : null}
                  {statusChangeSaved ? <p role="status" className="text-sm text-success">{t("projects.lifecycle.statusUpdated")}</p> : null}
                  <Button type="button" size="sm" disabled={!statusTargetId || !availableStatuses.some((status) => status.id === statusTargetId) || changeProjectStatus.isPending || lifecycleOptions.isLoading || lifecycleOptions.isError} onClick={() => void submitStatusChange()}>{changeProjectStatus.isPending ? t("projects.lifecycle.savingStatus") : t("projects.lifecycle.saveStatusChange")}</Button>
                </> : null}
              </div>
            </div> : null}
          </section>

          <section className="space-y-4">
            <div className="space-y-1">
              <h3 className="text-base font-semibold">{t("projects.createSections.review")}</h3>
              {!isEdit && <p className="text-xs text-muted-foreground">{t("projects.createReviewDescription")}</p>}
              <p className="text-xs text-muted-foreground">{t("projects.initiationSummaryDescription")}</p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="project-business-case">{t("projects.businessCase")}</Label>
              <Textarea id="project-business-case" placeholder={t("projects.businessCase")} rows={3} value={form.watch("businessCase") ?? ""} onChange={(e) => form.setValue("businessCase", e.target.value)} />
            </div>

            {isEdit && (
              <div className="space-y-1.5">
                <Label htmlFor="project-notes">{t("projects.notes")}</Label>
                <Textarea id="project-notes" placeholder={t("projects.notes")} rows={2} value={form.watch("notes") ?? ""} onChange={(e) => form.setValue("notes", e.target.value)} />
              </div>
            )}
          </section>
        </ProjectSheetBody>

        <ProjectSheetFooter embedded={embedded}>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={isSubmitting || initialPhaseUnresolved || createStatusUnresolved}>
            {isSubmitting ? (<><Loader2 className="me-2 h-4 w-4 animate-spin" />{isEdit ? t("projects.updating") : t("common.creating")}</>) : isEdit ? t("projects.update") : t("projects.create")}
          </Button>
        </ProjectSheetFooter>
      </form>
  );

  if (embedded)
    return <>
      <div className="overflow-hidden rounded-lg border border-border bg-card">{formContent}</div>
      {isEdit && project ? <ProjectPhaseGateSideSheet open={phaseGateOpen} onOpenChange={setPhaseGateOpen} project={project} /> : null}
    </>;

  return (
    <>
      <SideSheet
        open={open}
        onOpenChange={onOpenChange}
        title={isEdit ? t("projects.edit") : t("projects.create")}
        description={isEdit ? t("projects.editDescription") : t("projects.createDescription")}
        size="lg"
      >
        {formContent}
      </SideSheet>
      {isEdit && project ? <ProjectPhaseGateSideSheet open={phaseGateOpen} onOpenChange={setPhaseGateOpen} project={project} /> : null}
    </>
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
