"use client"

import { useParams, useSearchParams } from "next/navigation"
import { PlanningWorkspace } from "@/components/planning/PlanningWorkspace"
import { useEffect } from "react"

export default function ProjectPlanPage() {
  const params = useParams()
  const searchParams = useSearchParams()

  useEffect(() => {
    document.title = `Plan - Core Banking Modernisation — NEXUS PMO`
  }, [])

  return (
    <div className="flex h-[calc(100vh-3rem)] flex-col">
      <PlanningWorkspace />
    </div>
  )
}
