import { afterEach, describe, expect, it, vi } from "vitest";
import type { PlanActivityDto, PlanDependencyDto, PlanSchedulingAnalysisDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import {
  buildCriticalPathPdfBlob,
  buildCriticalPathPdfDocumentPlan,
  buildCriticalPathSvg,
  DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS,
  downloadCriticalPathPng,
  getCriticalPathPngLimits,
  prepareCriticalPathExport,
  CriticalPathExportError,
  type CriticalPathExportLabels,
  type CriticalPathExportOptions,
  type CriticalPathExportSnapshot,
} from "@features/projects/lib/critical-path-export";

const labels: CriticalPathExportLabels = {
  title: "FOPX PM — Critical Path Network Diagram", subtitle: "Saved Working schedule", project: "Project", plan: "Plan", workingVersion: "Working version", exportedAt: "Exported",
  activities: "Activities", dependencies: "Dependencies", criticalActivities: "Critical activities", criticalPaths: "Critical paths", projectFinish: "Project finish",
  task: "Task", summary: "Summary task", milestone: "Milestone", critical: "Critical", nonCritical: "Non-critical", taskId: "Task ID", wbs: "WBS", name: "Name",
  duration: "Duration", start: "Planned start", finish: "Planned finish", totalFloat: "Total float", progress: "Progress", days: "days", planned: "Planned",
  dependency: "Dependency", criticalDependency: "Critical dependency", normalDependency: "Normal dependency", criticalMilestone: "Critical milestone", normalMilestone: "Non-critical milestone",
  fullNetwork: "Full Network — Critical Highlighted", criticalOnly: "Critical Path Only", page: "Page", of: "of", pageGuide: "Page location guide", pageActivities: "Activities on page",
  crossPageConnectors: "Cross-page connectors", tile: "Tile", row: "Row", column: "Column", connection: "Link", from: "From", to: "To", type: "Type", lagLead: "Lead / lag", sourcePage: "From page", targetPage: "To page", noLag: "None",
  legend: "Legend", projectFinishUnavailable: "Not available", direction: "ltr",
  dependencyType: { FinishToFinish: "Finish to Finish", StartToStart: "Start to Start", FinishToStart: "Finish to Start", StartToFinish: "Start to Finish" },
  dependencyShortCode: { FinishToFinish: "FF", StartToStart: "SS", FinishToStart: "FS", StartToFinish: "SF" },
  formatDate: (value) => value.slice(0, 10), formatNumber: (value) => String(value),
};

function makeActivity(id: string, type: PlanActivityDto["type"] = "Task", options: Partial<PlanActivityDto> = {}): PlanActivityDto {
  return {
    id,
    planVersionId: "version-1",
    code: `T-${id}`,
    name: `Activity ${id}`,
    type,
    isSummaryTask: id === "a",
    duration: type === "Milestone" ? 0 : 2,
    startDate: "2026-02-02T08:00:00Z",
    endDate: "2026-02-04T17:00:00Z",
    progress: 35,
    wbsId: id === "a" ? "wbs-child" : undefined,
    critical: false,
    rowVersion: `rv-${id}`,
    ...options,
  };
}

function makeDependency(id: string, from: string, to: string, type: PlanDependencyDto["type"], lag = 0): PlanDependencyDto {
  return { id, planVersionId: "version-1", predecessorActivityId: from, activityId: to, type, lag, rowVersion: `rv-${id}` };
}

function fixture(): CriticalPathExportSnapshot {
  const activities = ["s", "a", "b", "q", "j"].map((id) => makeActivity(id));
  activities[5] = makeActivity("m", "Milestone", { name: "Completion milestone" });
  const dependencies = [
    makeDependency("d1", "s", "a", "FinishToStart"),
    makeDependency("d2", "s", "b", "StartToStart", 1),
    makeDependency("d3", "a", "j", "FinishToFinish", -2),
    makeDependency("d4", "b", "j", "StartToFinish", 3),
    makeDependency("d5", "j", "m", "FinishToStart"),
    makeDependency("d6", "s", "q", "StartToFinish", -1),
    makeDependency("d7", "q", "m", "FinishToFinish"),
  ];
  const criticalIds = new Set(["s", "a", "b", "j", "m"]);
  const analysis: PlanSchedulingAnalysisDto = {
    planVersionId: "version-1",
    hasCycle: false,
    projectFinishDate: "2026-02-04T17:00:00Z",
    criticalPathActivityIds: [...criticalIds],
    criticalPathDependencyIds: ["d1", "d2", "d3", "d4", "d5"],
    activities: activities.map((item) => ({ activityId: item.id, totalSlack: criticalIds.has(item.id) ? 0 : 3, critical: criticalIds.has(item.id) })),
  };
  const wbs: PlanWbsNodeDto[] = [{
    id: "wbs-root", planVersionId: "version-1", sortOrder: 1, code: "1", name: "Delivery", rowVersion: "wbs-rv",
    children: [{ id: "wbs-child", planVersionId: "version-1", parentWbsId: "wbs-root", sortOrder: 1, code: "1.2", name: "Build", rowVersion: "wbs-rv-2", children: [] }],
  }];
  const layout = [
    { activityId: "s", x: 0, y: 170, width: 216, height: 152 },
    { activityId: "a", x: 360, y: 0, width: 216, height: 152 },
    { activityId: "b", x: 360, y: 190, width: 216, height: 152 },
    { activityId: "q", x: 360, y: 380, width: 216, height: 152 },
    { activityId: "j", x: 720, y: 170, width: 216, height: 152 },
    { activityId: "m", x: 1080, y: 158, width: 208, height: 176 },
  ];
  return { projectId: "project-1", projectName: "Network Project", projectCode: "NP-01", planId: "plan-1", planName: "Execution Plan", planVersionId: "version-1", planVersionNumber: 4, activities, dependencies, wbs, layout, analysis, exportedAt: "2026-03-01T12:00:00Z" };
}

function options(overrides: Partial<CriticalPathExportOptions> = {}): CriticalPathExportOptions {
  return { ...DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS, ...overrides };
}

function prepare(snapshot = fixture(), selected = options()) {
  return prepareCriticalPathExport(snapshot, selected, labels);
}

describe("complete critical-path diagram export model", () => {
  it("uses the canonical critical graph and retains both parallel critical paths", () => {
    const model = prepare();
    expect(model.nodes).toHaveLength(6);
    expect(model.edges).toHaveLength(7);
    expect(model.criticalActivityCount).toBe(5);
    expect(model.criticalPathCount).toBe("2");
    expect(model.edges.filter((edge) => edge.critical)).toHaveLength(5);
    expect(model.nodes.find((node) => node.activity.id === "a")?.wbsPath).toBe("1 › 1.2");
    expect(model.nodes.find((node) => node.activity.id === "m")?.height).toBeGreaterThan(140);
    expect(model.nodes.find((node) => node.activity.id === "a")?.x).toBe(360);
    expect(model.nodes.find((node) => node.activity.id === "a")?.y).toBe(0);
    for (const edge of model.edges) expect(edge.source.x).toBeLessThan(edge.target.x);
  });

  it("Critical Path Only includes every canonical critical branch and milestone, excluding parallel non-critical work", () => {
    const model = prepare(fixture(), options({ scope: "critical" }));
    expect(model.nodes.map((node) => node.activity.id).sort()).toEqual(["a", "b", "j", "m", "s"]);
    expect(model.edges.map((edge) => edge.dependency.id).sort()).toEqual(["d1", "d2", "d3", "d4", "d5"]);
    expect(model.criticalPathCount).toBe("2");
  });

  it("SVG is standalone, complete, injection-safe, and includes all dependency types and lead/lag values", () => {
    const input = fixture();
    input.activities[0].name = "Plan & <Deliver> 'securely'";
    const svg = buildCriticalPathSvg(prepare(input));
    expect(svg.match(/data-activity-id=/g)).toHaveLength(6);
    expect(svg.match(/data-dependency-id=/g)).toHaveLength(7);
    expect(svg).toContain("Plan &amp; &lt;Deliver&gt; &apos;securely&apos;");
    expect(svg).not.toContain("<script>");
    expect(svg).not.toContain("foreignObject");
    expect(svg).not.toContain("<style");
    expect(svg).toContain("Finish to Start</text>");
    expect(svg).toContain("Start to Start +1 days");
    expect(svg).toContain("Finish to Finish −2 days");
    expect(svg).toContain("Start to Finish +3 days");
    expect(svg).toContain("−2 days");
    expect(svg).toContain("+3 days");
    expect(svg).toContain("data-kind=\"milestone\"");
    expect(svg).toContain('d="M216 246');
  });

  it("rejects incomplete, mixed-version, cyclic, and invalid canonical data without inventing a path", () => {
    const base = fixture();
    expect(() => prepare({ ...base, analysis: undefined })).toThrowError(CriticalPathExportError);
    expect(() => prepare({ ...base, analysis: { ...base.analysis!, hasCycle: true } })).toThrowError(CriticalPathExportError);
    expect(() => prepare({ ...base, activities: base.activities.map((item, index) => index === 2 ? { ...item, startDate: undefined } : item) })).toThrowError(CriticalPathExportError);
    expect(() => prepare({ ...base, activities: base.activities.map((item, index) => index === 0 ? { ...item, planVersionId: "version-old" } : item) })).toThrowError(CriticalPathExportError);
    expect(() => prepare({ ...base, dependencies: [...base.dependencies, makeDependency("orphan", "not-in-plan", "m", "FinishToStart")] })).toThrowError(CriticalPathExportError);
    expect(() => prepare({ ...base, analysis: { ...base.analysis!, criticalPathActivityIds: [], criticalPathDependencyIds: [], activities: base.analysis!.activities.map((item) => ({ ...item, critical: false })) } })).toThrowError(CriticalPathExportError);
  });

  it("fit PDF contains the complete graph and rejects a page that would make the diagram unreadable", () => {
    const fitModel = prepare(fixture(), options({ format: "pdf", pdfLayout: "fit", pageSize: "A1" }));
    const fitPlan = buildCriticalPathPdfDocumentPlan(fitModel);
    expect(fitPlan.pageCount).toBe(1);
    const svg = String((fitPlan.definition.content as Array<{ svg: string }>)[0].svg);
    expect(svg.match(/data-activity-id=/g)).toHaveLength(6);
    expect(svg.match(/data-dependency-id=/g)).toHaveLength(7);

    const longChain = makeChain(30);
    const largeModel = prepare(longChain, options({ format: "pdf", pdfLayout: "fit", pageSize: "A4" }));
    expect(() => buildCriticalPathPdfDocumentPlan(largeModel)).toThrowError(CriticalPathExportError);
  });

  it("automatically selects A4–A0 or a custom fit size with the requested orientation", () => {
    const small = buildCriticalPathPdfDocumentPlan(prepare(fixture(), options({ format: "pdf", pdfLayout: "fit", pageSize: "AUTO", orientation: "auto" })));
    expect(small.definition.pageSize).toBe("A3");
    expect(small.pageCount).toBe(1);
    const panoramic = buildCriticalPathPdfDocumentPlan(prepare(makeChain(10), options({ format: "pdf", pdfLayout: "fit", pageSize: "AUTO", orientation: "auto" })));
    expect(panoramic.definition.pageSize).toMatchObject({ width: expect.any(Number), height: expect.any(Number) });
    const model = prepare(makeChain(10), options({ format: "pdf", pdfLayout: "fit", pageSize: "CUSTOM", orientation: "landscape" }));
    const plan = buildCriticalPathPdfDocumentPlan(model);
    const pageSize = plan.definition.pageSize as { width: number; height: number };
    expect(pageSize.width).toBeGreaterThan(2_383);
    expect(pageSize.width).toBeGreaterThanOrEqual(pageSize.height);
    expect(plan.pageCount).toBe(1);
  });

  it("multi-page PDF tiles the original layout with overlap and retains every connected edge", () => {
    const snapshot = makeChain(37);
    const model = prepare(snapshot, options({ format: "pdf", pageSize: "A4", orientation: "landscape", pdfLayout: "detailed" }));
    const plan = buildCriticalPathPdfDocumentPlan(model);
    const pages = plan.definition.content as Array<{ svg: string; pageBreak?: string }>;
    const allSvg = pages.map((page) => page.svg).join("\n");
    expect(plan.pageCount).toBeGreaterThan(4);
    expect(pages).toHaveLength(plan.pageCount);
    const activityIds = [...allSvg.matchAll(/data-activity-id="([^"]+)"/g)].map((match) => match[1]);
    const dependencyIds = [...allSvg.matchAll(/data-dependency-id="([^"]+)"/g)].map((match) => match[1]);
    expect(new Set(activityIds).size).toBe(snapshot.activities.length);
    expect(new Set(dependencyIds).size).toBe(snapshot.dependencies.length);
    expect(plan.nodePages.size).toBe(snapshot.activities.length);
    expect(plan.crossPageConnections.length).toBeGreaterThan(0);
    expect(plan.crossPageConnections.every((item) => item.sourcePage !== item.targetPage)).toBe(true);
    expect(allSvg).toContain("Tile");
    expect(allSvg).toContain("viewBox=\"");
  });

  it("retains 200-plus activities in original-layout tiles with the same two-line card title treatment", () => {
    const snapshot = makeChain(225);
    snapshot.activities[150].name = "An unusually long activity name that is fully wrapped across several lines without clipping";
    const model = prepare(snapshot, options({ pdfLayout: "detailed", pageSize: "A2" }));
    const plan = buildCriticalPathPdfDocumentPlan(model);
    const pages = plan.definition.content as Array<{ svg: string }>;
    const allSvg = pages.map((page) => page.svg).join("\n");
    expect(new Set([...allSvg.matchAll(/data-activity-id="([^"]+)"/g)].map((match) => match[1])).size).toBe(225);
    expect(new Set([...allSvg.matchAll(/data-dependency-id="([^"]+)"/g)].map((match) => match[1])).size).toBe(224);
    expect(allSvg).toContain("An unusually");
    expect(allSvg).toContain("…");
    expect(plan.pageCount).toBeLessThan(250);
  });

  it.each([20, 100])("lays out every saved node and dependency for a %i-activity network", (count) => {
    const snapshot = makeChain(count);
    const model = prepare(snapshot, options({ scope: "full", format: "svg" }));
    const svg = buildCriticalPathSvg(model);
    expect((svg.match(/data-activity-id=/g) ?? []).length).toBe(count);
    expect((svg.match(/data-dependency-id=/g) ?? []).length).toBe(count - 1);
  });

  it("keeps Arabic labels RTL while preserving the left-to-right dependency layout", async () => {
    const arabicLabels: CriticalPathExportLabels = {
      ...labels,
      title: "مخطط شبكة المسار الحرج",
      direction: "rtl",
      project: "المشروع",
      plan: "الخطة",
      workingVersion: "نسخة العمل",
      exportedAt: "تاريخ التصدير",
      taskId: "معرّف المهمة",
      wbs: "هيكل تقسيم العمل",
      critical: "حرج",
      nonCritical: "غير حرج",
      dependencyType: { FinishToFinish: "انتهاء إلى انتهاء", StartToStart: "بدء إلى بدء", FinishToStart: "انتهاء إلى بدء", StartToFinish: "بدء إلى انتهاء" },
    };
    const model = prepareCriticalPathExport(fixture(), options({ format: "pdf" }), arabicLabels);
    expect(buildCriticalPathSvg(model)).toContain('direction="rtl"');
    expect(model.nodes.find((node) => node.activity.id === "s")!.x).toBeLessThan(model.nodes.find((node) => node.activity.id === "a")!.x);
    const blob = await buildCriticalPathPdfBlob(model);
    expect(blob.size).toBeGreaterThan(500);
  });

  it("creates a real vector PDF blob from the pinned PDF library", async () => {
    const model = prepare(fixture(), options({ format: "pdf", pageSize: "A3", pdfLayout: "detailed" }));
    const blob = await buildCriticalPathPdfBlob(model);
    expect(blob.type).toMatch(/pdf/i);
    expect(blob.size).toBeGreaterThan(500);
    const bytes = await new Promise<ArrayBuffer>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(blob);
    });
    expect(new Uint8Array(bytes).slice(0, 4)).toEqual(new Uint8Array([0x25, 0x50, 0x44, 0x46]));
  });
});

describe("PNG export limits and rendering", () => {
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); });

  it("blocks oversized 200-node canvases and reports the configured hard limits", async () => {
    const model = prepare(makeChain(225), options({ format: "png", pngScale: 3 }));
    await expect(downloadCriticalPathPng(model)).rejects.toMatchObject({ code: "pngTooLarge" });
    expect(getCriticalPathPngLimits()).toEqual({ maxSide: 16_384, maxPixels: 40_000_000 });
  });

  it("renders and downloads a complete PNG at the requested scale", async () => {
    vi.useFakeTimers();
    const createdUrls: string[] = [];
    const revokedUrls: string[] = [];
    vi.stubGlobal("Image", class {
      onload: ((event: Event) => void) | null = null;
      onerror: ((event: Event) => void) | null = null;
      set src(_value: string) { queueMicrotask(() => this.onload?.(new Event("load"))); }
    });
    vi.stubGlobal("URL", {
      createObjectURL: vi.fn(() => { const url = `blob:diagram-${createdUrls.length + 1}`; createdUrls.push(url); return url; }),
      revokeObjectURL: vi.fn((url: string) => revokedUrls.push(url)),
    });
    const context = { fillStyle: "", fillRect: vi.fn(), drawImage: vi.fn() };
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(context as unknown as CanvasRenderingContext2D);
    vi.spyOn(HTMLCanvasElement.prototype, "toBlob").mockImplementation((callback) => callback(new Blob(["png"], { type: "image/png" })));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
    const model = prepare(fixture(), options({ format: "png", pngScale: 2 }));
    await downloadCriticalPathPng(model);
    await vi.runAllTimersAsync();
    expect(context.drawImage).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(createdUrls).toHaveLength(2);
    expect(revokedUrls).toContain(createdUrls[0]);
    click.mockRestore();
  });
});

function makeChain(count: number): CriticalPathExportSnapshot {
  const activities = Array.from({ length: count }, (_, index) => makeActivity(`chain-${String(index).padStart(3, "0")}`, index === count - 1 ? "Milestone" : "Task", { code: `C-${String(index + 1).padStart(3, "0")}`, name: `Chain activity ${index + 1}`, isSummaryTask: index % 12 === 0 }));
  const dependencies = activities.slice(1).map((target, index) => makeDependency(`chain-link-${index + 1}`, activities[index].id, target.id, index % 4 === 0 ? "FinishToStart" : index % 4 === 1 ? "StartToStart" : index % 4 === 2 ? "FinishToFinish" : "StartToFinish", index % 3 === 0 ? -1 : 1));
  const ids = activities.map((item) => item.id);
  const analysis: PlanSchedulingAnalysisDto = {
    planVersionId: "version-1", hasCycle: false, projectFinishDate: "2026-12-31T17:00:00Z",
    criticalPathActivityIds: ids, criticalPathDependencyIds: dependencies.map((item) => item.id),
    activities: activities.map((item) => ({ activityId: item.id, totalSlack: 0, critical: true })),
  };
  const layout = activities.map((activity, index) => ({ activityId: activity.id, x: index * 360, y: 0, width: activity.type === "Milestone" ? 208 : 216, height: activity.type === "Milestone" ? 176 : 152 }));
  return { projectId: "project-1", projectName: "Large Network", projectCode: "LN", planId: "plan-1", planName: "Large Plan", planVersionId: "version-1", planVersionNumber: 2, activities, dependencies, wbs: [], layout, analysis, exportedAt: "2026-12-01T00:00:00Z" };
}
