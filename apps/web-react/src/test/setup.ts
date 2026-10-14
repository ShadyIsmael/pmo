import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeAll } from "vitest";

beforeAll(() => {
  if (typeof globalThis.ResizeObserver === "undefined") {
    globalThis.ResizeObserver = class TestResizeObserver implements ResizeObserver {
      private readonly callback: ResizeObserverCallback;

      constructor(callback: ResizeObserverCallback) {
        this.callback = callback;
      }

      observe(target: Element) {
        const width = target.getBoundingClientRect().width || 640;
        const height = target.getBoundingClientRect().height || 360;
        const contentRect = { x: 0, y: 0, top: 0, right: width, bottom: height, left: 0, width, height, toJSON: () => ({}) } as DOMRectReadOnly;
        this.callback([{ target, contentRect } as ResizeObserverEntry], this);
      }

      unobserve() {}
      disconnect() {}
    };
  }

  if (typeof window !== "undefined" && typeof window.matchMedia !== "function") {
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false,
      }),
    });
  }
});

afterEach(() => { cleanup(); window.localStorage.clear(); document.documentElement.className = ""; document.documentElement.removeAttribute("data-theme"); document.documentElement.dir = "ltr"; });
