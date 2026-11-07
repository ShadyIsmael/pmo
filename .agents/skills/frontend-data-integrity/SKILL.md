---
name: frontend-data-integrity
description: "Mandatory rules for frontend data integrity in apps/web-react: prefer authoritative backend data over hardcoded business values, no fake operational data, no unapproved currency fallbacks, backend-owned security/business rules, centralized localization, consistent formatting, centralized branding, proper Loading/Empty/Error/Unavailable states."
---

# Frontend Data Integrity

Apply this skill to every change in `apps/web-react`.

## Mandatory rules

1. Prefer authoritative backend data over hardcoded business values.
2. Never present mocked operational data as real.
3. Never fabricate success, security, license, or financial statuses.
4. Never introduce arbitrary currency fallbacks.
5. Respect backend ownership of security and business validation rules.
6. Use centralized translation catalogs for user-facing text.
7. Preserve EN/AR and RTL compatibility.
8. Use consistent locale-aware formatting.
9. Reuse centralized branding configuration.
10. Provide proper Loading, Empty, Error, and Unavailable states.
11. Preserve existing application design and visual consistency.
12. Verify that actual API responses support displayed information.
13. Prevent regressions using focused tests and appropriate automated checks.

## Exceptions

The following are not inherently prohibited: API endpoint paths, permission codes, technical enum values, database field identifiers, document-format names, established protocol constants, explicit Development-only localhost configuration, legitimate test fixtures inside test files. Distinguish technical constants from misleading hardcoded business information.

## Currency

Never silently substitute USD, GBP, or EGP when authoritative currency data is missing. Display an appropriate unavailable state. Keep business currency separate from formatting locale. Never aggregate monetary values across currencies without an approved conversion rule.

## Security

Never implement security controls only in React. Bind to existing authoritative security configuration. Differentiate Enabled, Disabled, Not Configured, and Unavailable.

## Branding

Use approved FOPX PM identity. Reuse centralized branding configuration.
