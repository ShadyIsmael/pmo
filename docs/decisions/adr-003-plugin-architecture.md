# ADR-003: Plugin Architecture

**Status:** Accepted  
**Date:** 2025-02-10  
**Author:** Architecture Team  

---

## Context

NEXUS PMO must support extensibility through plugins to:
- Allow third-party vendors to extend the platform
- Enable custom integrations (Jira, SAP, etc.) without core changes
- Support tenant-specific feature activation
- Enable staged delivery of modular functionality (placeholders → full implementation)
- Allow customers to develop private extensions

Key challenges:
- Plugin code runs in-process -- stability and security are critical
- Plugins need access to platform APIs and domain events
- Plugin discovery must be automatic
- Version compatibility must be validated
- Tenants must be able to activate/deactivate plugins independently

---

## Decision

### Extension-Point-Based Architecture

Adopt an **interface-based plugin architecture** where plugins implement well-defined contract interfaces rather than using a generic script/DSL approach:

```csharp
// Base contract
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    IEnumerable<PluginDependency> Dependencies { get; }
    Task OnInstallingAsync(PluginContext context);
    Task OnActivatingAsync(PluginContext context);
    Task OnDeactivatingAsync(PluginContext context);
    Task OnUninstallingAsync(PluginContext context);
}

// Extension point interfaces
public interface INavigationPlugin : IPlugin { ... }
public interface IDashboardWidgetPlugin : IPlugin { ... }
public interface IApiPlugin : IPlugin { ... }
public interface IDomainEventHandler : IPlugin { ... }
```

### Key Decisions

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Plugin format** | .NET class library assemblies | Type-safe, compile-time checked, full framework access |
| **Discovery** | Assembly scanning at startup | Automatic, no configuration needed |
| **Registration** | Plugin manager scans assemblies, reads `plugin.json` manifest | Separation of metadata from code |
| **Lifecycle** | Register → Install → Activate → Deactivate → Uninstall | Full control over plugin state |
| **Dependencies** | Declared in manifest, validated at install | Prevents runtime failures |
| **Versioning** | SemVer, min/max platform version | Breaking change protection |
| **Tenant activation** | Per-tenant via `TenantPlugin` entity | Granular control |
| **Security** | Plugins run in-process with standard auth | Consistent security model |
| **Storage** | Plugin metadata in database, assemblies on disk | Versioning and state persistence |

### Plugin Discovery Mechanism

```
1. Application starts
2. PluginManagement module scans configured directories
3. Read plugin.json manifest from each directory
4. Validate platform version compatibility
5. Register plugin metadata in database (if new)
6. Load assemblies for registered plugins
7. Resolve and validate inter-plugin dependencies
8. Activate plugins for each tenant based on TenantPlugin records
```

### Contract Interfaces

See [Plugin Architecture](../plugin-architecture.md) for full interface definitions.

---

## Consequences

### Positive
- **Type safety** -- compile-time interface compliance, no runtime reflection beyond discovery
- **Rich integration** -- plugins can extend any part of the platform (UI, API, events, jobs)
- **Discoverability** -- `plugin.json` manifest provides metadata without assembly loading
- **Tenant isolation** -- per-tenant activation enables staged rollouts
- **Version safety** -- SemVer validation prevents incompatible plugins from loading
- **Development simplicity** -- creating a plugin is just implementing interfaces

### Negative
- **In-process execution** -- a buggy plugin can crash the entire application (mitigated by try/catch wrappers around plugin execution)
- **No hot-reload** -- plugin changes require application restart (acceptable trade-off)
- **Assembly binding** -- version conflicts between plugin dependencies and platform dependencies possible
- **Security** -- plugins have full access to the process; code review and approval process required
- **Platform coupling** -- plugins depend on platform API contracts; breaking changes require plugin updates

### Mitigation Strategies

1. **Plugin isolation domains** -- each plugin loaded in separate `AssemblyLoadContext` for assembly isolation
2. **Sandboxed execution** -- plugin event handlers wrapped in try/catch with circuit breaker
3. **API versioning** -- plugin contracts versioned with major/minor; breaking changes trigger version bumps
4. **Approval workflow** -- plugin installation requires administrative approval
5. **Resource limits** -- plugin execution time and memory monitored
6. **Logging** -- all plugin activity logged with plugin ID for debugging

---

## Rejected Alternatives

| Alternative | Reason for Rejection |
|-------------|---------------------|
| **Script-based plugins** (Lua, Python) | Performance overhead, type safety loss, complex integration with .NET domain |
| **Microservice plugins** | Operational complexity, network latency, no transaction support across plugin/core boundaries |
| **Webhook-only extensibility** | Too limited for rich extensions (navigation, widgets, UI) |
| **OSGi-style dynamic module system** | Excessive complexity, runtime overhead, debugging challenges |
| **Plugin as separate process (IPC)** | Performance overhead, complex serialization, harder deployment |
