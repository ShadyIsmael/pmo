# ADR-009: React/Vite as the Sole Supported Frontend

**Status:** Accepted
**Date:** 2026-10-27
**Decision owner:** Product owner

## Context

The repository retained two browser applications. The Next.js application added a separate dependency tree, build path, Docker service, and audit surface. The React/Vite application is the interface selected for ongoing development.

## Decision

- Adopt apps/web-react, built with React, Vite, and React Router, as the sole supported frontend.
- Retire and remove the Next.js application in apps/web.
- Route root development, build, lint, typecheck, test, localization, and cleanup scripts to apps/web-react.
- Make the default Docker Compose web service build the React/Vite static SPA and serve it through nginx on host port 4000. Local Vite development uses port 4100.
- Keep the React and API runtime contracts unchanged. Do not modify API behavior, schema, migrations, data, or running services as part of this frontend retirement.
- Treat older Next.js architecture and audit notes as historical records. They are not build, audit, deployment, or agent requirements.

## Consequences

- The React app is the only frontend package future agents and dependency audits should inspect.
- React Router and Vite define the supported routing and build workflow.
- The old Next.js route and widget comparison tests are replaced with tests for the active React route tree and widget catalog.
- There is no Next.js CI workflow or app-specific security scan configuration to remove; generic scanners discover the remaining package manifests.
- This decision does not claim that a separate browser UAT was performed.
