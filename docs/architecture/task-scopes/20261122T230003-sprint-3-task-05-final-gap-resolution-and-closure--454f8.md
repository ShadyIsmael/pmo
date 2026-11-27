# UPI Task Context Manifest

Task: **Sprint 3 Task 05 final gap resolution and closure: verify D1 Budget-Line reservation integrity, D2 Status Report financial currency, permission catalog/delegation, frontend build and finance tests/mocks, backend financial regressions and DB persistence evidence, then update consolidated closure report.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261122T225947-23000fb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/Migrations/20261031000210_AddFinancialPersistenceSprints3to6.cs` | 1-653 | 11020 | filename-match:financial, filename-match:persistence, filename-match:sprint, module-mentioned, primary-module, symbol-match:AddFinancialPersistenceSprints3to6 |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx` — REFERENCES_TYPE (inferred-token-match; ApprovalMode)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Currency)

## Budget

- Initial selected source: **11020** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
