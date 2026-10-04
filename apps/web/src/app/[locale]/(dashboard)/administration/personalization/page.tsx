import { redirect } from "next/navigation";

export default function PersonalizationRedirectPage() {
  redirect("/account/preferences");
}
