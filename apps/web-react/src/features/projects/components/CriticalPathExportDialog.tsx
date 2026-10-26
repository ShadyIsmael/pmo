import * as React from "react";
import { Download, LoaderCircle } from "lucide-react";
import { Button } from "@components/ui/Button";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useI18n } from "@i18n/messages";
import {
  criticalPathExportErrorMessage,
  DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS,
  downloadCriticalPathPdf,
  downloadCriticalPathPng,
  downloadCriticalPathSvg,
  prepareCriticalPathExport,
  type CriticalPathExportErrorCode,
  type CriticalPathExportOptions,
  type CriticalPathExportSnapshot,
} from "../lib/critical-path-export";

type Translate = (key: string, params?: Record<string, string | number>) => string;
type ExportStatus = "idle" | "preparing" | "rendering" | "generating" | "cancelling" | "complete";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  snapshot: CriticalPathExportSnapshot | null;
  t: Translate;
}

function labelKey(t: Translate, key: string, params?: Record<string, string | number>) {
  return t(`planning.exportDiagram.${key}`, params);
}

export function CriticalPathExportDialog({ open, onOpenChange, snapshot, t }: Props) {
  const { locale, formatDate } = useI18n();
  const [options, setOptions] = React.useState<CriticalPathExportOptions>(DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS);
  const [status, setStatus] = React.useState<ExportStatus>("idle");
  const [errorMessage, setErrorMessage] = React.useState("");
  const [success, setSuccess] = React.useState(false);
  const controllerRef = React.useRef<AbortController | null>(null);
  const busy = status !== "idle" && status !== "complete";

  React.useEffect(() => {
    if (!open) {
      controllerRef.current?.abort();
      controllerRef.current = null;
      setStatus("idle");
      setErrorMessage("");
      setSuccess(false);
      return;
    }
    setOptions(DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS);
    setStatus("idle");
    setErrorMessage("");
    setSuccess(false);
  }, [open]);

  const update = <K extends keyof CriticalPathExportOptions>(key: K, value: CriticalPathExportOptions[K]) =>
    setOptions((current) => ({ ...current, [key]: value }));

  const exportFile = async () => {
    if (!snapshot || busy) return;
    const controller = new AbortController();
    controllerRef.current = controller;
    setErrorMessage("");
    setSuccess(false);
    setStatus("preparing");
    try {
      await yieldToUi();
      if (controller.signal.aborted) return;
      const exportSnapshot = { ...snapshot, exportedAt: new Date().toISOString() };
      const model = prepareCriticalPathExport(exportSnapshot, options, makeLabels(locale, formatDate, t));
      setStatus("rendering");
      await yieldToUi();
      if (controller.signal.aborted) return;
      setStatus("generating");
      if (options.format === "pdf") await downloadCriticalPathPdf(model, controller.signal);
      else if (options.format === "svg") downloadCriticalPathSvg(model, controller.signal);
      else await downloadCriticalPathPng(model, controller.signal);
      if (controller.signal.aborted) return;
      setSuccess(true);
      setStatus("complete");
    } catch (error) {
      const messages: Record<CriticalPathExportErrorCode, string> = {
        analysisUnavailable: labelKey(t, "errors.analysisUnavailable"), cycle: labelKey(t, "errors.cycle"),
        unscheduledActivities: labelKey(t, "errors.unscheduledActivities"), missingDependencies: labelKey(t, "errors.missingDependencies"),
        invalidDependencies: labelKey(t, "errors.invalidDependencies"), staleSnapshot: labelKey(t, "errors.staleSnapshot"),
        noCriticalPath: labelKey(t, "errors.noCriticalPath"), invalidCriticalPath: labelKey(t, "errors.invalidCriticalPath"),
        emptyNetwork: labelKey(t, "errors.emptyNetwork"), tooLarge: labelKey(t, "errors.tooLarge"),
        fitTooSmall: labelKey(t, "errors.fitTooSmall"), pngTooLarge: labelKey(t, "errors.pngTooLarge"),
        pageTooLarge: labelKey(t, "errors.pageTooLarge"), invalidData: labelKey(t, "errors.invalidData"), cancelled: labelKey(t, "errors.cancelled"),
      };
      const message = criticalPathExportErrorMessage(error, messages);
      if (controller.signal.aborted || message === messages.cancelled) setStatus("idle");
      else {
        setErrorMessage(message);
        setStatus("idle");
      }
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  };

  const cancel = () => {
    controllerRef.current?.abort();
    setStatus("cancelling");
  };

  const close = () => {
    if (!busy) onOpenChange(false);
  };

  const isPdf = options.format === "pdf";
  const isPng = options.format === "png";

  return <SideSheet open={open} onOpenChange={(value) => { if (value || !busy) onOpenChange(value); }} title={labelKey(t, "title")} description={labelKey(t, "description")} closeLabel={labelKey(t, "close")} size="lg" ariaLabel={labelKey(t, "title")}>
      <SideSheetBody className="space-y-4 text-start">
      <div className="grid gap-4 sm:grid-cols-2">
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">{labelKey(t, "scope")}</legend>
          <RadioCard name="diagram-scope" value="full" selected={options.scope === "full"} disabled={busy} onChange={() => update("scope", "full")} title={labelKey(t, "fullNetwork")} description={labelKey(t, "fullNetworkDescription")} />
          <RadioCard name="diagram-scope" value="critical" selected={options.scope === "critical"} disabled={busy} onChange={() => update("scope", "critical")} title={labelKey(t, "criticalOnly")} description={labelKey(t, "criticalOnlyDescription")} />
        </fieldset>

        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">{labelKey(t, "format")}</legend>
          <div className="grid grid-cols-3 gap-2">
            {(["pdf", "svg", "png"] as const).map((format) => <label key={format} className={`flex cursor-pointer items-center gap-2 rounded-md border px-3 py-2 text-sm ${options.format === format ? "border-primary bg-primary/5" : "border-border"}`}>
              <input type="radio" name="diagram-format" value={format} checked={options.format === format} disabled={busy} onChange={() => update("format", format)} />
              <span className="font-medium">{labelKey(t, format)}</span>
              {format === "pdf" ? <span className="text-[10px] text-muted-foreground">{labelKey(t, "recommended")}</span> : null}
            </label>)}
          </div>
          {isPdf ? <>
            <Field label={labelKey(t, "pageSize")}>
              <select className="input-field h-9" value={options.pageSize} disabled={busy} onChange={(event) => update("pageSize", event.target.value as CriticalPathExportOptions["pageSize"])}>
                <option value="AUTO">{labelKey(t, "autoPageSize")}</option>
                {(["A4", "A3", "A2", "A1", "A0"] as const).map((size) => <option key={size} value={size}>{size}</option>)}
                <option value="CUSTOM">{labelKey(t, "customLargeFormat")}</option>
              </select>
            </Field>
            <Field label={labelKey(t, "orientation")}>
              <select className="input-field h-9" value={options.orientation} disabled={busy} onChange={(event) => update("orientation", event.target.value as CriticalPathExportOptions["orientation"])}>
                <option value="auto">{labelKey(t, "automatic")}</option><option value="landscape">{labelKey(t, "landscape")}</option><option value="portrait">{labelKey(t, "portrait")}</option>
              </select>
            </Field>
            <Field label={labelKey(t, "pdfLayout")}>
              <select className="input-field h-9" value={options.pdfLayout} disabled={busy} onChange={(event) => update("pdfLayout", event.target.value as CriticalPathExportOptions["pdfLayout"])}>
                <option value="fit">{labelKey(t, "fitEntire")}</option><option value="detailed">{labelKey(t, "multiPageTiled")}</option>
              </select>
            </Field>
          </> : null}
          {isPng ? <Field label={labelKey(t, "pngScale")}>
            <select className="input-field h-9" value={options.pngScale} disabled={busy} onChange={(event) => update("pngScale", Number(event.target.value) as 1 | 2 | 3)}>
              <option value={1}>1× · {labelKey(t, "standard")}</option><option value={2}>2× · {labelKey(t, "highResolution")}</option><option value={3}>3× · {labelKey(t, "maximumResolution")}</option>
            </select>
          </Field> : null}
        </fieldset>
      </div>

      <fieldset className="space-y-2 rounded-md border p-3">
        <legend className="px-1 text-sm font-medium">{labelKey(t, "additionalOptions")}</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          <Check label={labelKey(t, "showTaskDates")} checked={options.showTaskDates} disabled={busy} onChange={(value) => update("showTaskDates", value)} />
          <Check label={labelKey(t, "showDuration")} checked={options.showDuration} disabled={busy} onChange={(value) => update("showDuration", value)} />
          <Check label={labelKey(t, "showTotalFloat")} checked={options.showTotalFloat} disabled={busy} onChange={(value) => update("showTotalFloat", value)} />
          <Check label={labelKey(t, "showProgress")} checked={options.showProgress} disabled={busy} onChange={(value) => update("showProgress", value)} />
          <Check label={labelKey(t, "showDependencyLabels")} checked={options.showDependencyLabels} disabled={busy} onChange={(value) => update("showDependencyLabels", value)} />
          <Check label={labelKey(t, "includeLegend")} checked={options.includeLegend} disabled={busy} onChange={(value) => update("includeLegend", value)} />
          <Check label={labelKey(t, "includeSummary")} checked={options.includeSummary} disabled={busy} onChange={(value) => update("includeSummary", value)} />
        </div>
      </fieldset>

      {busy ? <div className="flex items-center gap-2 rounded-md bg-muted/40 px-3 py-2 text-sm" role="status">
        {status === "cancelling" ? null : <LoaderCircle className="h-4 w-4 animate-spin" />}
        {statusLabel(t, status)}
      </div> : null}
      {errorMessage ? <p role="alert" className="rounded-md border border-destructive/40 px-3 py-2 text-sm text-destructive">{errorMessage}</p> : null}
      {success ? <p role="status" className="rounded-md border border-emerald-600/40 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-400">{labelKey(t, "success")}</p> : null}

      </SideSheetBody>
      <SideSheetFooter>
        {busy ? <Button type="button" variant="outline" onClick={cancel} disabled={status === "cancelling" || status === "generating" && options.format === "pdf"}>{labelKey(t, "cancelExport")}</Button> : <Button type="button" variant="outline" onClick={close}>{success ? labelKey(t, "done") : labelKey(t, "close")}</Button>}
        {!busy && !success ? <Button type="button" onClick={() => void exportFile()} disabled={!snapshot} className="gap-2"><Download className="h-4 w-4" />{labelKey(t, "export")}</Button> : null}
        {!busy && success ? <Button type="button" onClick={() => void exportFile()} disabled={!snapshot} className="gap-2"><Download className="h-4 w-4" />{labelKey(t, "exportAnotherCopy")}</Button> : null}
      </SideSheetFooter>
    </SideSheet>;
}

function RadioCard({ name, value, selected, disabled, onChange, title, description }: { name: string; value: string; selected: boolean; disabled: boolean; onChange: () => void; title: string; description: string }) {
  return <label className={`flex cursor-pointer items-start gap-2 rounded-md border p-2.5 ${selected ? "border-primary bg-primary/5" : "border-border"}`}>
    <input type="radio" name={name} value={value} checked={selected} disabled={disabled} onChange={onChange} className="mt-0.5" />
    <span><span className="block text-sm font-medium">{title}</span><span className="block text-xs text-muted-foreground">{description}</span></span>
  </label>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="mt-2 block space-y-1 text-xs font-medium text-foreground">{label}{children}</label>;
}

function Check({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (value: boolean) => void }) {
  return <label className="flex cursor-pointer items-center gap-2 text-sm"><input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} />{label}</label>;
}

function makeLabels(locale: string, formatDate: (value: string, options?: { numberingSystem?: string }) => string, t: Translate) {
  const key = (value: string) => labelKey(t, value);
  const dates = (value: string) => {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "—" : formatDate(value, { numberingSystem: "latn" });
  };
  const numberFormatter = new Intl.NumberFormat(locale === "ar" ? "ar-SA-u-nu-latn" : "en-US", { maximumFractionDigits: 2 });
  return {
    title: key("metadataTitle"), subtitle: key("description"), project: key("project"), plan: key("plan"), workingVersion: key("workingVersion"), exportedAt: key("exportedAt"),
    activities: key("activities"), dependencies: key("dependencies"), criticalActivities: key("criticalActivities"), criticalPaths: key("criticalPaths"), projectFinish: key("projectFinish"),
    task: key("node.task"), summary: key("node.summary"), milestone: key("node.milestone"), critical: key("node.critical"), nonCritical: key("node.nonCritical"),
    taskId: key("node.taskId"), wbs: key("node.wbs"), name: key("node.name"), duration: key("node.duration"), start: key("node.start"), finish: key("node.finish"), totalFloat: key("node.totalFloat"), progress: key("node.progress"), days: key("node.days"), planned: key("node.planned"),
    dependency: key("legend.dependency"), criticalDependency: key("legend.criticalDependency"), normalDependency: key("legend.normalDependency"), criticalMilestone: key("legend.criticalMilestone"), normalMilestone: key("legend.normalMilestone"),
    fullNetwork: key("fullNetwork"), criticalOnly: key("criticalOnly"), page: key("page"), of: key("of"), pageGuide: key("pageGuide"), tile: key("tile"), row: key("row"), column: key("column"), pageActivities: key("pageActivities"), crossPageConnectors: key("crossPageConnectors"), connection: key("connection"), from: key("from"), to: key("to"), type: key("type"), lagLead: key("lagLead"), sourcePage: key("sourcePage"), targetPage: key("targetPage"), noLag: key("noLag"), legend: key("legend.title"), projectFinishUnavailable: key("unavailable"),
    direction: locale === "ar" ? "rtl" as const : "ltr" as const,
    dependencyType: {
      FinishToStart: key("dependencyType.finishToStart"), StartToStart: key("dependencyType.startToStart"),
      FinishToFinish: key("dependencyType.finishToFinish"), StartToFinish: key("dependencyType.startToFinish"),
    },
    dependencyShortCode: { FinishToStart: "FS", StartToStart: "SS", FinishToFinish: "FF", StartToFinish: "SF" },
    formatDate: dates,
    formatNumber: (value: number) => numberFormatter.format(value),
  };
}

function statusLabel(t: Translate, status: ExportStatus): string {
  if (status === "preparing") return labelKey(t, "status.preparing");
  if (status === "rendering") return labelKey(t, "status.rendering");
  if (status === "generating") return labelKey(t, "status.generating");
  return labelKey(t, "status.cancelling");
}

function yieldToUi(): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, 0));
}
