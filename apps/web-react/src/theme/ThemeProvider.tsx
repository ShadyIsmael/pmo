import * as React from "react";

export type Theme = "light" | "dark" | "system";
interface ThemeContextValue { theme: Theme; resolvedTheme: "light" | "dark"; setTheme: (theme: Theme) => void; toggleTheme: () => void; }
const ThemeContext = React.createContext<ThemeContextValue | null>(null);

function getSystemTheme(): "light" | "dark" {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = React.useState<Theme>(() => {
    const saved = typeof window === "undefined" ? null : window.localStorage.getItem("nexus-theme");
    return saved === "dark" || saved === "light" || saved === "system" ? saved : "system";
  });
  const [systemTheme, setSystemTheme] = React.useState<"light" | "dark">(() => getSystemTheme());
  const resolvedTheme = theme === "system" ? systemTheme : theme;

  React.useEffect(() => {
    if (typeof window === "undefined" || typeof window.matchMedia !== "function") return;
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = (e: MediaQueryListEvent) => setSystemTheme(e.matches ? "dark" : "light");
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  React.useEffect(() => {
    document.documentElement.classList.toggle("dark", resolvedTheme === "dark");
    document.documentElement.dataset.theme = resolvedTheme;
    window.localStorage.setItem("nexus-theme", theme);
  }, [theme, resolvedTheme]);

  const setTheme = React.useCallback((next: Theme) => setThemeState(next), []);
  const toggleTheme = React.useCallback(() => {
    setThemeState((current) => {
      const resolved = current === "system" ? getSystemTheme() : current;
      return resolved === "dark" ? "light" : "dark";
    });
  }, []);

  const value = React.useMemo(() => ({ theme, resolvedTheme, setTheme, toggleTheme }), [theme, resolvedTheme, setTheme, toggleTheme]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const value = React.useContext(ThemeContext);
  if (!value) throw new Error("useTheme must be used within ThemeProvider");
  return value;
}
