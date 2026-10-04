import * as React from "react";

type Theme = "light" | "dark";
interface ThemeContextValue { theme: Theme; toggleTheme: () => void; }
const ThemeContext = React.createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setTheme] = React.useState<Theme>(() => window.localStorage.getItem("nexus-react-theme") === "dark" ? "dark" : "light");
  React.useEffect(() => { document.documentElement.classList.toggle("dark", theme === "dark"); document.documentElement.dataset.theme = theme; window.localStorage.setItem("nexus-react-theme", theme); }, [theme]);
  return <ThemeContext.Provider value={{ theme, toggleTheme: () => setTheme((current) => current === "dark" ? "light" : "dark") }}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue { const value = React.useContext(ThemeContext); if (!value) throw new Error("useTheme must be used within ThemeProvider"); return value; }
