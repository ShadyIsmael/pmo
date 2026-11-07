import fs from "node:fs";
import path from "node:path";
import * as ts from "typescript";
import { describe, expect, it } from "vitest";

const reactRoutesFile = path.resolve(process.cwd(), "src/routes/AppRoutes.tsx");
const routeSource = ts.createSourceFile(reactRoutesFile, fs.readFileSync(reactRoutesFile, "utf8"), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);

interface ReactRoutePattern { pattern: string; hasElement: boolean }

function collectReactRoutePatterns(node: ts.Node, parent = "", result: ReactRoutePattern[] = []) {
  if (ts.isJsxElement(node) || ts.isJsxSelfClosingElement(node)) {
    const opening = ts.isJsxElement(node) ? node.openingElement : node;
    let childParent = parent;
    if (opening.tagName.getText(routeSource) === "Route") {
      const pathAttribute = opening.attributes.properties.find((attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(routeSource) === "path");
      const value = pathAttribute && ts.isJsxAttribute(pathAttribute) && pathAttribute.initializer && ts.isStringLiteral(pathAttribute.initializer) ? pathAttribute.initializer.text : undefined;
      if (value !== undefined) {
        childParent = value.startsWith("/") ? value : parent.replace(/\/$/, "") + "/" + value;
        if (!childParent.startsWith("/")) childParent = "/" + childParent;
        const hasElement = opening.attributes.properties.some((attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(routeSource) === "element");
        result.push({ pattern: childParent || "/", hasElement });
      }
    }
    if (ts.isJsxElement(node)) node.children.forEach((child) => { collectReactRoutePatterns(child, childParent, result); });
    return result;
  }
  ts.forEachChild(node, (child) => { collectReactRoutePatterns(child, parent, result); });
  return result;
}

const reactRoutes = collectReactRoutePatterns(routeSource);

describe("React Router route inventory", () => {
  it("keeps an explicit, mounted entry for the application routes", () => {
    expect(reactRoutes.length).toBeGreaterThan(80);
    expect(reactRoutes.filter(({ hasElement }) => !hasElement)).toEqual([]);
  });

  it("keeps catch-all routes scoped to their feature areas with a root fallback", () => {
    const wildcardRoutes = reactRoutes.filter(({ pattern }) => pattern.includes("*")).map(({ pattern }) => pattern).sort();
    expect(wildcardRoutes).toEqual(["/*", "/ai-workspace/*", "/reports/*", "/security-audit/*"]);
    expect(reactRoutes.some(({ pattern }) => pattern === "/")).toBe(true);
  });
});
