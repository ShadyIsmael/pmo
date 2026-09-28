"use client";

import { useParams } from "next/navigation";
import { RiskDetailsView } from "@/components/risks/RiskDetailsView";

export default function RiskDetailsPage() {
  const params = useParams<{ id: string }>();
  return <RiskDetailsView riskId={params.id} />;
}
