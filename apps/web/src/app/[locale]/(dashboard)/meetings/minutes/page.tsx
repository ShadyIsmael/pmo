"use client";

import { ArrowLeft, FileText } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link } from "@/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export default function MeetingMinutesPage() {
  const t = useTranslations("meetings");

  return (
    <div className="space-y-6">
      <PageHeader title={t("minutesTitle")} description={t("minutesPageDescription")} />
      <Card>
        <CardContent className="flex min-h-64 flex-col items-center justify-center gap-3 py-12 text-center">
          <FileText className="h-10 w-10 text-muted-foreground/60" />
          <h2 className="font-medium">{t("minutesPageTitle")}</h2>
          <p className="max-w-md text-sm text-muted-foreground">{t("minutesPageDescription")}</p>
          <Button asChild variant="outline"><Link href="/meetings"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("backToMeetings")}</Link></Button>
        </CardContent>
      </Card>
    </div>
  );
}
