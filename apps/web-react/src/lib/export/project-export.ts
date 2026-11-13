import { api } from "@api/client";
import { brand } from "@lib/brand";

interface ExportProjectSummary {
  id: string;
  name: string;
  code: string;
  status: string;
  statusNameEn?: string | null;
  statusNameAr?: string | null;
  reportingCategory?: "InProgress" | "Completed" | "Excluded" | null;
  health?: string;
  planProgress?: number | null;
  endDate?: string;
  budget?: number;
  actualCost?: number;
  currency?: string;
  projectManagerName?: string;
}

interface PagedExportResult {
  items: readonly ExportProjectSummary[];
  totalPages: number;
  totalCount: number;
}

export interface ProjectExportParams {
  status?: string;
  includeAllStatuses?: boolean;
  search?: string;
  name?: string;
  code?: string;
  statuses?: string[];
  healths?: string[];
  progressMin?: number;
  progressMax?: number;
  budgetMin?: number;
  budgetMax?: number;
  forecastMin?: number;
  forecastMax?: number;
  projectManager?: string;
  dueDateFrom?: string;
  dueDateTo?: string;
}

export async function fetchFilteredProjects(params: ProjectExportParams = {}): Promise<ExportProjectSummary[]> {
  const { status, includeAllStatuses = false, search, name, code, statuses, healths, progressMin, progressMax, budgetMin, budgetMax, forecastMin, forecastMax, projectManager, dueDateFrom, dueDateTo } = params;

  const pageSize = 100;
  const all: ExportProjectSummary[] = [];
  let page = 1;
  let totalPages = 1;

  do {
    const result = await api.get<PagedExportResult>("/api/projects", { page, pageSize, status });
    all.push(...Array.from(result.items));
    totalPages = result.totalPages > 0 ? result.totalPages : 1;
    page += 1;
  } while (page <= totalPages);

  const active = statuses && statuses.length > 0
    ? all
    : includeAllStatuses
      ? all
      : all.filter((p) => p.reportingCategory !== "Excluded");

  return active.filter((p) => {
    if (name?.trim() && !p.name.toLowerCase().includes(name.toLowerCase())) return false;
    if (code?.trim() && !p.code.toLowerCase().includes(code.toLowerCase())) return false;
    if (statuses && statuses.length > 0 && !statuses.some((status) => status.toLocaleUpperCase() === p.status.toLocaleUpperCase())) return false;
    if (healths && healths.length > 0 && p.health && !healths.includes(p.health)) return false;
    if (progressMin !== undefined && (p.planProgress == null || p.planProgress < progressMin)) return false;
    if (progressMax !== undefined && (p.planProgress == null || p.planProgress > progressMax)) return false;
    if (budgetMin !== undefined && (p.budget || 0) < budgetMin) return false;
    if (budgetMax !== undefined && (p.budget || 0) > budgetMax) return false;
    if (forecastMin !== undefined && (p.actualCost || 0) < forecastMin) return false;
    if (forecastMax !== undefined && (p.actualCost || 0) > forecastMax) return false;
    if (projectManager?.trim() && !p.projectManagerName?.toLowerCase().includes(projectManager.toLowerCase())) return false;
    if (dueDateFrom && p.endDate && new Date(p.endDate) < new Date(dueDateFrom)) return false;
    if (dueDateTo && p.endDate && new Date(p.endDate) > new Date(dueDateTo)) return false;
    if (search?.trim()) {
      const q = search.trim().toLowerCase();
      if (!p.name.toLowerCase().includes(q) && !p.code.toLowerCase().includes(q) && !(p.projectManagerName ?? "").toLowerCase().includes(q)) return false;
    }
    return true;
  });
}

function displayWidth(value: string): number {
  let width = 0;
  for (const ch of value) {
    const code = ch.codePointAt(0) ?? 0;
    if (code >= 0x0600 && code <= 0x06ff) width += 1.5;
    else if (code >= 0x2e80 && code <= 0x9fff) width += 2;
    else width += 1;
  }
  return width;
}

function exportFileName(extension: "xlsx" | "pdf"): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `projects-${year}-${month}-${day}.${extension}`;
}

function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export interface ProjectsExcelOptions {
  headers: string[];
  rows: string[][];
}

export async function generateProjectsExcel(options: ProjectsExcelOptions): Promise<void> {
  const { headers, rows } = options;

  const { default: ExcelJS } = await import("exceljs");

  const workbook = new ExcelJS.Workbook();
  workbook.creator = brand.product.fullName;

  const worksheet = workbook.addWorksheet("Projects", {
    views: [{ state: "frozen", ySplit: 1 }],
  });

  const headerRow = worksheet.addRow(headers);
  headerRow.font = { bold: true, color: { argb: "FFFFFFFF" }, size: 11 };
  headerRow.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF1F6E8C" } };
  headerRow.alignment = { vertical: "middle", horizontal: "center" };
  headerRow.height = 22;

  rows.forEach((row) => worksheet.addRow(row));

  headers.forEach((_, index) => {
    const column = worksheet.getColumn(index + 1);
    const header = headers[index] ?? "";
    let max = Math.max(displayWidth(header), 10);
    column.eachCell({ includeEmpty: true }, (cell) => {
      if (cell.value !== null && cell.value !== undefined) {
        max = Math.max(max, displayWidth(String(cell.value)));
      }
    });
    column.width = Math.min(max + 2, 60);
    column.alignment = { vertical: "middle" };
  });

  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });

  downloadBlob(blob, exportFileName("xlsx"));
}

export interface ProjectsPdfOptions {
  headers: string[];
  rows: string[][];
  title: string;
  exportDateLabel: string;
  exportDate: string;
  filtersLabel: string;
  filtersSummary: string;
  countLabel: string;
  locale: string;
}

const PDF_WIDTHS = [170, 60, 70, 55, 60, 90, 90, 110, 75];

export async function generateProjectsPdf(options: ProjectsPdfOptions): Promise<void> {
  const { headers, rows, title, exportDateLabel, exportDate, filtersLabel, filtersSummary, countLabel, locale } = options;

  const isRtl = locale === "ar" || /[\u0600-\u06FF]/.test(headers.join(""));
  const font = isRtl ? "Cairo" : "Roboto";
  const align = isRtl ? "right" : "left";

  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default ?? pdfModule;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  const vfs = vfsModule.default ?? vfsModule;

  pdfMake.addVirtualFileSystem(vfs);
  pdfMake.addFonts({
    Roboto: {
      normal: "Roboto-Regular.ttf",
      bold: "Roboto-Medium.ttf",
      italics: "Roboto-Italic.ttf",
      bolditalics: "Roboto-MediumItalic.ttf",
    },
    Cairo: {
      normal: "Cairo-Regular.ttf",
      bold: "Cairo-Bold.ttf",
      italics: "Cairo-Regular.ttf",
      bolditalics: "Cairo-Bold.ttf",
    },
  });

  const headerCells = headers.map((header) => ({
    text: header,
    bold: true,
    color: "#FFFFFF",
    fillColor: "#2469A8",
    alignment: align,
    margin: [4, 4, 4, 4] as [number, number, number, number],
  }));

  const bodyRows = rows.map((row) =>
    row.map((cell) => ({
      text: cell,
      alignment: align,
      margin: [4, 3, 4, 3] as [number, number, number, number],
    }))
  );

  const docDefinition = {
    rtl: isRtl,
    pageSize: "A4",
    pageOrientation: "landscape",
    pageMargins: [24, 28, 24, 40] as [number, number, number, number],
    defaultStyle: { font, fontSize: 9 },
    info: { title },
    content: [
      { text: title, fontSize: 16, bold: true, alignment: align, margin: [0, 0, 0, 6] },
      { text: `${exportDateLabel}: ${exportDate}`, fontSize: 9, color: "#697586", alignment: align, margin: [0, 0, 0, 3] },
      { text: `${filtersLabel}: ${filtersSummary}`, fontSize: 9, color: "#697586", alignment: align, margin: [0, 0, 0, 3] },
      { text: `${countLabel}: ${rows.length}`, fontSize: 9, color: "#697586", alignment: align, margin: [0, 0, 0, 10] },
      {
        table: {
          rtl: isRtl,
          headerRows: 1,
          widths: PDF_WIDTHS,
          body: [headerCells, ...bodyRows],
        },
        layout: {
          hLineWidth: () => 0.5,
          vLineWidth: () => 0.5,
          hLineColor: () => "#DDE3E9",
          vLineColor: () => "#DDE3E9",
          paddingTop: () => 4,
          paddingBottom: () => 4,
          paddingLeft: () => 4,
          paddingRight: () => 4,
        },
      },
    ],
    footer: (currentPage: number, pageCount: number) => ({
      text: `${currentPage} / ${pageCount}`,
      alignment: "center",
      fontSize: 8,
      color: "#697586",
      margin: [0, 10, 0, 0] as [number, number, number, number],
    }),
  };

  const doc = pdfMake.createPdf(docDefinition);
  const blob = await doc.getBlob();
  downloadBlob(blob, exportFileName("pdf"));
}
