"use client";

import { useParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { ProjectSnapshotView } from "@/components/projects/ProjectSnapshotView";

export default function ProjectSnapshotDetailPage() {
  const t = useTranslations("projects");
  const params = useParams();
  const projectId = params.id as string;
  const snapshotId = params.snapshotId as string;

  return (
    <div className="space-y-6">
      <PageHeader title={t("snapshot")} description={t("snapshotsDescription")} />
      <ProjectSnapshotView projectId={projectId} snapshotId={snapshotId} />
    </div>
  );
}
