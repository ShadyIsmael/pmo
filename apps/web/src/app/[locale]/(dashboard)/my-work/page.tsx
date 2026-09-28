import { redirect } from "next/navigation";

export default function MyWorkRedirect({ params }: { params: { locale: string } }) {
  redirect(`/${params.locale}/my-work/tasks`);
}
