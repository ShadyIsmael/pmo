# Coding Conventions

---

## TypeScript / React Conventions

### Component Structure
- Use **React Server Components** by default
- Use **Client Components** (`"use client"`) only when interactivity is needed
- One component per file, named after the file
- Export named functions, not default exports

```typescript
// Correct
export function ProjectCard({ project }: ProjectCardProps) { ... }

// Incorrect
export default function ProjectCard() { ... }
```

### Props
- Define props as `interface` (not `type`) prefixed with component name
- Use `React.ReactNode` for children
- Destructure props in function signature

```typescript
interface ProjectCardProps {
  project: Project;
  onEdit?: (id: string) => void;
  className?: string;
}

export function ProjectCard({ project, onEdit, className }: ProjectCardProps) { ... }
```

### Hooks
- Prefix custom hooks with `use`
- Keep hooks focused on a single concern
- Return typed values

```typescript
export function useProjects(filters: ProjectFilters) {
  return useQuery({
    queryKey: ["projects", filters],
    queryFn: () => api.getProjects(filters),
  });
}
```

### State Management
- Server state: TanStack React Query
- Form state: React Hook Form
- UI state: React `useState` / `useReducer`
- Global UI state: React Context (sparingly)

### File Organization
```
ComponentName/
├── ComponentName.tsx      # Main component
├── ComponentName.test.tsx # Tests
└── index.ts              # Re-export
```

---

## C# Conventions

### File Organization (per module)
```
NEXUS.Modules.{Name}/
├── Domain/
│   ├── Entity.cs
│   ├── ValueObject.cs
│   └── DomainEvent.cs
├── Application/
│   ├── I{Service}Service.cs
│   ├── {Service}Service.cs
│   ├── Commands/
│   └── Queries/
├── Api/
│   ├── {Entity}Controller.cs
│   └── Dtos/
└── Infrastructure/
    └── Repository.cs
```

### Class Structure
- Public classes only (no `internal` in module projects)
- All entities inherit from `BaseEntity`
- All value objects inherit from `BaseValueObject`
- All domain events inherit from `BaseDomainEvent`

```csharp
public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    // ... properties

    // Collection navigation properties
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
}
```

### Service Pattern
- Interface and implementation in same project
- Interface defines all public operations
- Services return `Result<T>` for operation outcomes

```csharp
public interface IProjectService
{
    Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<PagedResult<ProjectSummaryDto>>> GetPagedAsync(int page, int pageSize, ...);
}

public class ProjectService : IProjectService
{
    // Implementation
}
```

### Module Registration
```csharp
public static class ProjectManagementModuleRegistration
{
    public static IServiceCollection AddProjectManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }
}
```

---

## Naming Conventions

| Entity | Convention | Example |
|--------|------------|---------|
| **TypeScript files** | kebab-case | `project-card.tsx` |
| **React components** | PascalCase | `ProjectCard` |
| **React hooks** | camelCase with `use` prefix | `useProjects` |
| **C# classes** | PascalCase | `ProjectService` |
| **C# interfaces** | PascalCase with `I` prefix | `IProjectService` |
| **C# methods** | PascalCase | `GetByIdAsync` |
| **C# properties** | PascalCase | `ProjectName` |
| **C# private fields** | camelCase with `_` prefix | `_projectService` |
| **Database tables** | PascalCase plural | `Projects`, `WorkItems` |
| **Database columns** | PascalCase | `CreatedAt`, `ProjectId` |
| **API routes** | kebab-case plural | `/api/projects`, `/api/work-items` |
| **URL parameters** | camelCase | `?pageSize=20&sortBy=name` |
| **JSON properties** | camelCase | `{ "projectName": "..." }` |
| **Environment variables** | UPPER_SNAKE_CASE | `NEXT_PUBLIC_API_URL` |

---

## Import Ordering

### TypeScript
```typescript
// 1. External libraries
import * as React from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";

// 2. Internal shared (aliased)
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

// 3. Relative imports (within same module)
import { ProjectCard } from "../ProjectCard/ProjectCard";

// 4. Types
import type { Project } from "@/types/project";
```

### C#
```csharp
// 1. System namespaces
using System.Text;

// 2. Microsoft/ASP.NET namespaces
using Microsoft.AspNetCore.Authorization;

// 3. Third-party libraries
using MediatR;

// 4. NEXUS namespaces (ordered by module)
using NEXUS.SharedKernel;
using NEXUS.Modules.ProjectManagement.Application;
// ... using NEXUS.Modules.{Module}.{Layer};
```

---

## Error Handling Patterns

### TypeScript
```typescript
// Use React Query error handling
const { data, error, isLoading } = useQuery({
  queryKey: ["projects", id],
  queryFn: () => api.getProject(id),
});

// Form errors via React Hook Form
const { register, handleSubmit, formState: { errors } } = useForm<FormData>();
```

### C#
```csharp
// Services return Result<T>
public async Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct)
{
    var project = await _context.Projects.FindAsync(new object[] { id }, ct);
    if (project is null)
        return Result<ProjectDto>.Failure(new Error("Project.NotFound", "Project not found", 404));
    return Result<ProjectDto>.Success(MapToDto(project));
}

// Controllers handle Result
var result = await _service.GetByIdAsync(id, ct);
if (result.IsFailure)
    return NotFound(new ProblemDetails { Title = result.Error.Message, Status = result.Error.StatusCode });
return Ok(result.Value);
```

---

## Testing Conventions

### Frontend (Vitest / React Testing Library)
- Test user behavior, not implementation details
- Use `@testing-library/react` queries: `getByRole`, `findByText`, `getByPlaceholderText`
- Prefer integration tests over unit tests for components
- Mock API calls at the network level (MSW)

```typescript
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

describe("ProjectCard", () => {
  it("renders project name", () => {
    render(<ProjectCard project={mockProject} />);
    expect(screen.getByText("Cloud Migration")).toBeInTheDocument();
  });
});
```

### Backend (xUnit)
- Test services with mocked dependencies
- Test controllers with integration tests
- Use `Shouldly` or `FluentAssertions` for readable assertions

```csharp
[Fact]
public async Task GetByIdAsync_WithValidId_ReturnsProject()
{
    // Arrange
    var service = new ProjectService(_context);

    // Act
    var result = await service.GetByIdAsync(_validProjectId, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.Name.ShouldBe("Cloud Migration");
}
```

---

## Git Conventions

### Branch Naming
```
feature/{issue-number}-{short-description}
bugfix/{issue-number}-{short-description}
hotfix/{issue-number}-{short-description}
release/{version}
```

### Commit Messages
```
type(scope): description

Types: feat, fix, refactor, test, docs, chore, style, perf
```

Examples:
```
feat(projects): add Gantt chart view
fix(resources): correct allocation percentage calculation
docs(api): update endpoint documentation
```

### Pull Request Checklist
- [ ] Code follows conventions
- [ ] Tests added/updated
- [ ] Lint passes (`npm run lint`)
- [ ] TypeScript type check passes (`npm run typecheck`)
- [ ] Self-reviewed
- [ ] Documentation updated if applicable
