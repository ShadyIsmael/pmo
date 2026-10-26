import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";
import { readFileSync } from "node:fs";
const brand = JSON.parse(readFileSync(new URL("./brand.json", import.meta.url), "utf8"));

export default defineConfig({
  plugins: [react(), {
    name: "fopx-brand-metadata",
    transformIndexHtml(html) {
      return html.split("%BRAND_PRODUCT%").join(brand.product.name)
        .split("%BRAND_DESCRIPTION%").join(brand.product.description)
        .split("%BRAND_FAVICON%").join(brand.product.favicon);
    },
  }],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
      "@features": path.resolve(__dirname, "./src/features"),
      "@components": path.resolve(__dirname, "./src/components"),
      "@hooks": path.resolve(__dirname, "./src/hooks"),
      "@api": path.resolve(__dirname, "./src/api"),
      "@lib": path.resolve(__dirname, "./src/lib"),
      "@styles": path.resolve(__dirname, "./src/styles"),
      "@i18n": path.resolve(__dirname, "./src/i18n"),
      "@auth": path.resolve(__dirname, "./src/auth"),
      "@theme": path.resolve(__dirname, "./src/theme"),
    },
  },
  server: {
    port: 4100,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ["react", "react-dom", "react-router-dom", "@tanstack/react-query"],
          ui: ["lucide-react", "recharts", "@dnd-kit/core", "@dnd-kit/sortable", "@dnd-kit/utilities"],
          forms: ["react-hook-form", "zod"],
        },
      },
    },
  },
});