# Validation Error Convention

The API is the authoritative enforcement layer for validation and business rules. React improves the presentation of those failures; client-side validation must not replace server validation or change business rules.

## Response contract

All failed API responses use Problem Details with a stable application `code`, a user-safe `detail`, and a `traceId`. Field validation responses include `errors`, keyed by request field name with one or more messages. Business validation may include non-sensitive `details` values needed to explain the rejection.

```json
{
  "type": "https://errors.nexuspmo.local/handover-date-after-project-end",
  "title": "Validation failed",
  "status": 400,
  "detail": "The handover date must be on or before the project end date.",
  "instance": "/api/v1/projects/123/handover-requests",
  "code": "HANDOVER_DATE_AFTER_PROJECT_END",
  "errors": {
    "effectiveHandoverDate": ["The handover date must be on or before the project end date."]
  },
  "details": {
    "handoverDate": "2026-11-05",
    "projectEndDate": "2026-11-04"
  },
  "traceId": "00-abc123..."
}
```

Use the established status categories:

| Status | Meaning | Client behavior |
|---|---|---|
| 400 | Invalid request or field validation | Highlight returned fields and explain what to correct |
| 401 | Authentication is missing or invalid | Ask the user to sign in again |
| 403 | The caller lacks permission | Explain that the action is not permitted |
| 404 | The requested resource does not exist | Offer refresh/navigation guidance where appropriate |
| 409 | The action conflicts with current data | Ask the user to refresh and review the latest record |
| 422 | A business rule rejected otherwise valid input | Explain the rule and required correction |
| 429 | Request limit reached | Ask the user to wait and retry |
| 500 | Unexpected server failure | Show a generic message and reference the trace ID through support tooling |

## Backend guidance

- Preserve the existing business rule and return its failure as a structured error; do not silently accept invalid input.
- Use a stable uppercase `code` such as `HANDOVER_DATE_AFTER_PROJECT_END`, and provide the request field name through `errors`.
- Include conflicting business values only when they help explain the rejection and are safe for the current caller to see.
- Keep authorization failures as 403 and state/concurrency conflicts as 409 rather than presenting them as ordinary validation.
- Do not return exception text, stack traces, SQL/provider errors, tokens, or other sensitive diagnostics. Log unexpected exceptions server-side and return a generic 500 response with a trace identifier.
- Prefer shared response normalization and exception handling over controller-specific anonymous error shapes.

## React guidance

- The shared API client maps status classes and known error codes to the centralized English/Arabic message catalogs.
- Standard field rules (required values, email format, length and numeric bounds) are translated in the shared client; recognized business codes use their localized message and safe `details` parameters.
- Unrecognized safe server validation details remain available in English. In Arabic, the client uses a localized status/field guidance message until an explicit translation is added for that domain code.
- Keep EN and AR strings in `apps/web-react/src/i18n/messages/en.json` and `apps/web-react/src/i18n/messages/ar.json`; do not add per-module duplicate API error maps.
- Use `ApiError.fieldErrors` or `getApiFieldError(error, fieldName)` to render a field error beside the corresponding control, with `aria-invalid` and an associated description/alert where the component supports it.
- Use `getApiFieldErrors(error)` when a form needs to map the full localized field-error collection to its inputs. Do not parse `details` or response objects in individual forms.
- For forms that only provide a general error surface, `ApiError.message` includes the localized field messages as well as its summary, so validation details are not lost. Use `ApiError.summary` only when rendering field messages separately to avoid duplication.
- Render general validation, permission, conflict, and unexpected errors in the existing form alert or toast surface. Do not display raw response bodies or server diagnostics.
- The application locale controls message language and the document direction (`ltr`/`rtl`). Use the shared locale/date formatting rather than hardcoding English text or date formats.
- Network failures are normalized to a localized connection message; unexpected server errors always use the generic localized internal-error message.
- When a stable code has a localized message with parameters, interpolate only the safe API `details` values. For unknown codes, use a recognized localized field-rule message or the safe server message in English; otherwise use the localized status-class fallback.

## Regression coverage

Tests should cover the API code/status/field/details contract, redaction of technical server errors, localized EN/AR text, RTL application direction, and field-level rendering for representative forms. Add a test whenever a new domain error code or localization is introduced.
