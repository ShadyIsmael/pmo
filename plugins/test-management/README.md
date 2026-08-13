# Test Management

Test case management with test suites, plans, cycles, executions, results, defects, and requirement coverage tracking.

**Status:** Active

## Features

- Test suite creation and organization
- Test planning with cycles and iterations
- Test execution tracking with pass/fail/blocked results
- Defect linking from failed tests
- Requirement coverage tracking and reporting
- Configurable test workflows

## Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `defaultTestSuiteTemplate` | string | — | Default template for new test suites |
| `defectWorkflowId` | string | — | Workflow ID for defect lifecycle |
| `requirementCoverageEnabled` | boolean | `true` | Enable requirement coverage tracking |

## Dependencies

- `nexus.module.project-management`

## How to Enable

Enable the `testManagement` feature flag. The Test Management section will appear in the sidebar. Create test suites and organize test cycles from the management pages.
