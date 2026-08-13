"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Loader2, LogIn, Eye, EyeOff, Building2, Shield } from "lucide-react";
import { APP_NAME } from "@/lib/constants";

export default function LoginPage({ params }: { params: { locale: string } }) {
  const { locale } = params;
  const router = useRouter();
  const { login, isAuthenticated, status } = useAuth();

  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [showPassword, setShowPassword] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (isAuthenticated) {
      router.replace(`/${locale}`);
    }
  }, [isAuthenticated, locale, router]);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email.trim(), password);
      router.replace(`/${locale}`);
    } catch (err) {
      setError((err as Error).message || "Unable to sign in. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (status === "loading") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-4">
          <div className="relative flex h-14 w-14 items-center justify-center rounded-2xl bg-primary-gradient text-lg font-bold text-white shadow-primary">
            N
          </div>
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center bg-background px-4 py-12 overflow-hidden">
      {/* Background decorative elements */}
      <div className="absolute inset-0" aria-hidden="true">
        <div className="blur-bg top-1/4 left-1/4 h-72 w-72 bg-primary/10" />
        <div className="blur-bg bottom-1/4 right-1/4 h-72 w-72 bg-info/10" />
        <div className="blur-bg top-1/2 right-1/6 h-48 w-48 bg-success/10" />
        <div className="absolute inset-0 bg-grid/30" />
      </div>

      <div className="relative w-full max-w-md animate-in stagger-1">
        <Card className="border border-border/50 shadow-elevated bg-card/80 backdrop-blur-sm">
          <CardHeader className="space-y-2 text-center pb-3">
            <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-gradient text-lg font-bold text-white shadow-primary">
              N
            </div>
            <CardTitle className="text-2xl font-bold tracking-tight text-foreground">{APP_NAME}</CardTitle>
            <CardDescription className="text-muted-foreground">Sign in to your workspace</CardDescription>
          </CardHeader>
          
          <form onSubmit={handleSubmit}>
            <CardContent className="space-y-5">
              <div className="space-y-2">
                <Label htmlFor="email" className="label-text">Email</Label>
                <div className="relative">
                  <Input
                    id="email"
                    type="email"
                    placeholder="you@company.com"
                    autoComplete="username"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    className="pl-10"
                    disabled={submitting}
                  />
                  <Building2 className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/50" aria-hidden="true" />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="password" className="label-text">Password</Label>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? "text" : "password"}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    className="pl-10 pr-10"
                    disabled={submitting}
                  />
                  <Shield className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/50" aria-hidden="true" />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground/50 hover:text-muted-foreground transition-colors"
                    aria-label={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                </div>
              </div>
              
              {error && (
                <div className="rounded-lg border bg-destructive/10 p-3 text-sm text-destructive animate-in" role="alert">
                  {error}
                </div>
              )}
            </CardContent>
            
            <CardFooter className="space-y-4 pt-4">
              <Button type="submit" className="w-full" size="lg" disabled={submitting}>
                {submitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Signing in...
                  </>
                ) : (
                  <>
                    <LogIn className="mr-2 h-4 w-4" />
                    Sign in
                  </>
                )}
              </Button>
              
              <p className="text-center text-xs text-muted-foreground">
                Demo: admin@nexus.com · password Demo123!
              </p>
            </CardFooter>
          </form>
        </Card>
        
        <div className="mt-6 text-center text-xs text-muted-foreground/60">
          <p>Enterprise Project Portfolio Management Platform</p>
        </div>
      </div>
    </div>
  );
}