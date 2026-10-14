import fs from "node:fs";
import path from "node:path";
import * as ts from "typescript";
import { describe, expect, it } from "vitest";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import {
  DASHBOARD_WIDGET_CATEGORIES,
  DASHBOARD_WIDGETS_WITH_RECORD_LIMIT,
  DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT,
  dashboardWidgetCategoryTranslationKeys,
  dashboardWidgetHasRecordLimit,
  dashboardWidgetRecordLimitOrDefault,
  dashboardWidgetRecordLimitFor,
  dashboardWidgetPovs,
  filterAvailableDashboardWidgets,
  type DashboardWidgetAvailabilityDefinition,
} from "../features/home/dashboard-widget-availability";
import { dashboardWidgetDefinitions, shouldLoadDashboardHomeSummary, unavailableWidgetTypes, type WidgetType } from "../features/home/DashboardBuilder";
import { Permissions } from "../lib/permissions";

function hasMessage(messages: unknown, key: string): boolean {
  const value = key.split(".").reduce<unknown>((current, part) => {
    if (!current || typeof current !== "object" || !(part in current)) return undefined;
    return (current as Record<string, unknown>)[part];
  }, messages);
  return typeof value === "string";
}

interface SourceWidgetMetadata {
  category: string;
  supportedPOVs: string[];
}

function unwrapExpression(node: ts.Expression): ts.Expression {
  let current = node;
  while (ts.isAsExpression(current) || ts.isSatisfiesExpression(current) || ts.isParenthesizedExpression(current)) {
    current = current.expression;
  }
  return current;
}

function stringLiteral(node: ts.Expression | undefined): string | undefined {
  if (!node) return undefined;
  const expression = unwrapExpression(node);
  return ts.isStringLiteralLike(expression) ? expression.text : undefined;
}

function registrationLoop(call: ts.CallExpression): { bindings: ts.ArrayBindingPattern; tuples: ts.ArrayLiteralExpression } | undefined {
  let parent: ts.Node | undefined = call.parent;
  while (parent && !ts.isForOfStatement(parent)) parent = parent.parent;
  if (!parent || !ts.isVariableDeclarationList(parent.initializer)) return undefined;

  const declaration = parent.initializer.declarations[0];
  if (!declaration || !ts.isArrayBindingPattern(declaration.name)) return undefined;

  const tuples = unwrapExpression(parent.expression);
  return ts.isArrayLiteralExpression(tuples) ? { bindings: declaration.name, tuples } : undefined;
}

function bindingIndex(bindings: ts.ArrayBindingPattern, name: string): number {
  return bindings.elements.findIndex((element) =>
    ts.isBindingElement(element) && ts.isIdentifier(element.name) && element.name.text === name,
  );
}

function widgetIdsForRegistration(typeArgument: ts.Expression, call: ts.CallExpression): string[] {
  const directId = stringLiteral(typeArgument);
  if (directId) return [directId];
  if (!ts.isIdentifier(typeArgument)) return [];

  const loop = registrationLoop(call);
  if (!loop) return [];
  const index = bindingIndex(loop.bindings, typeArgument.text);
  if (index < 0) return [];

  return loop.tuples.elements.flatMap((element) => {
    const tuple = unwrapExpression(element as ts.Expression);
    if (!ts.isArrayLiteralExpression(tuple)) return [];
    const id = stringLiteral(tuple.elements[index] as ts.Expression | undefined);
    return id ? [id] : [];
  });
}

function loopValueForWidget(typeArgument: ts.Expression, call: ts.CallExpression, name: string, widgetId: string): string | undefined {
  if (!ts.isIdentifier(typeArgument)) return undefined;
  const loop = registrationLoop(call);
  if (!loop) return undefined;
  const typeIndex = bindingIndex(loop.bindings, typeArgument.text);
  const valueIndex = bindingIndex(loop.bindings, name);
  if (typeIndex < 0 || valueIndex < 0) return undefined;

  for (const element of loop.tuples.elements) {
    const tuple = unwrapExpression(element as ts.Expression);
    if (!ts.isArrayLiteralExpression(tuple) || stringLiteral(tuple.elements[typeIndex] as ts.Expression | undefined) !== widgetId) continue;
    return stringLiteral(tuple.elements[valueIndex] as ts.Expression | undefined);
  }
  return undefined;
}

function nextDashboardWidgetMetadata(): Record<string, SourceWidgetMetadata> {
  const widgetsDirectory = path.resolve(process.cwd(), "../web/src/components/dashboard-builder/widgets");
  const result: Record<string, SourceWidgetMetadata> = {};
  const files = fs.readdirSync(widgetsDirectory).filter((file) => file.endsWith(".tsx"));

  for (const file of files) {
    const filePath = path.join(widgetsDirectory, file);
    const source = ts.createSourceFile(filePath, fs.readFileSync(filePath, "utf8"), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
    const visit = (node: ts.Node) => {
      if (ts.isCallExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === "registerWidget") {
        const [typeArgument, definitionArgument] = node.arguments;
        if (typeArgument && definitionArgument && ts.isObjectLiteralExpression(unwrapExpression(definitionArgument))) {
          const definition = unwrapExpression(definitionArgument) as ts.ObjectLiteralExpression;
          const categoryProperty = definition.properties.find((property) =>
            ts.isPropertyAssignment(property) && property.name.getText(source).replace(/["']/g, "") === "category",
          );
          const povProperty = definition.properties.find((property) =>
            ts.isPropertyAssignment(property) && property.name.getText(source).replace(/["']/g, "") === "supportedPOVs",
          );
          const ids = widgetIdsForRegistration(typeArgument, node);
          const category = categoryProperty && ts.isPropertyAssignment(categoryProperty)
            ? stringLiteral(categoryProperty.initializer)
            : undefined;
          const povExpression = povProperty && ts.isPropertyAssignment(povProperty)
            ? unwrapExpression(povProperty.initializer)
            : undefined;
          const supportedPOVs = povExpression && ts.isArrayLiteralExpression(povExpression)
            ? povExpression.elements.map((pov) => stringLiteral(pov as ts.Expression)).filter((pov): pov is string => Boolean(pov))
            : [];

          if (supportedPOVs.length > 0) {
            for (const id of ids) {
              const resolvedCategory = category ?? loopValueForWidget(typeArgument, node, "category", id);
              if (resolvedCategory) result[id] = { category: resolvedCategory, supportedPOVs };
            }
          }
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(source);
  }
  return result;
}

describe("dashboard widget availability parity", () => {
  it("filters the catalog by the active point of view and omits React-only widgets", () => {
    const projectManagerWidgets = filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []);
    const projectManagerTypes = projectManagerWidgets.map(({ type }) => type);
    expect(projectManagerTypes).toContain("projects-attention");
    expect(projectManagerTypes).not.toContain("projects-by-status");
    expect(projectManagerTypes).not.toContain("jira-sync");
    expect(projectManagerTypes).not.toContain("critical-risks");
    expect(projectManagerTypes).not.toContain("my-work");

    const programManagerTypes = filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Program Manager", []).map(({ type }) => type);
    expect(programManagerTypes).toContain("projects-by-status");
  });

  it("honors required permissions and hides widgets whose plugin is not connected", () => {
    const definitions: DashboardWidgetAvailabilityDefinition[] = [
      { type: "open", supportedPOVs: ["Project Manager"] },
      { type: "permissioned", supportedPOVs: ["Project Manager"], requiredPermission: "Reports.Export" },
      { type: "plugin", supportedPOVs: ["Project Manager"], requiredPlugin: "jira" },
    ];

    expect(filterAvailableDashboardWidgets(definitions, "Project Manager", []).map(({ type }) => type)).toEqual(["open"]);
    expect(filterAvailableDashboardWidgets(definitions, "Project Manager", ["Reports.Export"]).map(({ type }) => type)).toEqual(["open", "permissioned"]);
  });

  it("requires one valid time-tracking permission before offering Hours This Week", () => {
    const widget = dashboardWidgetDefinitions.find(({ type }) => type === "hours-this-week");
    expect(widget?.requiredAnyPermissions).toEqual(Object.values(Permissions.TimeTracking));
    expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []).map(({ type }) => type)).not.toContain("hours-this-week");
    for (const permission of Object.values(Permissions.TimeTracking)) {
      expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", [permission]).map(({ type }) => type)).toContain("hours-this-week");
    }
  });

  it("offers Pending Approvals only to users allowed to approve at least one included request type", () => {
    const widget = dashboardWidgetDefinitions.find(({ type }) => type === "pending-approvals");
    const requiredPermissions = [Permissions.Handover.Approve, Permissions.ChangeRequest.Approve];
    expect(widget?.requiredAnyPermissions).toEqual(requiredPermissions);
    expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []).map(({ type }) => type)).not.toContain("pending-approvals");
    for (const permission of requiredPermissions) {
      expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", [permission]).map(({ type }) => type)).toContain("pending-approvals");
    }
  });

  it("does not fetch Home summary data for widgets whose source data is unavailable", () => {
    for (const type of unavailableWidgetTypes) {
      expect(shouldLoadDashboardHomeSummary(type), `${type} should not request the Home summary`).toBe(false);
    }
    expect(shouldLoadDashboardHomeSummary("active-projects")).toBe(true);
    expect(shouldLoadDashboardHomeSummary("hours-this-week")).toBe(false);
    expect(shouldLoadDashboardHomeSummary("pending-approvals")).toBe(false);
  });

  it("matches actual Next widget IDs, categories, and POVs, excluding unsupported React-only additions", () => {
    const nextWidgets = nextDashboardWidgetMetadata();
    const reactByType = new Map(dashboardWidgetDefinitions.map((definition) => [definition.type, definition]));
    const reactOnlyTypes: WidgetType[] = ["critical-risks", "my-work"];

    expect(Object.keys(nextWidgets).length).toBeGreaterThan(0);
    expect(Object.keys(nextWidgets).sort()).toEqual(
      dashboardWidgetDefinitions.map(({ type }) => type).filter((type) => !reactOnlyTypes.includes(type)).sort(),
    );

    for (const [type, source] of Object.entries(nextWidgets)) {
      const react = reactByType.get(type as WidgetType);
      expect(react, `missing React definition for Next widget ${type}`).toBeDefined();
      expect(react?.category, `category mismatch for ${type}`).toBe(source.category);
      expect(dashboardWidgetPovs[type], `missing copied POV metadata for ${type}`).toEqual(source.supportedPOVs);
      expect(react?.supportedPOVs, `supported POV mismatch for ${type}`).toEqual(source.supportedPOVs);
    }

    for (const type of reactOnlyTypes) {
      expect(reactByType.get(type)?.supportedPOVs, `${type} must remain unavailable without a Next source`).toEqual([]);
    }
  });

  it("matches Next widget settings for configurable record counts", () => {
    expect(DASHBOARD_WIDGETS_WITH_RECORD_LIMIT).toEqual([
      "projects-attention",
      "my-todo",
      "upcoming-milestones",
      "top-risks",
      "upcoming-meetings",
      "pending-approvals",
      "recent-notifications",
    ]);
    expect(DASHBOARD_WIDGETS_WITH_RECORD_LIMIT.every((type) => dashboardWidgetHasRecordLimit(type))).toBe(true);
    expect(dashboardWidgetHasRecordLimit("projects-by-health")).toBe(false);
    expect(dashboardWidgetHasRecordLimit("overdue-tasks")).toBe(false);
    expect(DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(undefined)).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(undefined, 8)).toBe(8);
    expect(dashboardWidgetRecordLimitOrDefault(0)).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(4)).toBe(4);
    expect(dashboardWidgetRecordLimitFor("upcoming-meetings", undefined)).toBe(8);
    expect(dashboardWidgetRecordLimitFor("upcoming-meetings", 4)).toBe(4);
    expect(dashboardWidgetRecordLimitFor("projects-attention", undefined)).toBe(10);
  });

  it("matches the Next category order and resolves every dynamic widget label in EN and AR", () => {
    expect(DASHBOARD_WIDGET_CATEGORIES).toEqual([
      "Overview", "Projects", "Portfolios", "My Work", "Schedule", "Resources", "Timesheets",
      "Costs", "Risks", "Meetings", "Approvals", "Notifications", "Integrations", "AI",
    ]);
    expect(dashboardWidgetDefinitions.find(({ type }) => type === "projects-at-risk")?.category).toBe("Overview");
    expect(dashboardWidgetDefinitions.find(({ type }) => type === "budget-vs-actual")?.category).toBe("Costs");

    const dynamicKeys = [
      ...Object.values(dashboardWidgetCategoryTranslationKeys),
      ...dashboardWidgetDefinitions.flatMap(({ titleKey, descriptionKey }) => [titleKey, descriptionKey]),
    ];
    for (const key of dynamicKeys) {
      expect(hasMessage(enMessages, key), `missing EN widget-library message: ${key}`).toBe(true);
      expect(hasMessage(arMessages, key), `missing AR widget-library message: ${key}`).toBe(true);
    }
  });
});
