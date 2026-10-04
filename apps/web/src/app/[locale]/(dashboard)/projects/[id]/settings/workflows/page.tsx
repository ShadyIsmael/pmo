"use client";

import { useParams } from "next/navigation";
import { ProjectWorkflowSettings } from "@/components/projects/ProjectWorkflowSettings";

export default function ProjectWorkflowSettingsPage() {
  const params = useParams<{ id: string }>();
  return <ProjectWorkflowSettings projectId={params.id} />;
}
