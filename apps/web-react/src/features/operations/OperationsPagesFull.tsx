import * as React from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowUpRight, Bot, FileText, Plus, Search, Send } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useFullWorkItems, type WorkItemType } from "@api/workItemsFull";

type Paged<T> = { items: T[]; totalCount: number; totalPages?: number };
type Report = { id: string; name: string; description?: string; category: string; outputFormat?: string; isSystem: boolean; isActive: boolean; createdAt?: string; updatedAt?: string };
type AuditEntry = { id: string; entityType: string; entityId: string; action: string; changes?: string; userName?: string; timestamp: string; };
type AIRequest = { id: string; title: string; context: string; provider: string; model?: string; status: string; requestedAt: string; completedAt?: string; result?: string; errorMessage?: string };

function Loading({ text }: { text: string }) { return <p className="py-12 text-center text-sm text-muted-foreground">{text}</p>; }
function ErrorState({ text }: { text: string }) { return <p className="py-12 text-center text-sm text-destructive">{text}</p>; }

export function ReportsPageFull() {
  const { t, formatDate } = useI18n();
  const query = useQuery({ queryKey: ["reports", "definitions"], queryFn: () => api.get<Paged<Report>>(endpoints.reports.root, { page: 1, pageSize: 100 }), staleTime: 30_000 });
  const [search, setSearch] = React.useState("");
  const items = (query.data?.items ?? []).filter(item => !search || `${item.name} ${item.category}`.toLowerCase().includes(search.toLowerCase()));
  return <div className="space-y-6"><PageHeader title={t("operations.reportsTitle")} description={t("operations.reportsDescription")} /><div className="relative max-w-md"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("operations.searchReports")} className="ps-8" /></div><Card><CardContent className="p-0">{query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : items.length ? <Table><TableHeader><TableRow><TableHead>{t("operations.name")}</TableHead><TableHead>{t("operations.category")}</TableHead><TableHead>{t("operations.format")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("operations.updated")}</TableHead></TableRow></TableHeader><TableBody>{items.map(item => <TableRow key={item.id}><TableCell className="font-semibold"><FileText className="me-2 inline h-4 w-4" />{item.name}</TableCell><TableCell>{item.category}</TableCell><TableCell>{item.outputFormat ?? "—"}</TableCell><TableCell><Badge status={item.isActive ? "Active" : "Inactive"}>{item.isActive ? t("common.active") : t("common.inactive")}</Badge></TableCell><TableCell>{item.updatedAt ? formatDate(item.updatedAt) : "—"}</TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}</CardContent></Card></div>;
}

export function AuditLogPageFull() {
  const { t, formatDate } = useI18n();
  const [search, setSearch] = React.useState("");
  const query = useQuery({ queryKey: ["audit", "entries"], queryFn: () => api.get<Paged<AuditEntry>>(endpoints.audit.root, { page: 1, pageSize: 100 }), staleTime: 15_000 });
  const entries = (query.data?.items ?? []).filter(entry => !search || `${entry.entityType} ${entry.action} ${entry.userName ?? ""} ${entry.changes ?? ""}`.toLowerCase().includes(search.toLowerCase()));
  return <div className="space-y-6"><PageHeader title={t("operations.auditTitle")} description={t("operations.auditDescription")} /><div className="relative max-w-lg"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("operations.searchAudit")} className="ps-8" /></div><Card><CardContent className="p-0">{query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : entries.length ? <Table><TableHeader><TableRow><TableHead>{t("operations.timestamp")}</TableHead><TableHead>{t("operations.user")}</TableHead><TableHead>{t("operations.action")}</TableHead><TableHead>{t("operations.entity")}</TableHead><TableHead>{t("operations.changes")}</TableHead></TableRow></TableHeader><TableBody>{entries.map(entry => <TableRow key={entry.id}><TableCell className="whitespace-nowrap text-xs">{formatDate(entry.timestamp)}</TableCell><TableCell>{entry.userName ?? "—"}</TableCell><TableCell className="font-mono text-xs">{entry.action}</TableCell><TableCell>{entry.entityType}<span className="ms-1 text-xs text-muted-foreground">{entry.entityId}</span></TableCell><TableCell className="max-w-md truncate text-xs text-muted-foreground">{entry.changes ?? "—"}</TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}</CardContent></Card></div>;
}

export function WorkItemCollectionPageFull({ type, titleKey, descriptionKey }: { type: WorkItemType; titleKey: string; descriptionKey: string }) {
  const { t, formatDate } = useI18n();
  const [search, setSearch] = React.useState("");
  const query = useFullWorkItems({ type, search: search || undefined, page: 1, pageSize: 100 });
  const items = query.data?.items ?? [];
  return <div className="space-y-6"><PageHeader title={t(titleKey)} description={`${t(descriptionKey)} · ${query.data?.totalCount ?? 0}`} actions={<Link to="/work-items" className="button-outline"><Plus className="me-1 h-4 w-4" />{t("operations.openWorkItems")}</Link>} /><div className="relative max-w-lg"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("operations.searchWorkItems")} className="ps-8" /></div><Card><CardContent className="p-0">{query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : items.length ? <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.priority")}</TableHead><TableHead>{t("workItems.assignee")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("workItems.dueDate")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{items.map(item => <TableRow key={item.id}><TableCell className="font-semibold">{item.title}</TableCell><TableCell><Badge status={item.priority}>{item.priority}</Badge></TableCell><TableCell>{item.assignedResourceName ?? item.assignedTeamName ?? t("workItems.unassigned")}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell><TableCell>{item.dueDate ? formatDate(item.dueDate) : "—"}</TableCell><TableCell><Link to={`/work-items/${encodeURIComponent(item.id)}`} className="text-primary hover:underline"><ArrowUpRight className="h-4 w-4" /></Link></TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}</CardContent></Card></div>;
}

export function AIWorkspacePageFull() {
  const { t, formatDate } = useI18n();
  const [title, setTitle] = React.useState("");
  const [context, setContext] = React.useState("");
  const [selected, setSelected] = React.useState<AIRequest | null>(null);
  const history = useQuery({ queryKey: ["ai-planning", "requests"], queryFn: () => api.get<Paged<AIRequest>>(endpoints.aiPlanning.root, { page: 1, pageSize: 50 }), staleTime: 15_000 });
  const generate = useMutation({ mutationFn: (body: { title: string; context: string }) => api.post<AIRequest>(endpoints.aiPlanning.generate, body), onSuccess: (result) => setSelected(result) });
  function submit(event: React.FormEvent) { event.preventDefault(); if (title.trim() && context.trim()) generate.mutate({ title: title.trim(), context: context.trim() }); }
  return <div className="space-y-6"><PageHeader title={t("operations.aiTitle")} description={t("operations.aiDescription")} actions={<Badge status="AI"><Bot className="me-1 h-4 w-4" />{t("operations.aiBackend")}</Badge>} /><div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(18rem,1fr)]"><Card><CardHeader><CardTitle className="flex items-center gap-2"><Bot className="h-5 w-5 text-primary" />{t("operations.generatePlan")}</CardTitle></CardHeader><CardContent><form onSubmit={submit} className="space-y-4"><div className="space-y-1.5"><Label>{t("operations.titleField")}</Label><Input value={title} onChange={(e) => setTitle(e.target.value)} required /></div><div className="space-y-1.5"><Label>{t("operations.context")}</Label><Textarea value={context} onChange={(e) => setContext(e.target.value)} rows={8} required placeholder={t("operations.contextPlaceholder")} /></div><Button type="submit" disabled={generate.isPending}><Send className="me-1.5 h-4 w-4" />{generate.isPending ? t("operations.generating") : t("operations.generate")}</Button>{generate.isError ? <p className="text-sm text-destructive">{t("operations.generateError")}</p> : null}</form>{selected ? <div className="mt-6 rounded border p-4"><p className="mb-2 font-semibold">{selected.title}</p><p className="whitespace-pre-wrap text-sm">{selected.result ?? selected.errorMessage ?? selected.status}</p></div> : null}</CardContent></Card><Card><CardHeader><CardTitle>{t("operations.aiHistory")}</CardTitle></CardHeader><CardContent className="space-y-2">{history.isLoading ? <Loading text={t("common.loading")} /> : (history.data?.items ?? []).map(item => <button key={item.id} type="button" className="w-full rounded border p-3 text-start hover:bg-muted/50" onClick={() => setSelected(item)}><p className="font-medium">{item.title}</p><p className="text-xs text-muted-foreground">{item.status} · {formatDate(item.requestedAt)}</p></button>)}{!history.isLoading && !(history.data?.items ?? []).length ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : null}</CardContent></Card></div></div>;
}

export function SecurityAuditPageFull() { return <AuditLogPageFull />; }
