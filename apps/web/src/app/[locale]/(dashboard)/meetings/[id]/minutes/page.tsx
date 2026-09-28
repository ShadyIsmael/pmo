"use client";

import * as React from "react";
import { ArrowLeft, CheckCircle2, FileText, Loader2, Save } from "lucide-react";
import { useParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link } from "@/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { RichTextContent, RichTextEditor } from "@/components/ui/rich-text-editor";
import { MeetingMinutesCollaboration } from "@/components/meetings/MeetingMinutesCollaboration";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import {
  useCreateMeetingMinutes,
  useFinalizeMeetingMinutes,
  useMeeting,
  useMeetingMinutes,
  useUpdateMeetingMinutes,
} from "@/hooks/meetings/useMeetings";
import type { CreateMeetingMinutesRequest, MeetingMinutesDto } from "@/lib/api/meetings";

type MinutesDraft = CreateMeetingMinutesRequest;

const EMPTY_DRAFT: MinutesDraft = {
  summary: "",
  discussionNotes: "",
  decisions: "",
  outcomes: "",
};

export default function MeetingMinutesEditorPage() {
  const params = useParams<{ id: string; locale: string }>();
  const meetingId = typeof params.id === "string" ? params.id : "";
  const t = useTranslations("meetings");
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage));
  const meetingQuery = useMeeting(meetingId);
  const minutesQuery = useMeetingMinutes(meetingId, authStatus === "authenticated" && canView);
  const createMinutes = useCreateMeetingMinutes();
  const updateMinutes = useUpdateMeetingMinutes();
  const finalizeMinutes = useFinalizeMeetingMinutes();
  const [draft, setDraft] = React.useState<MinutesDraft>(EMPTY_DRAFT);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const minutes = minutesQuery.data;
  const isFinal = minutes?.state === "Final";
  const isSaving = createMinutes.isPending || updateMinutes.isPending || finalizeMinutes.isPending;
  const richTextCopy = {
    formatting: t("formatting"),
    bold: t("bold"),
    italic: t("italic"),
    underline: t("underline"),
    bulletedList: t("bulletedList"),
    numberedList: t("numberedList"),
    emoji: t("emoji"),
  };

  React.useEffect(() => {
    if (!minutesQuery.isSuccess) return;
    setDraft(minutesQuery.data ? toDraft(minutesQuery.data) : EMPTY_DRAFT);
  }, [minutesQuery.data, minutesQuery.isSuccess]);

  function updateDraft(field: keyof MinutesDraft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setNotice(null);
    setError(null);
  }

  async function persistDraft(): Promise<MeetingMinutesDto> {
    const body = normalizeDraft(draft);
    if (minutes) {
      return updateMinutes.mutateAsync({ id: meetingId, body: { ...body, rowVersion: minutes.rowVersion } });
    }
    return createMinutes.mutateAsync({ id: meetingId, body });
  }

  async function save() {
    setNotice(null);
    setError(null);
    try {
      await persistDraft();
      await minutesQuery.refetch();
      setNotice(t("minutesSaved"));
    } catch {
      setError(t("minutesSaveError"));
    }
  }

  async function finalize() {
    setNotice(null);
    setError(null);
    try {
      const saved = await persistDraft();
      await finalizeMinutes.mutateAsync({ id: meetingId, rowVersion: saved.rowVersion });
      await minutesQuery.refetch();
      setNotice(t("minutesFinalized"));
    } catch {
      setError(t("minutesFinalizeError"));
    }
  }

  if (authStatus === "authenticated" && !canView) {
    return <StateCard title={t("title")} description={t("permissionDenied")} />;
  }
  if (authStatus !== "authenticated") return null;
  if (meetingQuery.isLoading || minutesQuery.isLoading) return <MinutesSkeleton />;
  if (meetingQuery.isError || !meetingQuery.data) return <StateCard title={t("loadError")} description={t("actionError")} />;
  if (minutesQuery.isError) return <StateCard title={t("minutesLoadError")} description={t("actionError")} />;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("minutesTitle")}
        description={meetingQuery.data.title}
        actions={
          <Button asChild size="sm" variant="outline">
            <Link href={`/meetings/${meetingId}`}><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("backToMeeting")}</Link>
          </Button>
        }
      >
        <span className="text-xs font-normal text-muted-foreground">{isFinal ? t("minutes.final") : t("minutes.draft")}</span>
      </PageHeader>

      {notice ? <p role="status" className="rounded-md border border-emerald-500/25 bg-emerald-500/5 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300">{notice}</p> : null}
      {error ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}

      {!minutes && !canManage ? <Card><CardContent className="flex min-h-56 flex-col items-center justify-center gap-3 py-12 text-center"><FileText className="h-10 w-10 text-muted-foreground/60" /><h2 className="font-medium">{t("noMinutesTitle")}</h2><p className="max-w-md text-sm text-muted-foreground">{t("noMinutesDescription")}</p></CardContent></Card> : (
        <MeetingMinutesCollaboration
          meetingId={meetingId}
          canManage={canManage}
          locale={params.locale}
          mainContent={
            <Card>
              <CardHeader>
                <CardTitle>{t("minutesTitle")}</CardTitle>
                <CardDescription>{isFinal ? t("minutesReadOnly") : t("minutesEditorDescription")}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-5">
                <MinutesField t={richTextCopy} id="minutes-summary" label={t("summary")} value={draft.summary ?? ""} onChange={(value) => updateDraft("summary", value)} disabled={!canManage || isFinal} placeholder={t("summaryPlaceholder")} />
                <MinutesField t={richTextCopy} id="minutes-discussion" label={t("discussionNotes")} value={draft.discussionNotes ?? ""} onChange={(value) => updateDraft("discussionNotes", value)} disabled={!canManage || isFinal} placeholder={t("discussionNotesPlaceholder")} />
                <div className="grid gap-5 lg:grid-cols-2">
                  <MinutesField t={richTextCopy} id="minutes-decisions" label={t("decisions")} value={draft.decisions ?? ""} onChange={(value) => updateDraft("decisions", value)} disabled={!canManage || isFinal} placeholder={t("decisionsPlaceholder")} />
                  <MinutesField t={richTextCopy} id="minutes-outcomes" label={t("outcomes")} value={draft.outcomes ?? ""} onChange={(value) => updateDraft("outcomes", value)} disabled={!canManage || isFinal} placeholder={t("outcomesPlaceholder")} />
                </div>
                {canManage && !isFinal ? <div className="flex flex-wrap justify-end gap-2 border-t pt-4"><Button type="button" variant="outline" onClick={() => void save()} disabled={isSaving}><Save className="me-1.5 h-4 w-4" />{isSaving && !finalizeMinutes.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : t("saveMinutes")}</Button><Button type="button" onClick={() => void finalize()} disabled={isSaving}><CheckCircle2 className="me-1.5 h-4 w-4" />{finalizeMinutes.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : t("finalizeMinutes")}</Button></div> : null}
              </CardContent>
            </Card>
          }
        />
      )}
    </div>
  );
}

function MinutesField({ t, id, label, value, onChange, disabled, placeholder }: { t: Record<string, string>; id: string; label: string; value: string; onChange: (_value: string) => void; disabled: boolean; placeholder: string }) {
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{disabled ? <div className="min-h-28 rounded-lg border border-input bg-muted/20 px-3 py-3"><RichTextContent value={value} /></div> : <RichTextEditor id={id} value={value} onChange={onChange} placeholder={placeholder} ariaLabel={label} t={t} />}</div>;
}

function normalizeDraft(draft: MinutesDraft): MinutesDraft {
  return Object.fromEntries(Object.entries(draft).map(([key, value]) => [key, value?.trim() || undefined])) as MinutesDraft;
}

function toDraft(minutes: MeetingMinutesDto): MinutesDraft {
  return { summary: minutes.summary ?? "", discussionNotes: minutes.discussionNotes ?? "", decisions: minutes.decisions ?? "", outcomes: minutes.outcomes ?? "" };
}

function MinutesSkeleton() {
  return <div className="space-y-5"><PageHeader title="" description="" actions={<Skeleton className="h-8 w-32" />} /><Card><CardContent className="space-y-5 p-5"><Skeleton className="h-24" /><Skeleton className="h-32" /><div className="grid gap-5 lg:grid-cols-2"><Skeleton className="h-32" /><Skeleton className="h-32" /></div></CardContent></Card></div>;
}

function StateCard({ title, description }: { title: string; description: string }) {
  return <Card><CardContent className="flex min-h-56 flex-col items-center justify-center gap-2 py-12 text-center"><FileText className="h-10 w-10 text-muted-foreground/60" /><h2 className="font-medium">{title}</h2><p className="text-sm text-muted-foreground">{description}</p></CardContent></Card>;
}
