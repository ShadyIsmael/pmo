import * as React from "react";
import { Sidebar } from "./Sidebar";
import { Header } from "./Header";

export function AppShell({ children }: { children: React.ReactNode }) { return <div className="app-shell"><Sidebar /><div className="app-column"><Header /><main className="main-content">{children}</main></div></div>; }
