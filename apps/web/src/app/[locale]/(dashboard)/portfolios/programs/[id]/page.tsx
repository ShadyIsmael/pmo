"use client";

import { useParams } from "next/navigation";
import { ProgramManagementWorkspace } from "@/components/programs/ProgramManagementWorkspace";

export default function ProgramDetailPage() {
  const params = useParams<{ id: string }>();
  return <ProgramManagementWorkspace programId={params.id} />;
}
