import { useOutletContext } from "react-router-dom";
import { ProjectWorkflowSettings } from "./ProjectWorkflowSettings";

export function ProjectWorkflowsTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  // Visible-first: settings load only when tab active (inside component).
  return <ProjectWorkflowSettings projectId={projectId} />;
}
