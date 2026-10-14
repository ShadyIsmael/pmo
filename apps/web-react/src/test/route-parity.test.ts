import fs from "node:fs";
import path from "node:path";
import * as ts from "typescript";
import { describe, expect, it } from "vitest";

const webRoot = process.cwd();
const nextAppRoot = path.resolve(webRoot, "../web/src/app");
const reactRoutesFile = path.resolve(webRoot, "src/routes/AppRoutes.tsx");
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
        childParent = value.startsWith("/") ? value : `${parent.replace(/\/$/, "")}/${value}`;
        if (!childParent.startsWith("/")) childParent = `/${childParent}`;
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

function nextPageFiles(directory: string): string[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) return nextPageFiles(file);
    return entry.name === "page.tsx" ? [file] : [];
  });
}

function normalizeNextRoute(file: string) {
  const segments = path.relative(nextAppRoot, file).split(path.sep)
    .filter((segment) => segment !== "[locale]" && segment !== "(dashboard)")
    .slice(0, -1)
    .map((segment) => segment
      .replace(/^\[\.\.\.(.+)\]$/, "*")
      .replace(/^\[\[(?:\.\.\.)?(.+)\]\]$/, "*")
      .replace(/^\[(.+)\]$/, ":$1"));
  return `/${segments.join("/")}`;
}

function routeRegex(pattern: string) {
  if (pattern === "/") return /^\/$/;
  const parts = pattern.split("/").filter(Boolean).map((part) => {
    if (part === "*") return ".*";
    if (part.startsWith(":")) return "[^/]+";
    return part.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  });
  return new RegExp(`^/${parts.join("/")}/?$`);
}

const reactRoutes = collectReactRoutePatterns(routeSource);
const nextRoutes = nextPageFiles(nextAppRoot).map((file) => ({ file, route: normalizeNextRoute(file) }));
const matchedRoutes = nextRoutes.map((entry) => ({ ...entry, reactPattern: reactRoutes.find(({ pattern }) => routeRegex(pattern).test(entry.route)) }));

describe("Next.js to React route inventory", () => {
  it("maps every Next page route to an explicit React route, redirect, or scoped wildcard", () => {
    expect(nextRoutes.length).toBeGreaterThan(80);
    expect(matchedRoutes.filter((entry) => !entry.reactPattern).map((entry) => entry.route)).toEqual([]);
    expect(matchedRoutes.filter((entry) => !entry.reactPattern?.hasElement).map((entry) => entry.route)).toEqual([]);
  });

  it("keeps wildcard coverage limited to the four report subsections", () => {
    const wildcardMatches = matchedRoutes.filter((entry) => entry.reactPattern?.pattern.includes("*")).map((entry) => entry.route).sort();
    expect(wildcardMatches).toEqual(["/reports/custom", "/reports/executive", "/reports/portfolio", "/reports/scheduled"]);
  });
});
