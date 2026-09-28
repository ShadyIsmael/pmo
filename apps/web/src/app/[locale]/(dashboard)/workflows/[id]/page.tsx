"use client";

import { useParams } from "next/navigation";
import { WorkflowDefinitionWorkspace } from "@/components/workflows/WorkflowManagementWorkspace";

export default function WorkflowDefinitionPage() {
  const params = useParams<{ id: string }>();
  return <WorkflowDefinitionWorkspace definitionId={params.id} />;
}
