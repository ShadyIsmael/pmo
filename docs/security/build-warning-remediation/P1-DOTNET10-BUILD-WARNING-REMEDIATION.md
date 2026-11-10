# P1 .NET 10 Build Warning Remediation

**Date:** 2026-11-08
**Repository:** `C:\Projects\pmo`
**Status:** Source warning fixes implemented. Fresh NuGet advisory retrieval and test execution remain blocked by this runner.

## Summary

The requested nullable-reference warnings and .NET 10 forwarded-header deprecation were fixed. The forwarded-header policy keeps the existing exact loopback proxy allowlist, header selection, and forwarding limit.

NuGet audit remains enabled. A fresh restore attempted the configured official source and emitted NU1900 for nine projects because this runner cannot resolve either NuGet endpoint. No advisory data was retrieved, so this run makes no claim that the current package graph is vulnerability-free.

The API compiled successfully after the fixes. Its final build still reports 361 warnings: nine NU1900 warnings and 352 existing IKVM warnings from the MPXJ adapter. The requested CS8601, CS8602, and ASPDEPR005 diagnostics are absent. The focused test assembly compiled, but VSTest could not connect to its testhost, including after the timeout was raised to 300 seconds; no tests executed.

## Root causes and changes

| Warning | Root cause | Change |
|---|---|---|
| NU1900 | DNS resolution for `api.nuget.org` and `data.nuget.org` fails in this runner. TCP/443 and HTTPS requests consequently fail before TLS validation. No proxy environment variables, WinINet proxy, or PAC configuration are available. | No NuGet source or audit configuration was changed because the supported alternate endpoint could not be verified reachable. Auditing remains enabled, and no warnings or findings were suppressed. |
| AuthController CS8602 | `Result<T>.Value` is nullable, and checking `IsFailure` does not establish a non-null value to the compiler or runtime. | Cache and validate the successful response before reading it. A missing response returns a generic 500. Cookie login and refresh continue to reject a missing refresh token without setting a cookie. |
| ProgramService CS8601 | `Portfolio.Status` is nullable even when the query filters for Active; the nullable property does not flow as non-null into the DTO projection. | The eligible-portfolios queries use the existing Active predicate and assign the same explicit Active status constant to returned DTOs. Null-status portfolios remain excluded. |
| ProgramService CS8602 | The cross-portfolio creation path could dereference a missing management scope or organization assignment. | The path now fails closed when scope is missing and pattern-matches a present organization ID before checking organization eligibility. Tenant and assignment scope remain enforced. |
| ASPDEPR005 | .NET 10 obsoletes the ASP.NET-specific `KnownNetworks` / `IPNetwork` APIs. | Forwarded-header configuration now clears `KnownIPNetworks` using `System.Net.IPNetwork`, then trusts only `127.0.0.1` and `::1` through the existing `KnownProxies` allowlist. `ForwardLimit = 2` and the enabled headers are unchanged. |

Microsoft's [NuGet auditing guidance](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages) documents `https://data.nuget.org/v3/index.json` as a vulnerability-only audit source. Its [.NET 10 compatibility guidance](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/ipnetwork-knownnetworks-obsolete?view=aspnetcore-10.0) documents `System.Net.IPNetwork` and `KnownIPNetworks` as the replacement APIs. The audit endpoint could not be verified from this runner, so it was not added speculatively. The [forwarded-header trust guidance](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0) also confirms that forwarded values from unknown proxies must be ignored.

## Files changed for this task

- `apps/api/src/NEXUS.Api/Program.cs`
- `apps/api/src/NEXUS.Api/Security/ForwardedHeadersSecurityPolicy.cs` (new)
- `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs`
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/AuthCookieContractTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramAuthorizationTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/ForwardedHeadersTrustTests.cs` (new)
- `apps/api/tests/NEXUS.Api.Tests/Security/LocalStorageProviderPathTraversalTests.cs`
- `docs/security/forwarded-headers.md` (new)
- This report.

The LocalStorage test update replaces three synchronous `Assert.Throws` calls around asynchronous methods with awaited `Assert.ThrowsAsync` calls. It preserves all test cases and their expected exceptions; this was required to compile the test project under its current xUnit analyzer.

## NuGet audit evidence

- SDK: .NET SDK 10.0.302, pinned by the repository `global.json`.
- The configured package source is `https://api.nuget.org/v3/index.json`; no repository `NuGet.Config` or `auditSources` entry exists.
- `HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY`, and `NO_PROXY` are absent. WinINet has no proxy or PAC configured.
- DNS resolution and TCP/443 checks fail for both `api.nuget.org` and `data.nuget.org`. HTTPS requests fail with “No such host is known”; TLS certificate validation was not reached.
- Fresh restore reached NuGet with auditing enabled, but emitted NU1900 for: Audit, WorkflowEngine, IssueManagement, WorkItems, SharedKernel, PluginManagement, Documents, Reporting, and AIPlanning.
- Vulnerability findings: **Unavailable in this run** because advisory data could not be retrieved. The Nov 7 P0-07C report records a clean audit in a different Windows-host network context; that is historical evidence, not a fresh result for this run.
- Existing PR workflow settings that keep auditing on and fail on NU1900/NU1902–NU1905 were preserved.

## Before and after

| Measure | Latest stored build (Nov 7) | Verification in this run |
|---|---:|---:|
| API build summary | 361 warnings, 1 error | 361 warnings, 0 errors |
| Requested CS8601 / CS8602 locations | Present | 0 reported |
| ASPDEPR005 | Present | 0 reported |
| NU1900 | Present | 9 projects warn on fresh restore |
| Other API build warnings | IKVM / MPXJ warnings present | 352 IKVM warnings remain |

The prior build's error was MSB3491 while writing the coverage source-root mapping under its sandbox temporary directory. It is separate from the current API source changes. The final API build in this run did not include test projects.

## Verification

| Check | Result |
|---|---|
| Exact default `dotnet restore .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj` | Exit 1 before restore graph completion, with no diagnostic output in this runner. |
| Serial restore: `dotnet restore .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj --verbosity normal -m:1` | Exit 0; nine NU1900 warnings; actual vulnerability metadata unavailable. |
| Exact default `dotnet build .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj --no-restore` | Exit 1 with no warning/error diagnostics in this runner. |
| Full serial API build | Reached API compilation and exposed an ambiguous `IPNetwork` type between ASP.NET and `System.Net`; fixed by qualifying `System.Net.IPNetwork`. All referenced projects were built in that pass. |
| Final API compile: `dotnet build .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj --no-restore --no-dependencies --verbosity minimal -m:1` | **Passed**, 361 warnings, 0 errors. `--no-dependencies` reused project outputs built in the preceding full serial pass. |
| Focused test project compile | **Passed** after correcting the forwarded-header options wrapper and async xUnit assertions. The test assembly was produced. |
| Focused Identity, Portfolio/Program, and forwarded-header tests | **Not executed.** VSTest failed to connect to testhost after 90 seconds; retry with `VSTEST_CONNECTION_TIMEOUT=300` failed after 300 seconds. No test results were produced. |
| Forwarded-header source review | Trust list remains exactly `127.0.0.1` and `::1`; only X-Forwarded-For and X-Forwarded-Proto are enabled; forwarding limit remains 2. `UseForwardedHeaders` remains before HTTPS redirection and authentication. |
| Database, migrations, seed, Browser UAT | Not run; not required by this task. |

## Remaining blockers

1. **Fresh NuGet audit:** external DNS/network access to NuGet is unavailable. An approved host command runner was not exposed to this task. NU1900 remains unresolved and must not be treated as a clean audit.
2. **Automated test execution:** VSTest cannot connect to its testhost in this execution environment. The tests compile but have no runtime result.
3. **Existing build warnings:** 352 IKVM warnings from MPXJ class conversion remain. The adapter was built during the full serial pass; this work did not repeat or alter the existing MPXJ isolation.

## Skills and coordination

The repository instructions, documentation-first workflow, and available security/code-quality guidance were applied. No dedicated .NET, NuGet, ASP.NET Core, or test skill was available in the discovered catalogs.

The four requested workstreams were delegated: NuGet audit diagnosis; Identity nullability; Portfolio nullability; and forwarded-header migration/security coverage. The Main Agent integrated the changes and performed consolidated restore/build/test verification.
