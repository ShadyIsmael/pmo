# Sprint S5 — Business-First Permission UX

**Date:** 2026-11-14  
**Result:** **PARTIAL — BUSINESS-MODULE/FEATURE GROUPING SOURCE UPDATED; NOT VERIFIED**

## Implemented source slice

- The Roles screen now maps all eight observed catalog modules and their current scope cases into six bilingual business groups, then groups each permission by its existing feature/screen with View and Action controls together. Project-scoped Financial Management permissions appear under Project Management; Program/Portfolio rollups appear under Portfolio & Program Management.
- The selected role's read-only permission summary uses the same mapping. Regression coverage now checks current module/scope mapping and the Project Management display for Project Annual Budget permissions.
- Business group and feature order follows localized display labels with stable identifier tie-breaks. Permission filters now include business area, scope, level, grant state, and sensitivity.
- The existing search, filters, dependency context, protected Core locks, grant guards, and View/Action controls remain in place.
- The user permission directory now reports source role names, direct Allow/Deny source, catalog key scope, and the server-resolved saved effective state; unsaved override edits are labelled as pending and do not fabricate an effective result. A focused API/UI regression was authored but not run.
- Repaired the Arabic module and screen labels for ProjectStatus.Change and ProjectPhase.Change.

## Open work

- Source recount confirms 188 unique catalog entries and 48 unique `moduleId:screenId` groups. The catalog has a `screenId` field but no separate feature identifier, so the older 47-feature figure is not a directly comparable catalog count; no duplicate or ungrouped catalog entry was found. A Product Owner feature-parent crosswalk is still needed only if the product taxonomy must consolidate these 48 screen groups into 47 named features. The full screen/control crosswalk remains open.
- The six generic `ApprovalRequest.*` keys are now visibly marked **No verified UI control** in the role editor and selected-role summary (English/Arabic). No screen or API-only intent was invented. Their backend-only intent remains unverified; the crosswalk must confirm whether they are API-only or should map to specific approval controls.
- Arabic copy quality beyond the two identified catalog entries, RTL/browser layout, keyboard and screen-reader behavior, and stable visual grouping need owner UAT.
- No permission keys or persisted grants were renamed, removed, or migrated.

The focused permission-business-module/access-security frontend suites passed **39/39**, including the English/Arabic marker test. `npm run build` passed `tsc -b` and the Vite production build; Vite emitted dependency annotation, static/dynamic import, and large-chunk warnings. Browser UAT, broader Arabic/RTL review, keyboard checks, and screen-reader runtime checks remain unverified.
