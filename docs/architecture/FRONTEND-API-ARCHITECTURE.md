# Frontend API Architecture

The supported React/Vite app uses a centralized API boundary under apps/web-react/src/api/.

- client.ts owns HTTP transport, VITE_API_URL resolution, authentication headers, token refresh, session expiry, and response parsing.
- endpoints.ts defines the typed endpoint map and encodes path parameters.
- Domain API modules keep feature requests and query-key factories close to their React feature boundaries.
- queryClient.ts configures React Query behavior.

Feature screens live under apps/web-react/src/features/ and use the shared API client. React Router is configured in apps/web-react/src/routes/AppRoutes.tsx. English and Arabic messages live under apps/web-react/src/i18n/messages/.

The only supported browser client is apps/web-react.
