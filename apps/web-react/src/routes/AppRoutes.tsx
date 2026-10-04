import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { RequireAuth } from "../auth/RequireAuth";
import { HomePage } from "../features/home/HomePage";
import { LoginPage } from "../features/home/LoginPage";

export function AppRoutes() { return <Routes><Route path="/" element={<Navigate to="/home" replace />} /><Route path="/login" element={<LoginPage />} /><Route path="/home" element={<RequireAuth><AppShell><HomePage /></AppShell></RequireAuth>} /><Route path="*" element={<Navigate to="/home" replace />} /></Routes>; }
