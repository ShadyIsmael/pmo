"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useParams, useRouter } from "next/navigation";
import { PortfolioManagementWorkspace } from "@/components/portfolios/PortfolioManagementWorkspace";
import { PortfolioSheet } from "@/components/portfolios/PortfolioSheet";
import { Button } from "@/components/ui/button";
import { usePortfolio, useArchivePortfolio, useReactivatePortfolio } from "@/hooks/portfolios/usePortfolios";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";

export default function PortfolioDetailPage() {
  const params = useParams<{ id: string }>();
  const locale = useLocale();
  const router = useRouter();
  const t = useTranslations("portfolioManager");
  const { user } = useAuth();
  const { canMutate } = useManagementPerspective();
  const portfolio = usePortfolio(params.id);
  const archive = useArchivePortfolio();
  const reactivate = useReactivatePortfolio();
  const [editOpen, setEditOpen] = useState(false);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Manage) && canMutate);

  const actions = canManage && portfolio.data ? <div className="flex gap-2"><Button size="sm" variant="outline" onClick={() => setEditOpen(true)}>{t("edit")}</Button>{portfolio.data.status !== "Archived" ? <Button size="sm" variant="outline" onClick={async () => { if (!confirm(t("archiveConfirm"))) return; await archive.mutateAsync({ id: params.id }); router.push(`/${locale}/portfolios`); }} disabled={archive.isPending}>{t("archive")}</Button> : <Button size="sm" variant="outline" onClick={() => void reactivate.mutateAsync({ id: params.id })} disabled={reactivate.isPending}>{t("reactivate")}</Button>}</div> : undefined;

  return <><PortfolioManagementWorkspace portfolioId={params.id} actions={actions} /><PortfolioSheet open={editOpen} onOpenChange={setEditOpen} portfolio={portfolio.data} /></>;
}
