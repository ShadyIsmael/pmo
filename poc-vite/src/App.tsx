import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { useState } from "react";
import RiskRegisterPage from "@/pages/RiskRegister";

function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: false,
            refetchOnWindowFocus: false,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}

export default function App() {
  return (
    <Providers>
      <BrowserRouter>
        <div className="flex min-h-screen flex-col bg-muted/30">
          <main className="flex-1 overflow-auto">
            <div className="mx-auto max-w-7xl flex flex-col gap-4 p-4 lg:p-5 xl:p-6">
              <Routes>
                <Route path="/" element={<Navigate to="/risks" replace />} />
                <Route path="/risks" element={<RiskRegisterPage />} />
                <Route path="*" element={<Navigate to="/risks" replace />} />
              </Routes>
            </div>
          </main>
        </div>
      </BrowserRouter>
    </Providers>
  );
}
