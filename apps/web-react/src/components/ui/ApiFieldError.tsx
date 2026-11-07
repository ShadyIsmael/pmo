import { getApiFieldError } from "@api/client";

export function ApiFieldError({
  error,
  fieldName,
  id,
}: {
  error: unknown;
  fieldName: string;
  id?: string;
}) {
  const message = getApiFieldError(error, fieldName);
  return message ? <p id={id} role="alert" className="text-xs text-destructive">{message}</p> : null;
}
