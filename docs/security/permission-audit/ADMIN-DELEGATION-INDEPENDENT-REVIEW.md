# Admin Delegation: Independent Review and Evidence Limits

**Updated:** 2026-11-14  
**Reviewer:** Independent critical review  
**Status:** Source review completed; API, database, and runtime verification pending.

## Correction to prior conclusion

An earlier version described the historical `POST /api/users` 403 root cause as confirmed and stated that the incident was fixed. That conclusion exceeded the available evidence and is withdrawn. No request/response capture, trace-linked rejection diagnostic, deployed assembly identity, or database evidence for the historical incident was available. The current source contains a plausible correction, but the prior live incident is not reproduced or verified here.

## Independent source findings

- Role assignment now evaluates an explicit delegation path for the protected Tenant Admin instead of requiring operational possession for every eligible ordinary permission.
- Reserved Platform, CrossTenant, and SystemOwner permissions, inactive/uncatalogued or non-assignable permissions, invalid dependencies, protected administrator identities, self-escalation, and cross-Tenant assignments remain rejected.
- Canonical RoleKey is separate from System classification. The current seed defines only Tenant Admin and Organization Admin as System Roles; Project Manager and the other operational roles are Non-System.
- Direct Allow overrides are simulated through dependency enforcement so a requested Allow cannot activate an omitted inherited permission indirectly.
- Organization Admin appointment remains denied pending Product Owner policy. Its configuration permission set is scoped by active Organization membership/grant and fresh effective permissions.

## Outstanding independent-review blockers

1. The current Development database inventory and migration result are unavailable; no role IDs, membership counts, direct override counts, or permission-link preservation counts were read.
2. No current API build or focused .NET test result has yet been recorded in this review.
3. No authenticated HTTP test proves the Project Manager onboarding journey.
4. Organization Admin positive and negative API behavior has source/service test coverage only until the pending test run and runtime checks complete.
5. Organization Admin appointment, removal, protected status, recovery, and exact editability policy remain unresolved and must stay denied or immutable where not expressly approved.

## Review disposition

Do not claim complete recovery or readiness based on source inspection. Report implementation, build/test, migration, and runtime status separately. The final independent implementation review must be refreshed after the serialized test/build run and frontend integration review.
