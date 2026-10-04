import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

afterEach(() => { cleanup(); window.localStorage.clear(); document.documentElement.className = ""; document.documentElement.removeAttribute("data-theme"); document.documentElement.dir = "ltr"; });
