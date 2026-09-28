"use client"

import { useParams } from "next/navigation"
import dynamic from "next/dynamic"

const LazyProjectPlanningWorkspace = dynamic(
  () => import("@/components/projects/ProjectPlanningWorkspace").then((mod) => mod.ProjectPlanningWorkspace),
  { ssr: false }
)

export default function ProjectPlanPage() {
  const params = useParams()
  return (
    <div className="flex min-h-[calc(100vh-8rem)] flex-col">
      <LazyProjectPlanningWorkspace projectId={params.id as string} />
    </div>
  )
}
