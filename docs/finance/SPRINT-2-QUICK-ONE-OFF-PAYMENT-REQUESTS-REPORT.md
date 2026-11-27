# Sprint 2 Task 07 — Quick One-off Payment Requests

**Status:** Not implemented in this pass. Deferred due to scope and dependency on coordinated transactional refactor of Planned Payment registration and Payment Request creation.  
**Date:** 2026-11-22

## Summary

Task 07 requires a single guided workflow that atomically creates a Planned Payment, exactly one Installment, a Lifetime Budget reservation, and a linked Payment Request, then reuses Annual Budget funding and approval flows. The existing `PlannedPaymentService.RegisterAsync` and `ProjectPaymentRequestWorkflow.CreateAsync` each manage their own serializable transactions and financial locking hooks, so a truly atomic one-off operation requires refactoring those services to support an externally supplied transaction or a shared coordination method. That refactor was not completed in this pass to avoid destabilizing the passing Task 02/03/06 implementation.

## Deferred Items

- Coordinated `CreateQuickOneOffAsync` workflow.
- Frontend guided Quick One-off form.
- Focused automated tests.
- Migration (none expected; reuse existing schema).

## Recommendation

Implement Task 07 as a dedicated follow-up before consolidated Sprint 2 Closure, reusing the now-stable settlement/adjustment and planned-payment infrastructure. Consolidated Sprint Closure remains deferred.