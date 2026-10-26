/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ["class"],
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    container: { center: true, padding: "1.5rem", screens: { "2xl": "1400px" } },
    extend: {
      fontFamily: {
        planning: ["Inter", "Segoe UI", "Tahoma", "Arial", "sans-serif"],
      },
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        header: "hsl(var(--header))",
        primary: { DEFAULT: "hsl(var(--primary))", foreground: "hsl(var(--primary-foreground))", hover: "hsl(var(--primary-hover))", light: "hsl(var(--primary-light))" },
        secondary: { DEFAULT: "hsl(var(--secondary))", foreground: "hsl(var(--secondary-foreground))" },
        destructive: { DEFAULT: "hsl(var(--destructive))", foreground: "hsl(var(--destructive-foreground))", light: "hsl(var(--destructive-light))" },
        muted: { DEFAULT: "hsl(var(--muted))", foreground: "hsl(var(--muted-foreground))" },
        accent: { DEFAULT: "hsl(var(--accent))", foreground: "hsl(var(--accent-foreground))" },
        popover: { DEFAULT: "hsl(var(--popover))", foreground: "hsl(var(--popover-foreground))" },
        card: { DEFAULT: "hsl(var(--card))", foreground: "hsl(var(--card-foreground))" },
        success: { DEFAULT: "hsl(var(--success))", foreground: "hsl(var(--success-foreground))", light: "hsl(var(--success-light))" },
        warning: { DEFAULT: "hsl(var(--warning))", foreground: "hsl(var(--warning-foreground))", light: "hsl(var(--warning-light))" },
        danger: { DEFAULT: "hsl(var(--danger))", foreground: "hsl(var(--danger-foreground))", light: "hsl(var(--danger-subtle))" },
        info: { DEFAULT: "hsl(var(--info))", light: "hsl(var(--info-light))" },
        sidebar: { DEFAULT: "hsl(var(--sidebar-background))", foreground: "hsl(var(--sidebar-foreground))", accent: "hsl(var(--sidebar-accent))", active: "hsl(var(--sidebar-active))", "active-subtle": "hsl(var(--sidebar-active-subtle))", border: "hsl(var(--sidebar-border))", hover: "hsl(var(--sidebar-hover))", muted: "hsl(var(--sidebar-muted))", ring: "hsl(var(--sidebar-ring))" },
        chart: { 1: "hsl(var(--chart-1))", 2: "hsl(var(--chart-2))", 3: "hsl(var(--chart-3))", 4: "hsl(var(--chart-4))", 5: "hsl(var(--chart-5))", 6: "hsl(var(--chart-6))", 7: "hsl(var(--chart-7))" },
        fin: { navy: "hsl(var(--fin-navy))", indigo: "hsl(var(--fin-indigo))", blue: "hsl(var(--fin-blue))", amber: "hsl(var(--fin-amber))", violet: "hsl(var(--fin-violet))", green: "hsl(var(--fin-green))", red: "hsl(var(--fin-red))", slate: "hsl(var(--fin-slate))" },
        financial: { total: "hsl(var(--financial-total))" },
      },
      borderRadius: { lg: "var(--radius)", md: "calc(var(--radius) - 2px)", sm: "calc(var(--radius) - 4px)" },
      keyframes: {
        "accordion-down": { from: { height: "0" }, to: { height: "var(--radix-accordion-content-height)" } },
        "accordion-up": { from: { height: "var(--radix-accordion-content-height)" }, to: { height: "0" } },
      },
      animation: { "accordion-down": "accordion-down 0.2s ease-out", "accordion-up": "accordion-up 0.2s ease-out" },
    },
  },
  plugins: [],
};
