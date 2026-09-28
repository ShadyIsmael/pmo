"use client";

import { useParams } from "next/navigation";
import { WorkflowDesignerWorkspace } from "@/components/workflows/WorkflowManagementWorkspace";

export default function WorkflowVersionPage() {
  const params = useParams<{ id: string; versionId: string }>();
  return <WorkflowDesignerWorkspace definitionId={params.id} versionId={params.versionId} />;
}
