# ADR-008: Risk Workflow v1

## Status

Accepted

## Context

The original Risk row stored a single mitigation plan and target resolution date. That shape cannot represent multiple accountable actions, impact explanations, or a progress history, and it made status changes difficult to audit.

## Decision

Keep `Risk` as the project-owned aggregate root and add normalized `RiskImpact`, `RiskAction`, and append-only `RiskActionUpdate` entities. All entities inherit the shared tenant, soft-delete, audit, and row-version behavior. The workflow is `Risk → Action → Action Updates`, with statuses `Identified`, `ActionRequired`, `FollowUp`, `Mitigated`, and `Closed`. Action creation advances a risk to `ActionRequired`; the first meaningful action update may advance it to `FollowUp`; explicit status updates remain concurrency checked.

The migration copies any legacy mitigation plan or target date into an initial action, maps legacy follow-up plans to the action description, maps `NextReviewDate` to `RiskAction.NextFollowUpDate`, and copies existing follow-up updates into `RiskActionUpdate` rows while preserving IDs, authors, and timestamps. Legacy follow-up tables and legacy Risk columns remain physically present for lossless compatibility/rollback but are no longer mapped or written by the active model. Existing snapshot-only fields remain on the entity for compatibility with project snapshots.

## Consequences

- Risk impacts, actions, and action update history are independently queryable and tenant scoped.
- Existing data is preserved during migration, while new writes use the normalized workflow.
- Clients must use localized enum metadata rather than display enum names directly.
