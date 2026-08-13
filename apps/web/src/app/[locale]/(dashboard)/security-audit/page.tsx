import { redirect } from "next/navigation";

export default function SecurityAuditRedirect() {
  redirect("/en/security-audit/audit-log");
}
