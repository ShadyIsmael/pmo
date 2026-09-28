"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowRight, CheckCircle2, KeyRound, ShieldCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { LoadingSkeleton } from "@/components/ui/state-display";
import { ApiError } from "@/lib/api";
import { stakeholderInvitationApi } from "@/lib/api/stakeholder-invitations";

export default function AcceptStakeholderInvitationPage() {
  const t = useTranslations("stakeholder");
  const params = useParams<{ locale: string }>();
  const token = useSearchParams().get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const validation = useQuery({ queryKey: ["stakeholder-invitation", "validate", token], queryFn: () => stakeholderInvitationApi.validate(token), enabled: token.length > 0, retry: false });
  const accept = useMutation({ mutationFn: () => stakeholderInvitationApi.accept(token, { password, confirmPassword }) });

  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); await accept.mutateAsync(); };
  if (accept.isSuccess) return <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4"><Card className="w-full max-w-lg"><CardContent className="space-y-5 p-6 text-center"><CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" /><h1 className="text-xl font-semibold">{t("invitationAccepted")}</h1><Button asChild><Link href={`/${params.locale}/login`}>{t("goToLogin")}<ArrowRight className="ms-2 h-4 w-4" /></Link></Button></CardContent></Card></main>;
  if (!token || validation.isError) { const message = validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("expired") ? t("invitationExpired") : validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("revoked") ? t("invitationRevoked") : t("invitationInvalid"); return <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4"><Card className="w-full max-w-lg"><CardContent className="space-y-3 p-6 text-center"><ShieldCheck className="mx-auto h-10 w-10 text-muted-foreground" /><h1 className="text-xl font-semibold">{message}</h1><p className="text-sm text-muted-foreground">{t("invitationAcceptError")}</p></CardContent></Card></main>; }
  if (validation.isLoading || !validation.data) return <main className="flex min-h-screen items-center justify-center p-4"><Card className="w-full max-w-lg"><CardContent className="p-6"><LoadingSkeleton rows={4} /></CardContent></Card></main>;
  return <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4"><Card className="w-full max-w-lg"><CardHeader><div className="mb-2 flex items-center gap-2"><Badge variant="outline">{t("stakeholder")}</Badge><ShieldCheck className="h-4 w-4 text-emerald-600" /></div><CardTitle>{t("acceptInvitationTitle")}</CardTitle><p className="text-sm text-muted-foreground">{t("acceptInvitationDescription")}</p></CardHeader><CardContent><form className="space-y-5" onSubmit={(event) => void submit(event)}><section className="space-y-3 rounded-lg border bg-muted/20 p-4"><div><p className="text-xs text-muted-foreground">{t("acceptInvitationResource")}</p><p className="font-medium">{validation.data.resourceName}</p></div><div><p className="text-xs text-muted-foreground">{t("acceptInvitationEmail")}</p><p className="font-medium">{validation.data.email}</p></div><div><p className="text-xs text-muted-foreground">{t("acceptInvitationProject")}</p><p className="font-medium">{validation.data.projectName}</p></div></section><div className="space-y-2"><Label htmlFor="stakeholder-password">{t("setPassword")}</Label><div className="relative"><KeyRound className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="stakeholder-password" type="password" className="ps-9" minLength={6} required value={password} onChange={(event) => setPassword(event.target.value)} /></div></div><div className="space-y-2"><Label htmlFor="stakeholder-confirm-password">{t("confirmPassword")}</Label><Input id="stakeholder-confirm-password" type="password" minLength={6} required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} /></div>{accept.error ? <p className="text-sm text-destructive" role="alert">{accept.error instanceof ApiError ? accept.error.message : t("invitationAcceptError")}</p> : null}<Button className="w-full" type="submit" disabled={accept.isPending}>{accept.isPending ? t("submitting") : t("activateAccount")}</Button></form></CardContent></Card></main>;
}
