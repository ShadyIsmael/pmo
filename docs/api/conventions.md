# API Conventions

---

## RESTful API Design Principles

NEXUS PMO API follows standard RESTful conventions:

- **Resources** are nouns (e.g., `/projects`, `/resources`, `/risks`)
- **HTTP methods** map to CRUD operations:
  - `GET` -- Retrieve resource(s)
  - `POST` -- Create resource or execute action
  - `PUT` -- Full update of resource
  - `PATCH` -- Partial update of resource
  - `DELETE` -- Remove resource
- **Actions** on resources use `POST` with action-specific routes (e.g., `POST /approvals/{id}/approve`)
- **Consistent naming**: plural nouns, kebab-case for multi-word resources (`/work-items`, `/risk-mitigations`)

---

## URL Structure

```
/api/{version}/{module}/{resource}[/{id}][/{action}]
```

| Segment | Description | Example |
|---------|-------------|---------|
| `{version}` | API version | `v1` |
| `{module}` | Module name | `projects`, `resources` |
| `{resource}` | Resource type | `milestones`, `allocations` |
| `{id}` | Resource identifier (GUID) | `a1b2c3d4-...` |
| `{action}` | Action on resource | `approve`, `upload` |

**Examples:**
- `GET /api/v1/projects` -- List projects
- `GET /api/v1/projects/{id}` -- Get specific project
- `POST /api/v1/approvals/{id}/approve` -- Approve an approval request

---

## Standard Query Parameters

List endpoints support the following query parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | int | 1 | Page number (1-indexed) |
| `pageSize` | int | 20 | Items per page (max 100) |
| `sortBy` | string | `createdAt` | Field to sort by |
| `sortDirection` | string | `desc` | `asc` or `desc` |
| `search` | string | - | Full-text search term |
| `filters` | string | - | JSON-encoded filter object |

**Filter format:**
```
filters=[{"field":"status","operator":"eq","value":"Active"},{"field":"priority","operator":"in","value":["High","Critical"]}]
```

Supported operators: `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `in`, `nin`, `contains`, `between`

---

## Pagination Response Format

All list endpoints return a standardized paginated response:

```json
{
  "data": [ ... ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 156,
    "totalPages": 8,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

---

## Standard Response Envelope

Successful responses return data directly:

```json
{
  "data": { ... },
  "pagination": { ... }  // Only for list endpoints
}
```

---

## Error Response Format (Problem Details)

Errors follow RFC 7807 Problem Details format:

```json
{
  "type": "https://errors.nexuspmo.local/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/projects",
  "errors": {
    "name": ["The Name field is required."],
    "startDate": ["Start Date must be before End Date."]
  },
  "traceId": "00-abc123..."
}
```

| HTTP Status | Description |
|-------------|-------------|
| 200 | Success |
| 201 | Created |
| 400 | Bad Request / Validation Error |
| 401 | Unauthenticated |
| 403 | Forbidden (insufficient permissions) |
| 404 | Resource not found |
| 409 | Conflict (concurrency, duplicate) |
| 422 | Unprocessable Entity |
| 429 | Rate limit exceeded |
| 500 | Internal Server Error |

---

## Authentication

All endpoints except login require JWT Bearer authentication:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

The JWT contains:
- `sub` -- User ID
- `email` -- User email
- `name` -- User full name
- `tenant_id` -- Tenant identifier
- `role` -- Active role name
- `permissions` -- Array of granted permission strings

---

## Authorization

API authorization uses ASP.NET Core policy-based authorization:

```csharp
[Authorize]
[HttpGet("{id}")]
public async Task<IActionResult> GetById(...)

[Authorize(Policy = "RequireProjectWrite")]
[HttpPost]
public async Task<IActionResult> Create(...)
```

Authorization policies map to permissions from the identity module.

---

## Correlation IDs

Every API response includes a correlation ID for request tracing:

```
X-Correlation-ID: 00-abc123def456-789ghi012jkl-01
```

- Generated automatically for each request
- Included in all log entries for that request
- Returned in error responses for troubleshooting

---

## API Versioning

- **Current version:** `v1`
- **Strategy:** URL path versioning (`/api/v1/projects`)
- **Backward compatibility:** Breaking changes trigger a new version
- **Deprecation:** Deprecated versions receive a `Sunset` header with migration timeline

---

## Common Headers

| Header | Description | Required |
|--------|-------------|----------|
| `Authorization` | JWT Bearer token | Yes (except login) |
| `X-Correlation-ID` | Request correlation ID | Optional (auto-generated) |
| `X-Requested-With` | XMLHttpRequest for CSRF | Optional |
| `Accept-Language` | Preferred locale | Optional |
| `If-Match` | ETag for optimistic concurrency | Optional |
| `If-None-Match` | ETag for conditional GET | Optional |

---

## Rate Limiting

- **Default limit:** 100 requests per minute per user
- **Burst allowance:** 20 requests burst
- **Headers returned:**
  - `X-RateLimit-Limit` -- Max requests per window
  - `X-RateLimit-Remaining` -- Remaining requests
  - `X-RateLimit-Reset` -- When the window resets (Unix timestamp)
- **429 response:** Returned when limit exceeded with `Retry-After` header
