import * as React from "react";
import { FileSpreadsheet, FileText, Loader2 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";
import {
  exportFinancialReportingExcel,
  exportFinancialReportingPdf,
  type FinancialReportingExportFormat,
  type FinancialReportingExportInput,
} from "./financial-reporting-export";

interface FinancialReportingExportButtonsProps {
  ready: boolean;
  makeInput: () => FinancialReportingExportInput | null;
}

export function FinancialReportingExportButtons({ ready, makeInput }: FinancialReportingExportButtonsProps) {
  const { t } = useI18n();
  const [busy, setBusy] = React.useState<FinancialReportingExportFormat | null>(null);
  const [failed, setFailed] = React.useState(false);

  const run = React.useCallback(async (format: FinancialReportingExportFormat) => {
    const input = makeInput();
    if (!input) return;
    setBusy(format);
    setFailed(false);
    try {
      if (format === "pdf") await exportFinancialReportingPdf(input);
      else await exportFinancialReportingExcel(input);
    } catch {
      setFailed(true);
    } finally {
      setBusy(null);
    }
  }, [makeInput]);

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex items-center gap-2">
        <Button type="button" size="sm" variant="outline" disabled={!ready || busy !== null} onClick={() => void run("xlsx")}>
          {busy === "xlsx" ? <Loader2 className="me-2 h-4 w-4 animate-spin" aria-hidden="true" /> : <FileSpreadsheet className="me-2 h-4 w-4" aria-hidden="true" />}
          {t("financialReportingExport.excel")}
        </Button>
        <Button type="button" size="sm" variant="outline" disabled={!ready || busy !== null} onClick={() => void run("pdf")}>
          {busy === "pdf" ? <Loader2 className="me-2 h-4 w-4 animate-spin" aria-hidden="true" /> : <FileText className="me-2 h-4 w-4" aria-hidden="true" />}
          {t("financialReportingExport.pdf")}
        </Button>
      </div>
      {failed ? <p role="alert" className="text-xs text-destructive">{t("financialReportingExport.exportFailed")}</p> : null}
    </div>
  );
}