# NEXUS PMO (FOPX PM)

> **Enterprise Project Portfolio Management Platform**  
> *Plan. Govern. Execute. Optimize.*

NEXUS PMO (FOPX PM) is a scalable project portfolio management (PPM) solution built as a modular monolith in .NET 10 LTS and React 18 / Vite.

---

## 📖 Documentation

All detailed project specifications, architectural decisions, reports, and manuals are organized inside the **[`docs/`](docs/)** directory:

- **[Master Documentation Index](docs/README.md)** — Complete catalog of all documentation.
- **[Central Reports Directory](docs/reports/README.md)** — Index of all audits, benchmarks, test results, and performance reports.
- **[Architecture & ADRs](docs/architecture/README.md)** — System architecture, module catalogs, and decision records.
- **[API Documentation](docs/api/endpoints.md)** — Endpoints reference, conventions, and error formats.
- **[Security & Audits](docs/security/)** — Security architecture, permissions matrix, and penetration testing reports.
- **[Quality Assurance & UAT](docs/quality/)** — UAT checklists, parity gates, and data integrity guides.
- **[UI & Design System](docs/ui/design-system.md)** — Tokens, themes, typography, and component specifications.
- **[Releases & Versioning](docs/releases/RELEASE-HISTORY.md)** — Release notes, history, and versioning criteria.

---

## 🚀 Quick Start

### Prerequisites
- Node.js >= 18.0.0
- .NET 10 SDK
- Docker & Docker Compose (optional for local database and Redis)

### Development Setup
```bash
# 1. Install root & frontend dependencies
npm install
npm --prefix apps/web-react ci

# 2. Run database & Redis (Docker)
npm run docker:up

# 3. Start development servers
# Terminal 1: Backend API
npm run dev:api

# Terminal 2: Web Frontend (React + Vite)
npm run dev:web
```

For full setup instructions, see the [Local Development Guide](docs/development/local-setup.md).
