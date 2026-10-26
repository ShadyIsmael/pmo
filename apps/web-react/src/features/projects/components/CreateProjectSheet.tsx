import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router-dom";
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
import { toUserSelectOptions, useUserLookup } from "../lib/project-lookups";
import { useProjectEnumLabels } from "../lib/project-labels";
import { useCurrencies } from "@features/administration/api/admin";
import { usePortfolios, usePrograms } from "@features/delivery/api/delivery";
import { useOrganizationUnits } from "@features/organization/api/organization";
import { PROJECT_HEALTHS, PROJECT_PRIORITIES, PROJECT_STATUSES } from "@lib/enums";
import { toDateInput } from "@lib/utils";
import { ApiError } from "@api/client";

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
  onOpenChange: (open: boolean) => void;
  project?: ProjectDto | null;
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
    category: "",
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

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs text-destructive">{message}</p>;
}

export function ProjectSheet({ open, onOpenChange, project }: ProjectSheetProps) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const isEdit = !!project;
  const createMutation = useCreateProject();
  const updateMutation = useUpdateProject(project?.publicId ?? "");
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);
  const isSubmittingRef = React.useRef(false);

  const { data: userLookup, isLoading: usersLoading } = useUserLookup(undefined, open);
  const { data: projectManagers, isLoading: managersLoading } = useUserLookup("project.manager", open);
  const { data: portfolioData } = usePortfolios(open);
  const { data: programData } = usePrograms(open);
  const { data: departmentUnits } = useOrganizationUnits(open);
  const { data: currencies } = useCurrencies(open);

  const userOptions = toUserSelectOptions(userLookup ?? []);
  const projectManagerOptions = toUserSelectOptions(projectManagers ?? []);
  const portfolioOptions = (portfolioData?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const programOptions = (programData?.items ?? []).map((p) => ({ value: p.id, label: p.name }));
  const portfolioCurrencyMap = React.useMemo<Record<string, string>>(() => Object.fromEntries((portfolioData?.items ?? []).map((p) => [p.id, p.currency ?? ""])), [portfolioData]);
  const programLookupMap = React.useMemo(() => Object.fromEntries((programData?.items ?? []).map((p) => [p.id, p])), [programData]);
  const departmentOptions = (departmentUnits ?? []).map((d) => ({ value: d.id, label: d.name }));
  const baseCurrencyCode = currencies?.find((currency) => currency.isBase)?.code ?? currencies?.[0]?.code ?? "";
  const currentProjectCurrency = project?.currency ?? "";
  const currencyOptions = React.useMemo(() => {
    const options = currencies ?? [];
    if (currentProjectCurrency && !options.some((currency) => currency.code === currentProjectCurrency)) {
      return [{ id: currentProjectCurrency, code: currentProjectCurrency, name: currentProjectCurrency, symbol: "", exchangeRate: 1, isBase: false }, ...options];
    }
    return options;
  }, [currencies, currentProjectCurrency]);

  const form = useForm<ProjectFormData>({
    resolver: zodResolver(projectSchema) as never,
    defaultValues: isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode),
  });
  const selectedProgramId = form.watch("programId");

  React.useEffect(() => {
    if (!open) return;
    form.reset(isEdit && project ? toEditDefaultValues(project) : createDefaultValues(baseCurrencyCode));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, project]);

  React.useEffect(() => {
    if (!open || isEdit || !baseCurrencyCode || form.getValues("currency")) return;
    form.setValue("currency", baseCurrencyCode, { shouldValidate: true });
  }, [baseCurrencyCode, form, isEdit, open]);

  const onSubmit = async (data: ProjectFormData) => {
    if (isSubmittingRef.current) return;
    if (!isEdit && !data.projectManagerId) {
      form.setError("projectManagerId", { type: "manual", message: t("projects.projectManagerRequired") });
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
          clearSponsor: Boolean(project.sponsorId && !data.sponsorId),
          name: data.name,
          status: data.status,
          priority: data.priority,
          projectManagerId: data.projectManagerId || undefined,
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
        if (result?.publicId) {
          onOpenChange(false);
          form.reset();
          navigate(`/projects/${result.publicId}`);
          return;
        }
      }

      onOpenChange(false);
      form.reset();
    } catch (err) {
      const apiMessage = err instanceof ApiError ? err.message : err instanceof Error ? err.message : "Failed to save project";
      const normalized = apiMessage.toLowerCase();
      if (normalized.includes("code") && (normalized.includes("exist") || normalized.includes("duplicate") || normalized.includes("already"))) {
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
    <SideSheet
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t("projects.edit") : t("projects.create")}
      description={isEdit ? t("projects.editDescription") : t("projects.createDescription")}
      size="lg"
    >
      <form onSubmit={form.handleSubmit(onSubmit as never)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-5">
          {errorMessage && (
            <div className="flex items-center gap-2 rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">
              <AlertCircle className="h-4 w-4 shrink-0" />
              {errorMessage}
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
                <Input placeholder={t("projects.code")} value={form.watch("code") ?? ""} onChange={(e) => form.setValue("code", e.target.value)} disabled={!isEdit} readOnly={!isEdit} />
                <FieldError message={form.formState.errors.code?.message} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.name")} <span className="text-destructive">*</span></Label>
                <Input placeholder={t("projects.name")} value={form.watch("name") ?? ""} onChange={(e) => form.setValue("name", e.target.value, { shouldValidate: true })} />
                <FieldError message={form.formState.errors.name?.message} />
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
                <Label>{t("projects.projectManager")} {!isEdit && <span className="text-destructive">*</span>}</Label>
                <SearchableSelect options={projectManagerOptions} value={form.watch("projectManagerId") ?? ""} onValueChange={(v) => form.setValue("projectManagerId", v)} placeholder={t("projects.projectManager")} searchPlaceholder={t("projects.projectManager")} loading={managersLoading} emptyText={t("common.noResults")} />
                <FieldError message={form.formState.errors.projectManagerId?.message} />
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
                <Input type="date" value={form.watch("startDate") ?? ""} onChange={(e) => form.setValue("startDate", e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.plannedEndDate")}</Label>
                <Input type="date" value={form.watch("endDate") ?? ""} onChange={(e) => form.setValue("endDate", e.target.value)} />
                <FieldError message={form.formState.errors.endDate?.message} />
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
                  <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((currency) => (
                      <SelectItem key={currency.id} value={currency.code}>{currency.code} — {currency.name}{currency.symbol ? ` (${currency.symbol})` : ""}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError message={form.formState.errors.currency?.message} />
              </div>
              <div className="space-y-1.5">
                <Label>{t("projects.budgetCeiling")}</Label>
                <Input type="number" step="0.01" min="0" placeholder="0" value={form.watch("budget") ?? ""} onChange={(event) => form.setValue("budget", event.target.value === "" ? undefined : Number(event.target.value) as unknown as number)} disabled={isEdit} readOnly={isEdit} />
                <p className="text-xs text-muted-foreground">{t("projects.budgetCeilingHint")}</p>
              </div>
            </div>
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
        </SideSheetBody>

        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? (<><Loader2 className="mr-2 h-4 w-4 animate-spin" />{isEdit ? t("projects.updating") : t("common.creating")}</>) : isEdit ? t("projects.update") : t("projects.create")}
          </Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

export interface LegacyProjectSheetValues {
  name: string;
  description: string;
}

export function CreateProjectSheet({ open, onOpenChange, mode, initialValues, onSubmit }: { open: boolean; onOpenChange: (open: boolean) => void; mode?: "create" | "edit"; initialValues?: Partial<LegacyProjectSheetValues>; onSubmit?: (values: LegacyProjectSheetValues) => void | Promise<void> }) {
  // Legacy callers (e.g. ProjectHeader edit) use mode/initialValues/onSubmit.
  // Render a lightweight legacy modal for those, otherwise the full golden sheet.
  if (mode || initialValues || onSubmit) {
    return <LegacyCreateProjectSheet open={open} onOpenChange={onOpenChange} mode={mode} initialValues={initialValues} onSubmit={onSubmit} />;
  }
  return <ProjectSheet open={open} onOpenChange={onOpenChange} />;
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

export function EditProjectSheet({ open, onOpenChange, project }: { open: boolean; onOpenChange: (open: boolean) => void; project: ProjectDto | null }) {
  return <ProjectSheet open={open} onOpenChange={onOpenChange} project={project} />;
}
