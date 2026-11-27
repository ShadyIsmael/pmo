# Development Database Clean Reset Report

**Date:** 2026-11-22

## Root Cause

Migration `20261116093145_AddOrganizationOwnedRoles` assumed historical Organization and Role records existed and threw SQL error 51001 on a freshly recreated empty database.

## Fix

Added a fresh-database guard to the migration SQL: if `dbo.Roles` is empty, the legacy reconciliation block returns early, allowing schema changes to execute. Existing databases still run strict checks.

Changed file: `apps/api/src/NEXUS.Persistence/Migrations/20261116093145_AddOrganizationOwnedRoles.cs`

## Migration Execution

All repository migrations applied successfully; 0 pending. Model synchronized.

## Minimal Seed

Attempted `--seed-minimal-only` with `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` set. Failed: `InvalidOperationException` duplicate `Permission` key tracking in `SeedDataProvider.SeedIdentityAsync`. Blocker remains.

## Verification

- Migrations: 0 pending.
- Minimal Seed: NOT completed (blocker).
- Projects/Payments: not verified due to seed failure.

## Remaining Blockers

- Minimal seed duplicate permission tracking issue must be investigated; do not treat seed as complete.