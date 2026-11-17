import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { useQueries } from "@tanstack/react-query";
import { DocumentLibraryHeader, DocumentLibraryOverview } from "./DocumentLibraryOverview";
import { Archive, BookOpen, Building2, Check, Download, File, FileImage, FileSpreadsheet, FileText, History, LayoutGrid, List, Search, Upload, X } from "lucide-react";
import { LibraryDocuments } from "./LibraryDocuments";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Skeleton } from "@components/ui/Skeleton";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { formatDate, getLookupTextClass, getStatusTextClass } from "@lib/utils";
import { formatFileSize } from "@lib/formatting/file-size";
import { hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import { ApiError } from "@api/client";
import { getLocalizedApiErrorMessage, type ApiErrorLocale } from "@api/api-error-localization";
import { useManagement } from "@api/management";
import {
  documentApi,
  useActivateDocument,
  useArchiveDocument,
  useProjectDocument,
  useManagedDocumentCategories,
  useTemplateCategoryOptions,
  useProjectUploadCategoryOptions,
  useProjectDocuments,
  useUploadDocument,
  useUploadDocumentVersion,
  type DocumentDetail,
  type DocumentCategory,
  type DocumentListItem,
} from "./api/documents";

function fileIcon(type?: string) {
  const t = type?.toLowerCase() || "";
  if (t.includes("image")) return FileImage;
  if (t.includes("xls") || t.includes("excel") || t.includes("spreadsheet")) return FileSpreadsheet;
  if (t === "pdf" || t.includes("pdf")) return FileText;
  return File;
}

function MutationError({ message }: { message: string }) { return <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{message}</p>; }

function documentActionError(t: (key: string, values?: Record<string, string | number>) => string) {
  return t("documents.actionFailed");
}

function uploadErrorMessage(error: unknown, t: (key: string, values?: Record<string, string | number>) => string, locale: ApiErrorLocale) {
  if (error instanceof ApiError) {
    if (error.code === "UNSUPPORTED_FILE_TYPE") return error.message;
    const details = error.details as { detail?: unknown; message?: unknown; error?: unknown } | null | undefined;
    const serverMessage = typeof details?.detail === "string"
      ? details.detail
      : typeof details?.message === "string"
        ? details.message
        : typeof details?.error === "string"
          ? details.error
          : undefined;
    if (error.status === 400 && serverMessage && /file type is not supported/i.test(serverMessage)) {
      return getLocalizedApiErrorMessage("UNSUPPORTED_FILE_TYPE", 400, locale);
    }
  }
  return documentActionError(t);
}

function categoryDisplayName(category: DocumentCategory, locale: string) {
  return locale === "ar" ? category.nameAr || category.name || category.code || "" : category.nameEn || category.name || category.code || "";
}

export function ProjectDocumentsTab() {
  const { projectId, project } = useOutletContext<{ projectId: string; project: { id: string; publicId?: string } }>();
  // projectId from the route is the public project identifier expected by the
  // backend PublicId routing boundary. project.id is the internal key used for
  // document relationships and upload payloads.
  return <DocumentCenter projectId={projectId} internalProjectId={project.id} compact />;
}

export function DocumentsCenterPage() {
  return <DocumentCenter compact={false} />;
}

function DocumentCenter({ projectId, internalProjectId, compact }: { projectId?: string; internalProjectId?: string; compact: boolean }) {
  const { t, locale, formatNumber } = useI18n();
  const { user } = useAuth();
  const { canMutate, snapshot } = useManagement();
  const [tab, setTab] = React.useState("documents");
  const [search, setSearch] = React.useState("");
  const [libraryView, setLibraryView] = React.useState<"grid" | "list">("list");
  const [page, setPage] = React.useState(1);
  const [templateStatus, setTemplateStatus] = React.useState("Active");
  const [templateCategoryId, setTemplateCategoryId] = React.useState("");
  const [selectedOrganizationId, setSelectedOrganizationId] = React.useState("");
  const [uploadTemplateOrganizationId, setUploadTemplateOrganizationId] = React.useState("");
  const [uploadTemplateCategoryId, setUploadTemplateCategoryId] = React.useState("");
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [showUpload, setShowUpload] = React.useState(false);
  const [uploadAsTemplate, setUploadAsTemplate] = React.useState(false);
  const [showVersion, setShowVersion] = React.useState(false);
  const [showTemplateStart, setShowTemplateStart] = React.useState(false);
  const [uploadError, setUploadError] = React.useState<string | null>(null);
  const [versionError, setVersionError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [templateStartError, setTemplateStartError] = React.useState<string | null>(null);
  const [selectedTemplateId, setSelectedTemplateId] = React.useState<string | null>(null);
  const [uploadSourceTemplateId, setUploadSourceTemplateId] = React.useState("");
  const canViewDocuments = compact || (!!user && hasPermission(user.permissions, Permissions.Document.View));
  const canCreate = canViewDocuments && canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Create);
  const canEdit = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Edit);
  const canArchive = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.Archive);
  const canTemplate = canMutate && !!user && hasPermission(user.permissions, Permissions.Document.TemplateManage);
  const canViewCategories = !compact && !!user && hasPermission(user.permissions, Permissions.DocumentCategory.View);
  const organizationScope = snapshot?.selectedContext ? snapshot.selectedContext.scope : snapshot?.scope;
  const organizations = React.useMemo(() => {
    if (!organizationScope) return [];
    const allowedIds = new Set(organizationScope.organizationIds ?? []);
    return (organizationScope.organizationReferences ?? []).filter((organization) => allowedIds.has(organization.id));
  }, [organizationScope]);
  const selectedOrganizationIsAllowed = organizations.some((organization) => organization.id === selectedOrganizationId);
  const canUploadTemplate = canCreate && canTemplate && organizations.length > 0;
  const uploadTemplateOrganizationIsAllowed = organizations.some((organization) => organization.id === uploadTemplateOrganizationId);
  React.useEffect(() => {
    setSelectedOrganizationId((current) => {
      if (organizations.some((organization) => organization.id === current)) return current;
      return organizations.length === 1 ? organizations[0].id : "";
    });
  }, [organizations]);
  const query = useProjectDocuments({
    projectId,
    organizationId: !compact ? selectedOrganizationId || undefined : undefined,
    search: search || undefined,
    page,
    pageSize: compact ? 8 : 15,
    templatesOnly: tab === "templates",
    categoryId: !compact ? templateCategoryId || undefined : undefined,
    status: !compact && templateStatus !== "All" ? templateStatus : undefined,
    includeArchived: !compact && templateStatus !== "Active",
  }, canViewDocuments && (Boolean(projectId) || (!compact && selectedOrganizationIsAllowed)));
  const templatesQuery = useProjectDocuments({ page: 1, pageSize: 50, forProjectId: internalProjectId, templatesOnly: true }, canViewDocuments && (showTemplateStart || (showUpload && compact && !uploadAsTemplate)) && Boolean(internalProjectId));
  const managedLibraryCategories = useManagedDocumentCategories({ organizationId: selectedOrganizationId || undefined }, !compact && canViewDocuments && canViewCategories && selectedOrganizationIsAllowed);
  const templateCategoryOptions = useTemplateCategoryOptions(uploadTemplateOrganizationId, !compact && canCreate && canTemplate && showUpload && uploadAsTemplate && uploadTemplateOrganizationIsAllowed);
  const projectUploadCategoryOptions = useProjectUploadCategoryOptions(internalProjectId ?? "", compact && canCreate && showUpload && !uploadAsTemplate && Boolean(internalProjectId));
  const availableLibraryCategories = React.useMemo(() =>
    (managedLibraryCategories.data ?? []).filter((category) => category.isActive === true && (!category.organizationId || category.organizationId === selectedOrganizationId)),
  [managedLibraryCategories.data, selectedOrganizationId]);
  const libraryFilterCategories = availableLibraryCategories;
  const uploadCategoryLookup = uploadAsTemplate ? templateCategoryOptions : projectUploadCategoryOptions;
  const uploadCategories = uploadCategoryLookup.data ?? [];
  const uploadCategoriesLoading = uploadCategoryLookup.isLoading;
  const uploadCategoriesError = uploadCategoryLookup.isError;
  const retryUploadCategories = () => {
    if (uploadAsTemplate) void templateCategoryOptions.refetch();
    else void projectUploadCategoryOptions.refetch();
  };
  const uploadCategoryStatus = uploadCategoriesError
    ? <div role="alert" className="mt-1 flex items-center gap-2 text-xs text-destructive"><span>{t("documents.categoryLoadError")}</span><Button type="button" variant="link" size="sm" className="h-auto p-0 text-xs" onClick={retryUploadCategories}>{t("documents.categoryRetry")}</Button></div>
    : uploadCategoriesLoading
      ? <span role="status" className="mt-1 block text-xs text-muted-foreground">{t("documents.categoryLoading")}</span>
      : uploadAsTemplate && !uploadTemplateOrganizationIsAllowed
        ? <span role="status" className="mt-1 block text-xs text-muted-foreground">{t("documents.categorySelectOrganization")}</span>
        : !uploadCategories.length
          ? <span role="status" className="mt-1 block text-xs text-muted-foreground">{t("documents.categoryNoAvailable")}</span>
          : null;
  const totalsEnabled = !compact && canViewDocuments && selectedOrganizationIsAllowed;
  const libraryTotal = useProjectDocuments({ organizationId: selectedOrganizationId || undefined, page: 1, pageSize: 1, includeArchived: true }, totalsEnabled);
  const activeTemplatesTotal = useProjectDocuments({ organizationId: selectedOrganizationId || undefined, page: 1, pageSize: 1, templatesOnly: true, status: "Active" }, totalsEnabled);
  const folderCounts = useQueries({ queries: availableLibraryCategories.map((category) => ({
    queryKey: ["documents", "library-category-count", selectedOrganizationId, category.id],
    queryFn: () => documentApi.list({ organizationId: selectedOrganizationId, categoryId: category.id, page: 1, pageSize: 1, includeArchived: true }),
    enabled: totalsEnabled, staleTime: 20_000,
  })) });
  const libraryCategories = managedLibraryCategories.data && selectedOrganizationIsAllowed ? availableLibraryCategories.map((category, index) => ({ id: category.id, name: categoryDisplayName(category, locale), count: category.usageCount ?? (folderCounts[index]?.isError ? undefined : folderCounts[index]?.data?.totalCount) })) : undefined;
  const selected = useProjectDocument(canViewDocuments ? selectedId : null);
  const selectedTemplate = useProjectDocument(canViewDocuments ? selectedTemplateId : null);
  const uploadSourceTemplate = useProjectDocument(canViewDocuments ? uploadSourceTemplateId || null : null);
  const archive = useArchiveDocument();
  const activate = useActivateDocument();
  const upload = useUploadDocument();
  const version = useUploadDocumentVersion();

  const items = query.data?.items ?? [];

  const download = async (item: DocumentListItem) => {
    const blob = await documentApi.download(item.id);
    const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = item.fileName || item.name; anchor.click(); URL.revokeObjectURL(url);
  };
  const preview = async (item: DocumentListItem) => {
    const blob = await documentApi.preview(item.id);
    // Defence in depth: only non-executable types open inline; anything else downloads.
    if (!/^application\/pdf\b|^image\/(png|jpeg)\b/i.test(blob.type || "")) {
      const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = item.fileName || item.name; anchor.click(); URL.revokeObjectURL(url);
      return;
    }
    const url = URL.createObjectURL(blob);
    window.open(url, "_blank", "noopener,noreferrer");
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  };
  const downloadVersion = async (item: DocumentListItem, versionNumber: number) => {
    const blob = await documentApi.download(item.id, versionNumber);
    const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = `${item.name}-v${versionNumber}${item.fileName?.includes(".") ? `.${item.fileName.split(".").pop()}` : ""}`; anchor.click(); URL.revokeObjectURL(url);
  };
  const openUpload = (asTemplate: boolean) => {
    setUploadError(null);
    setUploadAsTemplate(asTemplate);
    setUploadSourceTemplateId("");
    setUploadTemplateOrganizationId(asTemplate && selectedOrganizationIsAllowed ? selectedOrganizationId : "");
    setUploadTemplateCategoryId("");
    setShowUpload(true);
  };
  const submitUpload = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const formElement = event.currentTarget; const form = new FormData(formElement); if (internalProjectId) form.set("ProjectId", internalProjectId);
    if (uploadAsTemplate) {
      if (!uploadTemplateOrganizationIsAllowed || !uploadTemplateCategoryId) {
        setUploadError(t("documents.actionFailed"));
        return;
      }
      form.set("IsTemplate", "true");
      form.set("OrganizationId", uploadTemplateOrganizationId);
      form.set("CategoryId", uploadTemplateCategoryId);
    }
    if (compact && internalProjectId && uploadSourceTemplateId) {
      const sourceTemplate = templatesQuery.data?.items.find((item) => item.id === uploadSourceTemplateId);
      const sourceVersion = uploadSourceTemplate.data?.versions.find((item) => item.isCurrent);
      if (!sourceTemplate || !sourceVersion || uploadSourceTemplate.isLoading || uploadSourceTemplate.isError) {
        setUploadError(t("documents.actionFailed"));
        return;
      }
      form.set("IsTemplate", "false");
      form.set("SourceTemplateId", sourceTemplate.id);
      form.set("SourceTemplateVersionId", sourceVersion.id);
      form.set("SourceTemplateVersionNumber", String(sourceVersion.versionNumber));
    }
    setUploadError(null);
    try { await upload.mutateAsync(form); setShowUpload(false); setUploadSourceTemplateId(""); setUploadTemplateOrganizationId(""); setUploadTemplateCategoryId(""); formElement.reset(); }
    catch (error) { setUploadError(uploadErrorMessage(error, t, locale as ApiErrorLocale)); }
  };
  const submitVersion = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); if (!selectedId) return; const formElement = event.currentTarget; const form = new FormData(formElement); setVersionError(null);
    try { await version.mutateAsync({ id: selectedId, data: form }); setShowVersion(false); formElement.reset(); }
    catch (error) { setVersionError(uploadErrorMessage(error, t, locale as ApiErrorLocale)); }
  };
  const submitTemplateStart = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const formElement = event.currentTarget;
    const template = templatesQuery.data?.items.find((item) => item.id === selectedTemplateId);
    const selectedVersion = selectedTemplate.data?.versions.find((templateVersion) => templateVersion.isCurrent);
    if (!template || !internalProjectId || !selectedVersion) return;
    const form = new FormData(formElement);
    form.set("ProjectId", internalProjectId); form.set("IsTemplate", "false"); form.set("SourceTemplateId", template.id); form.set("SourceTemplateVersionId", selectedVersion.id); form.set("SourceTemplateVersionNumber", String(selectedVersion.versionNumber));
    setTemplateStartError(null);
    try { await upload.mutateAsync(form); setShowTemplateStart(false); setSelectedTemplateId(null); formElement.reset(); }
    catch (error) { setTemplateStartError(uploadErrorMessage(error, t, locale as ApiErrorLocale)); }
  };
  const archiveSelected = async (id: string) => {
    setActionError(null);
    try { await archive.mutateAsync(id); setSelectedId(null); }
    catch { setActionError(documentActionError(t)); }
  };
  const activateSelected = async (id: string) => {
    setActionError(null);
    try { await activate.mutateAsync(id); setSelectedId(null); }
    catch { setActionError(documentActionError(t)); }
  };

  const selectedDetail = selected.data;
  const Icon = selectedDetail?.document ? fileIcon(selectedDetail.document.fileType) : FileText;

  return <div className={`nexus-page ${compact ? "space-y-3" : "space-y-4"}`}>
    {compact
      ? <div className="flex flex-col gap-2 rounded-xl border border-border/80 bg-card/90 px-3.5 py-2.5 shadow-2xs sm:flex-row sm:items-center sm:justify-between"><div className="flex items-center gap-2.5"><span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-primary/25 bg-primary/10 text-primary shadow-2xs"><BookOpen className="h-4 w-4" aria-hidden="true" /></span><div><h3 className="text-sm font-bold tracking-tight text-foreground">{t("documents.projectTitle")}</h3><p className="text-[11px] text-muted-foreground">{t("documents.projectDescription")}</p></div></div>{canCreate ? <div className="flex flex-wrap items-center gap-2"><Button size="sm" variant="outline" onClick={() => setShowTemplateStart(true)} className="h-8 text-xs hover:border-primary/40"><BookOpen className="me-1.5 h-3.5 w-3.5 text-muted-foreground" />{t("documents.startFromTemplate")}</Button><Button size="sm" onClick={() => openUpload(false)} className="h-8 text-xs shadow-2xs"><Upload className="me-1.5 h-3.5 w-3.5" />{t("documents.upload")}</Button></div> : null}</div>
      : <DocumentLibraryHeader canUpload={canUploadTemplate} canManageCategories={canViewCategories} onUpload={() => openUpload(true)} />}
    {!compact && canViewDocuments ? <DocumentLibraryOverview totalDocuments={libraryTotal.isError ? undefined : libraryTotal.data?.totalCount} activeTemplates={activeTemplatesTotal.isError ? undefined : activeTemplatesTotal.data?.totalCount} categories={libraryCategories} loadingCategories={managedLibraryCategories.isLoading} selectedCategoryId={templateCategoryId} folderSearch={search} onSelectCategory={(id) => { setTemplateCategoryId((current) => current === id ? "" : id); if (search && availableLibraryCategories.some((category) => category.id === id && categoryDisplayName(category, locale).toLocaleLowerCase(locale).includes(search.trim().toLocaleLowerCase(locale)))) setSearch(""); setPage(1); }} /> : null}

    <Tabs value={tab} onValueChange={(value) => { setTab(value); setPage(1); }}>
      <div className="flex flex-col gap-2 rounded-xl border border-border/70 bg-card/70 p-2 shadow-2xs backdrop-blur-xs sm:flex-row sm:items-center sm:justify-between"><TabsList className="h-8 bg-muted/60 p-0.5">{canViewDocuments ? <><TabsTrigger value="documents" className="h-7 px-3 text-xs font-semibold data-[state=active]:shadow-2xs">{t(compact ? "documents.tabs.documents" : "documents.libraryTitle")}</TabsTrigger>{compact ? <TabsTrigger value="templates" className="h-7 px-3 text-xs font-semibold data-[state=active]:shadow-2xs">{t("documents.tabs.templates")}</TabsTrigger> : null}</> : null}</TabsList><div className="flex flex-wrap items-center gap-2"><div className="relative min-w-40 flex-1 sm:w-56 sm:flex-none"><Search className="absolute start-2.5 top-2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" /><Input aria-label={t(compact ? "documents.search" : "documents.librarySearch")} value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder={t(compact ? "documents.search" : "documents.librarySearch")} className="h-8 rounded-lg border-border/80 bg-background/90 ps-8 pe-7 text-xs shadow-2xs" />{search ? <button type="button" onClick={() => { setSearch(""); setPage(1); }} className="absolute end-2 top-2 text-muted-foreground hover:text-foreground"><X className="h-3.5 w-3.5" /></button> : null}</div>{!compact ? <select aria-label={t("documents.fields.organization")} value={selectedOrganizationId} onChange={(event) => { setSelectedOrganizationId(event.target.value); setTemplateCategoryId(""); setPage(1); }} className="flex h-8 rounded-lg border border-input bg-background/90 px-2.5 text-xs font-medium shadow-2xs transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring sm:w-auto" disabled={!organizations.length}><option value="">{organizations.length ? t("documents.organizationSelect") : t("documents.organizationsUnavailable")}</option>{organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}</select> : null}{!compact ? <><select aria-label={t("documents.statusFilter")} value={templateStatus} onChange={(event) => { setTemplateStatus(event.target.value); setPage(1); }} className="flex h-8 rounded-lg border border-input bg-background/90 px-2.5 text-xs font-medium shadow-2xs transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring sm:w-auto"><option value="Active">{t("documents.status.active")}</option><option value="Archived">{t("documents.status.archived")}</option><option value="All">{t("documents.status.all")}</option></select><select aria-label={t("documents.categoryFilter")} value={templateCategoryId} onChange={(event) => { setTemplateCategoryId(event.target.value); setPage(1); }} className="flex h-8 rounded-lg border border-input bg-background/90 px-2.5 text-xs font-medium shadow-2xs transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring sm:w-auto"><option value="">{t("documents.categoryAll")}</option>{libraryFilterCategories.map((category) => <option key={category.id} value={category.id}>{categoryDisplayName(category, locale)}</option>)}</select></> : null}</div></div>
      {canViewDocuments ? <>
        <TabsContent value="documents" className="mt-3">{compact ? <DocumentTable items={items} loading={query.isLoading} error={query.isError} emptyTitle={t("documents.emptyTitle")} emptyDescription={t("documents.emptyDescription")} onRetry={() => void query.refetch()} onOpen={(id) => { setActionError(null); setSelectedId(id); }} onDownload={(item) => void download(item)} locale={locale} /> : !selectedOrganizationIsAllowed ? <EmptyState icon={<Building2 className="h-9 w-9 text-muted-foreground/60" />} title={organizations.length ? t("documents.organizationRequired") : t("documents.organizationsUnavailable")} description={organizations.length ? t("documents.organizationRequiredDescription") : t("documents.organizationsUnavailableDescription")} /> : <><div className="mb-2.5 flex flex-wrap items-center justify-between gap-2"><div className="flex items-center gap-2"><h2 className="text-sm font-bold tracking-tight text-foreground">{t("documents.recentDocuments")}</h2>{query.data?.totalCount !== undefined ? <span className="rounded-full bg-muted px-1.5 py-0.2 text-[10px] font-bold tabular-nums text-muted-foreground">{formatNumber(query.data.totalCount)}</span> : null}</div><div className="flex flex-wrap items-center gap-1.5"><Button variant="ghost" size="sm" onClick={() => { setSearch(""); setTemplateCategoryId(""); setTemplateStatus("Active"); setPage(1); }} className="h-7 px-2 text-[11px] font-medium text-muted-foreground hover:text-foreground">{t("documents.clearFilters")}</Button><div className="inline-flex items-center rounded-lg border border-border/80 bg-muted/40 p-0.5 shadow-2xs"><Button variant={libraryView === "grid" ? "default" : "outline"} size="sm" aria-pressed={libraryView === "grid"} onClick={() => setLibraryView("grid")} className={`h-6 px-2 text-[11px] font-semibold transition-all ${libraryView === "grid" ? "shadow-2xs" : "border-transparent bg-transparent hover:bg-background/60"}`}><LayoutGrid className="me-1 h-3 w-3" aria-hidden="true" />{t("documents.gridView")}</Button><Button variant={libraryView === "list" ? "default" : "outline"} size="sm" aria-pressed={libraryView === "list"} onClick={() => setLibraryView("list")} className={`h-6 px-2 text-[11px] font-semibold transition-all ${libraryView === "list" ? "shadow-2xs" : "border-transparent bg-transparent hover:bg-background/60"}`}><List className="me-1 h-3 w-3" aria-hidden="true" />{t("documents.listView")}</Button></div></div></div><LibraryDocuments items={items.map((item) => ({ ...item, categoryName: availableLibraryCategories.find((category) => category.id === item.categoryId) ? categoryDisplayName(availableLibraryCategories.find((category) => category.id === item.categoryId)!, locale) : item.categoryName }))} view={libraryView} loading={query.isLoading} error={query.isError} filtered={!!search || !!templateCategoryId} onRetry={() => void query.refetch()} onOpen={(id) => { setActionError(null); setSelectedId(id); }} onDownload={(item) => void download(item)} /></>}<DocumentPagination query={query.data} page={page} setPage={setPage} t={t} formatNumber={formatNumber} compact={compact} /></TabsContent>
        <TabsContent value="templates" className="mt-3">{!compact && canUploadTemplate ? <div className="mb-3 flex justify-end"><Button size="sm" onClick={() => openUpload(true)}><Upload className="me-2 h-4 w-4" />{t("documents.uploadTemplate")}</Button></div> : null}{!compact && !selectedOrganizationIsAllowed ? <EmptyState icon={<Building2 className="h-10 w-10" />} title={organizations.length ? t("documents.organizationRequired") : t("documents.organizationsUnavailable")} description={organizations.length ? t("documents.organizationRequiredDescription") : t("documents.organizationsUnavailableDescription")} /> : <><DocumentTable items={items} loading={query.isLoading} error={query.isError} emptyTitle={t("documents.templatesEmptyTitle")} emptyDescription={t("documents.templatesEmptyDescription")} onRetry={() => void query.refetch()} onOpen={(id) => { setActionError(null); setSelectedId(id); }} onDownload={(item) => void download(item)} locale={locale} templateMode={!compact} t={t} /><DocumentPagination query={query.data} page={page} setPage={setPage} t={t} formatNumber={formatNumber} compact={compact} /></>}</TabsContent>
      </> : null}
    </Tabs>
    <SideSheet open={!!selectedId} onOpenChange={(open) => { if (!open) setSelectedId(null); }} title={selectedDetail?.document.name || t("documents.details")} description={selectedDetail?.document.fileName || t("documents.detailsDescription")} size="lg"><SideSheetBody>{actionError ? <MutationError message={actionError} /> : null}{selected.isLoading ? <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : selectedDetail ? <DocumentDetails detail={selectedDetail} icon={Icon} locale={locale} t={t} canEdit={canEdit && (!selectedDetail.document.isTemplate || canTemplate)} canArchive={canArchive && (!selectedDetail.document.isTemplate || canTemplate)} canActivate={canEdit && canTemplate && selectedDetail.document.isArchived} onDownload={() => void download(selectedDetail.document)} onPreview={() => void preview(selectedDetail.document)} onVersion={() => { setVersionError(null); setShowVersion(true); }} onDownloadVersion={(versionNumber) => void downloadVersion(selectedDetail.document, versionNumber)} onArchive={() => { void archiveSelected(selectedDetail.document.id); }} onActivate={() => { void activateSelected(selectedDetail.document.id); }} /> : null}</SideSheetBody></SideSheet>
    <SideSheet open={showUpload} onOpenChange={(open) => { setShowUpload(open); if (!open) setUploadSourceTemplateId(""); }} title={t(uploadAsTemplate ? "documents.uploadTemplateTitle" : "documents.uploadTitle")} description={projectId ? t("documents.projectUploadDescription") : uploadAsTemplate ? t("documents.templateUploadDescription") : t("documents.uploadDescription")}><form onSubmit={(event) => void submitUpload(event)}><SideSheetBody className="space-y-4">{uploadError ? <MutationError message={uploadError} /> : null}{uploadAsTemplate ? <><p className="rounded-md border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{t("documents.externalApprovalWarning")}</p><label className="block text-sm font-medium">{t("documents.fields.organization")}<select name="OrganizationId" value={uploadTemplateOrganizationId} onChange={(event) => { setUploadTemplateOrganizationId(event.target.value); setUploadTemplateCategoryId(""); }} required className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="">{t("documents.organizationSelect")}</option>{organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}</select></label><label className="block text-sm font-medium">{t("documents.fields.category")}<select name="CategoryId" value={uploadTemplateCategoryId} onChange={(event) => setUploadTemplateCategoryId(event.target.value)} required className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" disabled={!uploadTemplateOrganizationIsAllowed || uploadCategoriesLoading || uploadCategoriesError || !uploadCategories.length}><option value="">{t("documents.chooseCategory")}</option>{uploadCategories.map((category) => <option key={category.id} value={category.id}>{categoryDisplayName(category, locale)}</option>)}</select></label>{uploadCategoryStatus}</> : null}<label className="block text-sm font-medium">{t("documents.fields.name")}<Input name="Name" className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.description")}<Input name="Description" className="mt-1" /></label>{uploadAsTemplate ? <label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" name="IsMandatory" value="true" className="h-4 w-4 rounded border-input" />{t("documents.fields.mandatoryDocument")}</label> : null}{!uploadAsTemplate ? <><label className="block text-sm font-medium">{t("documents.fields.category")}<select name="CategoryId" className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" disabled={uploadCategoriesLoading || uploadCategoriesError || !uploadCategories.length}><option value="">{t("documents.chooseCategory")}</option>{uploadCategories.map((category) => <option key={category.id} value={category.id}>{categoryDisplayName(category, locale)}</option>)}</select></label>{uploadCategoryStatus}</> : null}{compact && internalProjectId && !uploadAsTemplate ? <label className="block text-sm font-medium">{t("documents.sourceTemplate")}<select value={uploadSourceTemplateId} onChange={(event) => setUploadSourceTemplateId(event.target.value)} className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" disabled={templatesQuery.isLoading || templatesQuery.isError}><option value="">{t("documents.sourceTemplateOptional")}</option>{templatesQuery.data?.items.filter((template) => !template.isArchived).map((template) => <option key={template.id} value={template.id}>{template.name} · {template.categoryName || t("documents.categoryFallback")} · v{template.version}</option>)}</select>{templatesQuery.isLoading ? <span className="mt-1 block text-xs text-muted-foreground">{t("common.loading")}</span> : null}{templatesQuery.isError || uploadSourceTemplate.isError ? <span className="mt-1 block text-xs text-destructive">{t("documents.loadError")}</span> : null}{uploadSourceTemplateId && uploadSourceTemplate.data ? <span className="mt-1 block text-xs text-muted-foreground">{t("documents.templateVersionSource", { version: uploadSourceTemplate.data.versions.find((item) => item.isCurrent)?.versionNumber ?? uploadSourceTemplate.data.document.version })}</span> : null}{uploadSourceTemplateId && !uploadSourceTemplate.isLoading && uploadSourceTemplate.data && !uploadSourceTemplate.data.versions.some((item) => item.isCurrent) ? <span className="mt-1 block text-xs text-destructive">{t("documents.unavailable")}</span> : null}</label> : null}<label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="File" type="file" className="mt-1" required /></label></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setShowUpload(false)}>{t("documents.cancel")}</Button><Button type="submit" disabled={upload.isPending || (uploadAsTemplate && (!uploadTemplateOrganizationIsAllowed || !uploadTemplateCategoryId)) || Boolean(uploadSourceTemplateId && (uploadSourceTemplate.isLoading || !uploadSourceTemplate.data?.versions.some((item) => item.isCurrent)))}>{upload.isPending ? t("documents.saving") : t(uploadAsTemplate ? "documents.uploadTemplate" : "documents.upload")}</Button></SideSheetFooter></form></SideSheet>
    <SideSheet open={showVersion} onOpenChange={setShowVersion} title={t("documents.newVersionTitle")}><form onSubmit={(event) => void submitVersion(event)}><SideSheetBody className="space-y-4">{versionError ? <MutationError message={versionError} /> : null}<label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="file" type="file" className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.changeNotes")}<Input name="changeNotes" className="mt-1" /></label></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setShowVersion(false)}>{t("documents.cancel")}</Button><Button type="submit" disabled={version.isPending}>{t("documents.saveVersion")}</Button></SideSheetFooter></form></SideSheet>
    <SideSheet open={showTemplateStart} onOpenChange={(open) => { setShowTemplateStart(open); if (!open) { setSelectedTemplateId(null); setTemplateStartError(null); } }} title={t("documents.startFromTemplate")} description={t("documents.startFromTemplateDescription")} size="lg"><SideSheetBody className="space-y-4">{templatesQuery.isLoading ? <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : templatesQuery.isError ? <ErrorState title={t("documents.loadError")} onRetry={() => void templatesQuery.refetch()} /> : !templatesQuery.data?.items.length ? <EmptyState icon={<BookOpen className="h-10 w-10" />} title={t("documents.templatesEmptyTitle")} description={t("documents.templatesEmptyDescription")} /> : <div className="space-y-2">{templatesQuery.data.items.map((template) => { const isSelected = template.id === selectedTemplateId; const TemplateIcon = fileIcon(template.fileType); return <button key={template.id} type="button" onClick={() => { setTemplateStartError(null); setSelectedTemplateId(template.id); }} className={`flex w-full items-center gap-3 rounded-lg border p-3 text-start transition-colors ${isSelected ? "border-primary bg-primary/5" : "border-border hover:bg-muted/30"}`}><TemplateIcon className="h-5 w-5 shrink-0 text-primary" /><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{template.name}</span><span className="block text-xs text-muted-foreground">{template.fileName} · v{template.version}</span></span>{isSelected ? <Check className="h-4 w-4 text-primary" /> : null}</button>; })}</div>}{(() => { const template = templatesQuery.data?.items.find((item) => item.id === selectedTemplateId); if (!template) return null; if (selectedTemplate.isError) return <ErrorState title={t("documents.loadError")} onRetry={() => void selectedTemplate.refetch()} />; const hasCurrentVersion = Boolean(selectedTemplate.data?.versions.some((templateVersion) => templateVersion.isCurrent)); return <TemplateStartForm key={selectedTemplateId} template={template} t={t} onDownload={() => void download(template)} onSubmit={submitTemplateStart} pending={upload.isPending || selectedTemplate.isLoading || !hasCurrentVersion} error={templateStartError} />; })()}</SideSheetBody></SideSheet>
  </div>;
}

function documentStatusLabel(status: string | undefined, isArchived: boolean, t: (key: string, values?: Record<string, string | number>) => string) {
  if (isArchived) return t("documents.status.archived");
  if (status?.toLowerCase() === "active") return t("documents.status.active");
  return status || t("documents.unavailable");
}

function DocumentTable({ items, loading, error, emptyTitle, emptyDescription, onRetry, onOpen, onDownload, locale, templateMode = false, t }: {
  items: readonly DocumentListItem[];
  loading: boolean;
  error: boolean;
  emptyTitle: string;
  emptyDescription: string;
  onRetry: () => void;
  onOpen: (id: string) => void;
  onDownload: (item: DocumentListItem) => void;
  locale: string;
  templateMode?: boolean;
  t?: (key: string, values?: Record<string, string | number>) => string;
}) {
  const { t: localizedT } = useI18n();
  const translate = t ?? localizedT;
  if (loading) return <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div>;
  if (error) return <ErrorState title={translate("documents.loadError")} onRetry={onRetry} />;
  if (!items.length) return <EmptyState icon={<FileText className="h-10 w-10" />} title={emptyTitle} description={emptyDescription} />;

  if (templateMode) return <div className="nexus-table-surface overflow-hidden">
    <div className="nexus-table-header hidden grid-cols-12 gap-2 px-3 py-1.5 text-center sm:grid">
      <div className="col-span-3 text-start">{translate("documents.table.name")}</div>
      <div className="col-span-2 text-start">{translate("documents.table.description")}</div>
      <div className="col-span-2">{translate("documents.table.category")}</div>
      <div className="col-span-1">{translate("documents.table.version")}</div>
      <div className="col-span-1">{translate("documents.table.status")}</div>
      <div className="col-span-2">{translate("documents.table.organization")}</div>
      <div className="col-span-1" />
    </div>
    {items.map((item) => {
      const Icon = fileIcon(item.fileType);
      return <div key={item.id} className="nexus-table-row grid grid-cols-2 items-start gap-2.5 px-3 py-2 text-xs sm:grid-cols-12 sm:items-center sm:gap-2">
        <div className="col-span-2 min-w-0 sm:col-span-3">
          <button type="button" onClick={() => onOpen(item.id)} className="flex min-w-0 items-center gap-2 text-start hover:text-primary transition-colors">
            <Icon className="h-3.5 w-3.5 shrink-0 text-primary" />
            <span className="min-w-0"><span className="block truncate font-bold text-foreground hover:text-primary">{item.name}</span><span className="block truncate text-[11px] text-muted-foreground sm:hidden">{item.description || translate("documents.unavailable")}</span></span>
          </button>
        </div>
        <div className="hidden min-w-0 truncate text-[11px] text-muted-foreground sm:col-span-2 sm:block">{item.description || translate("documents.unavailable")}</div>
        <div className="min-w-0 text-[11px] sm:col-span-2 sm:text-center font-medium"><span className="me-1 text-muted-foreground sm:hidden">{translate("documents.table.category")}:</span>{item.categoryName || translate("documents.categoryUnavailable")}</div>
        <div className="text-[11px] tabular-nums font-semibold sm:col-span-1 sm:text-center"><span className="me-1 text-muted-foreground sm:hidden">{translate("documents.table.version")}:</span>v{item.version}</div>
        <div className={`text-[11px] font-bold sm:col-span-1 sm:text-center ${getStatusTextClass(item.status || "Unknown")}`}><span className="me-1 text-muted-foreground sm:hidden">{translate("documents.table.status")}:</span>{documentStatusLabel(item.status, item.isArchived, translate)}</div>
        <div className="col-span-2 min-w-0 truncate text-[11px] text-muted-foreground sm:col-span-2 sm:text-center font-medium"><span className="me-1 sm:hidden">{translate("documents.table.organization")}:</span>{item.organizationName || translate("documents.organizationUnavailable")}</div>
        <div className="col-span-2 flex justify-end sm:col-span-1"><Button variant="ghost" size="icon" className="h-7 w-7 rounded-md hover:bg-primary/10 hover:text-primary" aria-label={translate("documents.download")} onClick={() => onDownload(item)}><Download className="h-3.5 w-3.5" /></Button></div>
      </div>;
    })}
  </div>;

  return <div className="nexus-table-surface overflow-hidden">
    <div className="nexus-table-header grid grid-cols-12 gap-2 px-3 py-1.5 text-center"><div className="col-span-5 text-start">{translate("documents.table.name")}</div><div className="col-span-2">{translate("documents.table.type")}</div><div className="col-span-2">{translate("documents.table.size")}</div><div className="col-span-2">{translate("documents.table.updated")}</div><div className="col-span-1" /></div>
    {items.map((item) => { const Icon = fileIcon(item.fileType); return <div key={item.id} className="nexus-table-row grid grid-cols-12 items-center gap-2 px-3 py-2 text-xs"><button type="button" onClick={() => onOpen(item.id)} className="col-span-5 flex min-w-0 items-center gap-2 text-start hover:text-primary transition-colors"><Icon className="h-3.5 w-3.5 shrink-0 text-primary" /><span className="truncate font-bold text-foreground hover:text-primary">{item.name}</span></button><div className={`col-span-2 text-center text-[11px] font-semibold ${getLookupTextClass(item.fileType)}`}>{item.fileType || "-"}</div><div className="col-span-2 text-center text-[11px] text-muted-foreground tabular-nums font-medium">{formatFileSize(item.fileSize, locale)}</div><div className="col-span-2 text-center text-[11px] text-muted-foreground">{formatDate(item.updatedAt || item.createdAt, locale)}</div><div className="col-span-1 flex justify-end"><Button variant="ghost" size="icon" className="h-7 w-7 rounded-md hover:bg-primary/10 hover:text-primary" aria-label={translate("documents.download")} onClick={() => onDownload(item)}><Download className="h-3.5 w-3.5" /></Button></div></div>; })}
  </div>;
}
function TemplateStartForm({ template, t, onDownload, onSubmit, pending, error }: {
  template: DocumentListItem;
  t: (key: string, values?: Record<string, string | number>) => string;
  onDownload: () => void;
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
  pending: boolean;
  error: string | null;
}) {
  return <div className="rounded-lg border border-primary/20 bg-primary/[0.04] p-4"><div className="flex flex-wrap items-center justify-between gap-2"><div><p className="text-sm font-semibold">{template.name}</p><p className="mt-1 text-xs text-muted-foreground">{t("documents.templateVersionSource", { version: template.version })}</p></div><Button type="button" variant="outline" size="sm" onClick={onDownload}><Download className="me-2 h-4 w-4" />{t("documents.downloadBlank")}</Button></div><form className="mt-4 space-y-3" onSubmit={onSubmit}>{error ? <MutationError message={error} /> : null}<label className="block text-sm font-medium">{t("documents.fields.name")}<Input name="Name" defaultValue={template.name} className="mt-1" required /></label><label className="block text-sm font-medium">{t("documents.fields.description")}<Input name="Description" defaultValue={t("documents.createdFromTemplate", { name: template.name })} className="mt-1" /></label><label className="block text-sm font-medium">{t("documents.fields.file")}<Input name="File" type="file" className="mt-1" required /></label><Button type="submit" disabled={pending} className="w-full">{pending ? t("documents.saving") : t("documents.createFromTemplate")}</Button></form></div>;
}
function DocumentDetails({ detail, icon: Icon, t, locale, canEdit, canArchive, canActivate, onDownload, onPreview, onVersion, onDownloadVersion, onArchive, onActivate }: {
  detail: DocumentDetail;
  icon: typeof File;
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
  canEdit: boolean;
  canArchive: boolean;
  canActivate: boolean;
  onDownload: () => void;
  onPreview: () => void;
  onVersion: () => void;
  onDownloadVersion: (versionNumber: number) => void;
  onArchive: () => void;
  onActivate: () => void;
}) {
  const item = detail.document;
  const previewable = item.contentType?.startsWith("image/") || item.contentType === "application/pdf";
  const fileType = item.fileType?.toUpperCase() || "";
  const fileColor = fileType.includes("PDF")
    ? "text-red-600 dark:text-red-400"
    : fileType.includes("XLS")
      ? "text-green-600 dark:text-green-400"
      : fileType.includes("DOC")
        ? "text-blue-600 dark:text-blue-400"
        : "text-primary";
  const categoryLabel = item.categoryId
    ? (locale === "ar" ? (item.categoryNameAr || item.categoryName) : item.categoryName) || t("documents.categoryUnavailable")
    : t("documents.categoryUnavailable");
  const statusLabel = documentStatusLabel(item.status, item.isArchived, t);
  return (
    <div className="space-y-4">
      <section className="flex items-start gap-3 rounded-xl border border-border bg-card p-3 shadow-2xs">
        <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border bg-muted/40 ${fileColor}`}>
          <Icon className="h-5 w-5" aria-hidden="true" />
        </div>
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-bold text-foreground">{item.name}</h3>
          <p className="truncate text-[11px] text-muted-foreground">{item.fileName || t("documents.unavailable")}</p>
          <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
            <Badge variant="outline" className="text-[10px] tabular-nums font-semibold">v{item.version}</Badge>
            <span className={`text-[11px] font-semibold ${item.isArchived ? "text-muted-foreground" : "text-green-600 dark:text-green-400"}`}>{statusLabel}</span>
            <Badge variant={item.isMandatory ? "default" : "outline"} className={item.isMandatory ? "bg-amber-500 text-white hover:bg-amber-500/90 text-[10px] font-semibold" : "text-[10px]"}>
              {item.isMandatory ? t("documents.mandatory") : t("documents.optional")}
            </Badge>
            <span className={`text-[11px] font-medium ${getLookupTextClass(item.fileType)}`}>{item.fileType || t("documents.unavailable")}</span>
          </div>
        </div>
      </section>

      <section className="grid gap-2 sm:grid-cols-2">
        <div className="rounded-lg border border-border bg-card/50 p-2.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.fields.description")}</p>
          <p className="mt-0.5 text-xs text-foreground font-medium">{item.description || t("documents.unavailable")}</p>
        </div>
        <div className="rounded-lg border border-border bg-card/50 p-2.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.fields.category")}</p>
          <p className="mt-0.5 text-xs text-foreground font-semibold">{categoryLabel}</p>
        </div>
        <div className="rounded-lg border border-border bg-card/50 p-2.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.table.type")}</p>
          <p className="mt-0.5 text-xs text-foreground font-medium">{item.fileType || t("documents.unavailable")} ({previewable ? t("documents.preview") : "File"})</p>
        </div>
        <div className="rounded-lg border border-border bg-card/50 p-2.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.table.size")}</p>
          <p className="mt-0.5 text-xs text-foreground font-medium tabular-nums">{formatFileSize(item.fileSize, locale)}</p>
        </div>
        {item.isTemplate ? (
          <div className="rounded-lg border border-border bg-card/50 p-2.5 sm:col-span-2">
            <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.fields.organization")}</p>
            <p className="mt-0.5 text-xs text-foreground font-semibold">{item.organizationName || t("documents.organizationUnavailable")}</p>
          </div>
        ) : (
          <div className="rounded-lg border border-border bg-card/50 p-2.5 sm:col-span-2">
            <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.fields.project")}</p>
            <p className="mt-0.5 text-xs text-foreground font-semibold">{item.projectId || t("documents.globalScope")}</p>
          </div>
        )}
      </section>

      <section className="grid gap-2 sm:grid-cols-2">
        {item.sourceTemplateId ? (
          <div className="rounded-lg border border-primary/20 bg-primary/[0.04] p-2.5 sm:col-span-2">
            <p className="text-[10px] font-semibold uppercase tracking-wide text-primary">{t("documents.sourceTemplate")}</p>
            <p className="mt-0.5 text-xs text-foreground font-medium">{item.sourceTemplateId}</p>
            <p className="mt-0.5 text-[11px] text-muted-foreground">{t("documents.templateVersionSource", { version: item.sourceTemplateVersionNumber ?? 1 })}</p>
          </div>
        ) : null}
      </section>

      <section className="space-y-1.5">
        <div className="flex items-center gap-1.5">
          <History className="h-3.5 w-3.5 text-muted-foreground" aria-hidden="true" />
          <p className="text-xs font-bold text-foreground">{t("documents.versions")}</p>
        </div>
        <div className="space-y-1.5">
          {detail.versions.map((v) => (
            <div key={v.id} className="flex items-center justify-between gap-2.5 rounded-lg border border-border bg-card/50 px-2.5 py-1.5 text-xs">
              <div className="min-w-0">
                <p className="font-semibold text-foreground">
                  v{v.versionNumber}{v.isCurrent ? ` · ${t("documents.current")}` : ""}
                </p>
                <p className="truncate text-[11px] text-muted-foreground">
                  {v.fileName} · {formatFileSize(v.fileSize, locale)}{v.uploadedBy ? ` · ${v.uploadedBy}` : ""}
                </p>
              </div>
              <Button variant="ghost" size="sm" onClick={() => onDownloadVersion(v.versionNumber)} aria-label={t("documents.download")} className="h-7 w-7 p-0">
                <Download className="h-3.5 w-3.5" />
              </Button>
            </div>
          ))}
        </div>
      </section>

      <section className="grid gap-2 sm:grid-cols-2">
        <div className="rounded-lg border border-border bg-card/50 p-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.created")}</p>
          <p className="mt-0.5 text-xs text-foreground font-medium">{formatDate(item.createdAt, locale)}</p>
        </div>
        <div className="rounded-lg border border-border bg-card/50 p-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("documents.updated")}</p>
          <p className="mt-0.5 text-xs text-foreground font-medium">{formatDate(item.updatedAt || item.createdAt, locale)}</p>
        </div>
      </section>

      <div className="flex flex-wrap items-center gap-2 border-t border-border pt-3">
        <Button onClick={onDownload} size="sm" className="h-8 text-xs">
          <Download className="me-1.5 h-3.5 w-3.5" />{t("documents.download")}
        </Button>
        {previewable ? (
          <Button variant="outline" size="sm" onClick={onPreview} className="h-8 text-xs">{t("documents.preview")}</Button>
        ) : null}
        {canEdit && !item.isArchived ? (
          <Button variant="outline" size="sm" onClick={onVersion} className="h-8 text-xs">
            <Upload className="me-1.5 h-3.5 w-3.5" />{t("documents.newVersion")}
          </Button>
        ) : null}
        {canArchive && !item.isArchived ? (
          <Button variant="ghost" size="sm" className="h-8 text-xs text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={onArchive}>
            <Archive className="me-1.5 h-3.5 w-3.5" />{t("documents.archive")}
          </Button>
        ) : null}
        {canActivate && item.isArchived ? (
          <Button variant="outline" size="sm" onClick={onActivate} className="h-8 text-xs text-emerald-600 hover:border-emerald-500 hover:bg-emerald-500/10">
            <Check className="me-1.5 h-3.5 w-3.5" />{t("documents.activate")}
          </Button>
        ) : null}
      </div>
    </div>
  );
}
function DocumentPagination({ query, page, setPage, t, formatNumber, compact }: { query?: { totalPages: number; totalCount: number; pageSize: number }; page: number; setPage: (page: number) => void; t: (key: string, values?: Record<string, string | number>) => string; formatNumber: (value: number | bigint, options?: Intl.NumberFormatOptions) => string; compact: boolean }) {
  if (!query || query.totalCount === 0) return null;
  return <div className="mt-3 flex flex-wrap items-center justify-between gap-2"><p className="text-xs text-muted-foreground">{t("documents.pagination", { count: formatNumber(query.totalCount) })}{compact ? ` · ${t("documents.compactFooter")}` : ""}</p><Pagination page={page} totalPages={query.totalPages} total={query.totalCount} pageSize={query.pageSize} onChange={setPage} /></div>;
}
