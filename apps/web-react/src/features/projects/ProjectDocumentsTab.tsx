import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { Archive, BookOpen, Check, Download, File, FileImage, FileText, History, Search, Upload } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Skeleton } from "@components/ui/Skeleton";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { formatDate, getLookupTextClass, getStatusTextClass } from "@lib/utils";
import { hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { shouldLoadProjectLookup } from "./lib/project-lookups";
import {
  documentApi,
  useArchiveDocument,
  useProjectDocument,
  useDocumentCategories,
  useProjectDocuments,
  useUploadDocument,
  useUploadDocumentVersion,
  type DocumentDetail,
  type DocumentListItem,
} from "./api/documents";

function fileSize(value?: number) { if (!value) return "—"; if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`; return `${(value / (1024 * 1024)).toFixed(1)} MB`; }
function fileIcon(type?: string) { return type?.toLowerCase().includes("image") ? FileImage : type?.toLowerCase() === "pdf" ? FileText : File; }

export function ProjectDocumentsTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  return <DocumentCenter projectId={projectId} compact />;
}

export function DocumentsCenterPage() {
  return <DocumentCenter compact={false} />;
}

function DocumentCenter({ projectId, compact }: { projectId?: string; compact: boolean }) {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const { canMutate } = useManagement();
  const [tab, setTab] = React.useState("documents");
  const [search, setSearch] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [showUpload, setShowUpload] = React.useState(false);
  const [showVersion, setShowVersion] = React.useState(false);
  const [showTemplateStart, setShowTemplateStart] = React.useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = React.useState<string | null>(null);
  const canCreate = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Create);
  const canEdit = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Edit);
  const canArchive = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Archive);
  const canTemplate = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.TemplateManage);
  const query = useProjectDocuments({ projectId, search: search || undefined, page, pageSize: compact ? 8 : 15, templatesOnly: tab === "templates" }, Boolean(projectId) || !compact);
  const templatesQuery = useProjectDocuments({ page: 1, pageSize: 50, templatesOnly: true }, showTemplateStart);
  const categories = useDocumentCategories(shouldLoadProjectLookup(showUpload || (!compact && tab === "categories")));
  const selected = useProjectDocument(selectedId);
  const archive = useArchiveDocument();
  const upload = useUploadDocument();
  const version = useUploadDocumentVersion();

  const items = query.data?.items ?? [];

  const download = async (item: DocumentListItem) => {
    const blob = await documentApi.download(item.id);
    const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = item.fileName || item.name; anchor.click(); URL.revokeObjectURL(url);
  };
  const preview = async (item: DocumentListItem) => {
    const blob = await documentApi.preview(item.id);
    const url = URL.createObjectURL(blob);
    window.open(url, "_blank", "noopener,noreferrer");
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  };
  const downloadVersion = async (item: DocumentListItem, versionNumber: number) => {
    const blob = await documentApi.download(item.id, versionNumber);
    const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = `${item.name}-v${versionNumber}${item.fileName?.includes(".") ? `.${item.fileName.split(".").pop()}` : ""}`; anchor.click(); URL.revokeObjectURL(url);
  };
  const submitUpload = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const form = new FormData(event.currentTarget); if (projectId) form.set("ProjectId", projectId);
    await upload.mutateAsync(form); setShowUpload(false); event.currentTarget.reset();
  };
  const submitVersion = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); if (!selectedId) return; const form = new FormData(event.currentTarget); await version.mutateAsync({ id: selectedId, data: form }); setShowVersion(false);
  };
  const submitTemplateStart = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const template = templatesQuery.data?.items.find((item) => item.id === selectedTemplateId);
    if (!template || !projectId) return;
    const form = new FormData(event.currentTarget);
    form.set("ProjectId", projectId); form.set("IsTemplate", "false"); form.set("SourceTemplateId", template.id); form.set("SourceTemplateVersionNumber", String(template.version));
    await upload.mutateAsync(form); setShowTemplateStart(false); setSelectedTemplateId(null); event.currentTarget.reset();
  };

  const Icon = selected.data?.document ? fileIcon(selected.data.document.fileType) : FileText;

  return <div className={compact ? "space-y-3" : "space-y-5"}>
    {compact
      ? <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"><div><h3 className="text-sm font-semibold">{t("documents.projectTitle")}</h3><p className="text-xs text-muted-foreground">{t("documents.projectDescription")}</p></div>{canCreate ? <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" onClick={() => setShowTemplateStart(true)}><BookOpen className="me-2 h-4 w-4" />{t("documents.startFromTemplate")}</Button><Button size="sm" onClick={() => setShowUpload(true)}><Upload className="me-2 h-4 w-4" />{t("documents.upload")}</Button></div> : null}</div>
      : <PageHeader title={t("documents.title")} description={t("documents.description")} actions={canCreate ? <Button size="sm" onClick={() => setShowUpload(true)}><Upload className="me-2 h-4 w-4" />{t("documents.upload")}</Button> : null} />}
    <div className="grid gap-3 sm:grid-cols-3"><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("documents.metrics.documents")}</p><p className="mt-1 text-2xl font-semibold tabular-nums">{query.data?.totalCount?.toLocaleString("en-US") ?? "—"}</p></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("documents.metrics.templates")}</p><p className="mt-1 text-2xl font-semibold tabular-nums">{tab === "templates" ? query.data?.totalCount?.toLocaleString("en-US") ?? "—" : "—"}</p></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("documents.metrics.allowedFormats")}</p><p className="mt-1 text-sm font-semibold">PDF · DOCX · XLSX · PPTX</p></CardContent></Card></div>
    <Tabs value={tab} onValueChange={(value) => { setTab(value); setPage(1); }}>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><TabsList><TabsTrigger value="documents">{t("documents.tabs.documents")}</TabsTrigger><TabsTrigger value="templates">{t("documents.tabs.templates")}</TabsTrigger>{!compact ? <TabsTrigger value="categories">{t("documents.tabs.categories")}</TabsTrigger> : null}</TabsList><div className="relative w-full sm:max-w-xs"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder={t("documents.search")} className="ps-8" /></div></div>
      <TabsContent value="documents" className="mt-3"><DocumentTable items={items} loading={query.isLoading} error={query.isError} emptyTitle={t("documents.emptyTitle")} emptyDescription={t("documents.emptyDescription")} onRetry={() => void query.refetch()} onOpen={setSelectedId} onDownload={(item) => void download(item)} locale={locale} /><DocumentPagination query={query.data} page={page} setPage={setPage} t={t} compact={compact} /></TabsContent>
      <TabsContent value="templates" className="mt-3"><DocumentTable items={items} loading={query.isLoading} error={query.isError} emptyTitle={t("documents.templatesEmptyTitle")} emptyDescription={t("documents.templatesEmptyDescription")} onRetry={() => void query.refetch()} onOpen={setSelectedId} onDownload={(item) => void download(item)} locale={locale} /><DocumentPagination query={query.data} page={page} setPage={setPage} t={t} compact={compact} /></TabsContent>
      {!compact ? <TabsContent value="categories" className="mt-3"><Card><CardHeader><CardTitle>{t("documents.categoriesTitle")}</CardTitle></CardHeader><CardContent>{categories.isLoading ? <div className="space-y-2"><Skeleton className="h-12" /><Skeleton className="h-12" /><Skeleton className="h-12" /><Skeleton className="h-12" /></div> : categories.data?.length ? <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{categories.data.map((category) => <div key={category.id} className="rounded-lg border border-border bg-muted/10 p-3"><p className="text-sm font-medium">{category.name}</p><p className="mt-1 text-xs text-muted-foreground">{category.description || category.code || t("documents.categoryFallback")}</p></div>)}</div> : <EmptyState title={t("documents.categoriesEmptyTitle")} description={t("documents.categoriesEmptyDescription")} />}</CardContent></Card></TabsContent> : null}
    </Tabs>
    <SideSheet open={!!selectedId} onOpenChange={(open) => { if (!open) setSelectedId(null); }} title={selected.data?.document.name || t("documents.details")} description={selected.data?.document.fileName || t("documents.detailsDescription")} size="lg"><SideSheetBody>{selected.isLoading ? <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : selected.data ? <DocumentDetails detail={selected.data} icon={Icon} t={t} canEdit={canEdit} canArchive={canArchive} onDownload={() => void download(selected.data!.document)} onPreview={() => void preview(selected.data!.document)} onVersion={() => setShowVersion(true)} onDownloadVersion={(versionNumber) => void downloadVersion(selected.data!.document, versionNumber)} onArchive={() => { void archive.mutateAsync(selected.data!.document.id); setSelectedId(null); }} /> : null}</SideSheetBody></SideSheet>
    <SideSheet open={showUpload} onOpenChange={setShowUpload} title={t("documents.uploadTitle")} description={projectId ? t("documents.projectUploadDescription") : t("documents.uploadDescription")}><form onSubmit={(event) => void submitUpload(event)}><SideSheetBody className="space-y-4"><label className="block text-sm font-medium">{t("documents.fields.name")}<Input name="Name" className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.description")}<Input name="Description" className="mt-1" /></label><label className="block text-sm font-medium">{t("documents.fields.category")}<select name="CategoryId" className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm">{categories.data?.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>{canTemplate && !projectId ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" name="IsTemplate" value="true" />{t("documents.fields.template")}</label> : null}<label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="File" type="file" className="mt-1" required /></label></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setShowUpload(false)}>{t("documents.cancel")}</Button><Button type="submit" disabled={upload.isPending}>{upload.isPending ? t("documents.saving") : t("documents.upload")}</Button></SideSheetFooter></form></SideSheet>
    <SideSheet open={showVersion} onOpenChange={setShowVersion} title={t("documents.newVersionTitle")}><form onSubmit={(event) => void submitVersion(event)}><SideSheetBody className="space-y-4"><label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="file" type="file" className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.changeNotes")}<Input name="changeNotes" className="mt-1" /></label></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setShowVersion(false)}>{t("documents.cancel")}</Button><Button type="submit" disabled={version.isPending}>{t("documents.saveVersion")}</Button></SideSheetFooter></form></SideSheet>
    <SideSheet open={showTemplateStart} onOpenChange={(open) => { setShowTemplateStart(open); if (!open) setSelectedTemplateId(null); }} title={t("documents.startFromTemplate")} description={t("documents.startFromTemplateDescription")} size="lg"><SideSheetBody className="space-y-4">{templatesQuery.isLoading ? <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : !templatesQuery.data?.items.length ? <EmptyState icon={<BookOpen className="h-10 w-10" />} title={t("documents.templatesEmptyTitle")} description={t("documents.templatesEmptyDescription")} /> : <div className="space-y-2">{templatesQuery.data.items.map((template) => { const isSelected = template.id === selectedTemplateId; const TemplateIcon = fileIcon(template.fileType); return <button key={template.id} type="button" onClick={() => setSelectedTemplateId(template.id)} className={`flex w-full items-center gap-3 rounded-lg border p-3 text-start transition-colors ${isSelected ? "border-primary bg-primary/5" : "border-border hover:bg-muted/30"}`}><TemplateIcon className="h-5 w-5 shrink-0 text-primary" /><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{template.name}</span><span className="block text-xs text-muted-foreground">{template.fileName} · v{template.version}</span></span>{isSelected ? <Check className="h-4 w-4 text-primary" /> : null}</button>; })}</div>}{(() => { const template = templatesQuery.data?.items.find((item) => item.id === selectedTemplateId); if (!template) return null; return <TemplateStartForm key={selectedTemplateId} template={template} t={t} onDownload={() => void download(template)} onSubmit={submitTemplateStart} pending={upload.isPending} />; })()}</SideSheetBody></SideSheet>
  </div>;
}

function DocumentTable({ items, loading, error, emptyTitle, emptyDescription, onRetry, onOpen, onDownload, locale }: { items: readonly DocumentListItem[]; loading: boolean; error: boolean; emptyTitle: string; emptyDescription: string; onRetry: () => void; onOpen: (id: string) => void; onDownload: (item: DocumentListItem) => void; locale: string }) {
  const { t } = useI18n();
  if (loading) return <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div>;
  if (error) return <ErrorState title={t("documents.loadError")} onRetry={onRetry} />;
  if (!items.length) return <EmptyState icon={<FileText className="h-10 w-10" />} title={emptyTitle} description={emptyDescription} />;
  return <div className="nexus-table-surface overflow-hidden"><div className="nexus-table-header grid grid-cols-12 gap-2 text-center"><div className="col-span-5 text-start">{t("documents.table.name")}</div><div className="col-span-2">{t("documents.table.type")}</div><div className="col-span-2">{t("documents.table.size")}</div><div className="col-span-2">{t("documents.table.updated")}</div><div className="col-span-1" /></div>{items.map((item) => { const Icon = fileIcon(item.fileType); return <div key={item.id} className="nexus-table-row grid grid-cols-12 items-center gap-2 px-3 py-3 text-sm"><button type="button" onClick={() => onOpen(item.id)} className="col-span-5 flex min-w-0 items-center gap-2 text-start hover:text-primary"><Icon className="h-4 w-4 shrink-0 text-primary" /><span className="truncate font-medium">{item.name}</span></button><div className={`col-span-2 text-center text-xs font-medium ${getLookupTextClass(item.fileType)}`}>{item.fileType || "—"}</div><div className="col-span-2 text-center text-xs text-muted-foreground tabular-nums">{fileSize(item.fileSize)}</div><div className="col-span-2 text-center text-xs text-muted-foreground">{formatDate(item.updatedAt || item.createdAt, locale)}</div><div className="col-span-1 flex justify-end"><Button variant="ghost" size="icon" className="h-8 w-8" aria-label={t("documents.download")} onClick={() => onDownload(item)}><Download className="h-4 w-4" /></Button></div></div>; })}</div>;
}

function TemplateStartForm({ template, t, onDownload, onSubmit, pending }: { template: DocumentListItem; t: (key: string, values?: Record<string, string | number>) => string; onDownload: () => void; onSubmit: (event: React.FormEvent<HTMLFormElement>) => void; pending: boolean }) {
  return <div className="rounded-lg border border-primary/20 bg-primary/[0.04] p-4"><div className="flex flex-wrap items-center justify-between gap-2"><div><p className="text-sm font-semibold">{template.name}</p><p className="mt-1 text-xs text-muted-foreground">{t("documents.templateVersionSource", { version: template.version })}</p></div><Button type="button" variant="outline" size="sm" onClick={onDownload}><Download className="me-2 h-4 w-4" />{t("documents.downloadBlank")}</Button></div><form className="mt-4 space-y-3" onSubmit={onSubmit}><label className="block text-sm font-medium">{t("documents.fields.name")}<Input name="Name" defaultValue={template.name} className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.description")}<Input name="Description" defaultValue={t("documents.createdFromTemplate", { name: template.name })} className="mt-1" /></label><label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="File" type="file" className="mt-1" required /></label><Button type="submit" disabled={pending} className="w-full">{pending ? t("documents.saving") : t("documents.createFromTemplate")}</Button></form></div>;
}

function DocumentDetails({ detail, icon: Icon, t, canEdit, canArchive, onDownload, onPreview, onVersion, onDownloadVersion, onArchive }: { detail: DocumentDetail; icon: typeof File; t: (key: string, values?: Record<string, string | number>) => string; canEdit: boolean; canArchive: boolean; onDownload: () => void; onPreview: () => void; onVersion: () => void; onDownloadVersion: (versionNumber: number) => void; onArchive: () => void }) {
  const item = detail.document; const previewable = item.contentType?.startsWith("image/") || item.contentType === "application/pdf";
  return <div className="space-y-5"><div className="flex items-start gap-3 rounded-lg border border-border bg-muted/20 p-4"><Icon className="mt-0.5 h-8 w-8 shrink-0 text-primary" /><div className="min-w-0"><p className="font-semibold">{item.name}</p><p className="mt-1 text-xs text-muted-foreground">{item.fileName} · {fileSize(item.fileSize)}</p><div className="mt-2 flex flex-wrap gap-2"><span className={`text-xs font-medium ${getLookupTextClass(item.fileType)}`}>{item.fileType || "FILE"}</span><span className={`text-xs font-medium ${getStatusTextClass(item.status)}`}>{item.status}</span><Badge variant="outline">v{item.version}</Badge></div></div></div><div className="grid gap-3 sm:grid-cols-2"><div><p className="text-xs text-muted-foreground">{t("documents.fields.description")}</p><p className="mt-1 text-sm">{item.description || "—"}</p></div><div><p className="text-xs text-muted-foreground">{t("documents.fields.project")}</p><p className="mt-1 text-sm">{item.projectId || t("documents.globalScope")}</p></div></div>{item.sourceTemplateId ? <div className="rounded-lg border border-primary/20 bg-primary/[0.04] p-3 text-sm"><p className="font-medium">{t("documents.sourceTemplate")}</p><p className="mt-1 text-xs text-muted-foreground">{item.sourceTemplateId} · {t("documents.templateVersionSource", { version: item.sourceTemplateVersionNumber ?? 1 })}</p></div> : null}<div className="flex flex-wrap gap-2"><Button onClick={onDownload} size="sm"><Download className="me-2 h-4 w-4" />{t("documents.download")}</Button>{previewable ? <Button variant="outline" size="sm" onClick={onPreview}>{t("documents.preview")}</Button> : null}{canEdit ? <Button variant="outline" size="sm" onClick={onVersion}><Upload className="me-2 h-4 w-4" />{t("documents.newVersion")}</Button> : null}{canArchive && !item.isArchived ? <Button variant="ghost" size="sm" className="text-destructive" onClick={onArchive}><Archive className="me-2 h-4 w-4" />{t("documents.archive")}</Button> : null}</div><div><div className="mb-2 flex items-center gap-2"><History className="h-4 w-4 text-muted-foreground" /><p className="text-sm font-semibold">{t("documents.versions")}</p></div><div className="space-y-2">{detail.versions.map((v) => <div key={v.id} className="flex items-center justify-between gap-3 rounded border px-3 py-2 text-sm"><div className="min-w-0"><p className="font-medium">v{v.versionNumber}{v.isCurrent ? ` · ${t("documents.current")}` : ""}</p><p className="mt-0.5 truncate text-xs text-muted-foreground">{v.fileName} · {fileSize(v.fileSize)}{v.uploadedBy ? ` · ${v.uploadedBy}` : ""}</p></div><Button variant="ghost" size="sm" onClick={() => onDownloadVersion(v.versionNumber)}><Download className="h-3.5 w-3.5" /></Button></div>)}</div></div></div>;
}

function DocumentPagination({ query, page, setPage, t, compact }: { query?: { totalPages: number; totalCount: number; pageSize: number }; page: number; setPage: (page: number) => void; t: (key: string, values?: Record<string, string | number>) => string; compact: boolean }) {
  if (!query || query.totalCount === 0) return null;
  return <div className="mt-3 flex flex-wrap items-center justify-between gap-2"><p className="text-xs text-muted-foreground">{t("documents.pagination", { count: query.totalCount.toLocaleString("en-US") })}{compact ? ` · ${t("documents.compactFooter")}` : ""}</p><Pagination page={page} totalPages={query.totalPages} total={query.totalCount} pageSize={query.pageSize} onChange={setPage} /></div>;
}
