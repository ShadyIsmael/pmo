import { Link } from "react-router-dom";
import { brand, productVersion, shortGitCommit, buildDateDisplay, statusDisplay } from "@lib/version";
import { brandCopy } from "@lib/brand";

export function AboutPage() {
  const copy = brandCopy("en");

  return (
    <div className="mx-auto max-w-3xl space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{brand.product.name}</h1>
        <p className="text-muted-foreground">{copy.description}</p>
      </div>

      <section className="rounded-lg border p-4">
        <h2 className="text-lg font-medium">System Information</h2>
        <dl className="mt-3 grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-muted-foreground">Product</dt>
            <dd className="font-medium">{brand.product.name}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Version</dt>
            <dd className="font-mono font-medium">{productVersion.version}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Build Date</dt>
            <dd className="font-mono">{buildDateDisplay(productVersion.buildDate)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Git Commit</dt>
            <dd className="font-mono">{shortGitCommit(productVersion.gitCommit)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Release Status</dt>
            <dd className="font-medium">{statusDisplay(productVersion.status)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Parent Company</dt>
            <dd className="font-medium">{brand.company.name}</dd>
          </div>
        </dl>
      </section>

      <section className="rounded-lg border p-4">
        <h2 className="text-lg font-medium">Release History</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Release metadata is sourced from the centralized version source.
        </p>
        <ul className="mt-3 space-y-2 text-sm">
          <li className="flex items-center justify-between rounded-md border px-3 py-2">
            <span className="font-mono">{productVersion.version}</span>
            <span className="text-muted-foreground">Initial baseline</span>
          </li>
        </ul>
        <p className="mt-3 text-xs text-muted-foreground">
          No fabricated past releases are recorded. Release notes correspond to actual Git commits.
        </p>
      </section>

      <section className="rounded-lg border p-4">
        <h2 className="text-lg font-medium">Documentation</h2>
        <ul className="mt-3 space-y-2 text-sm">
          <li>
            <Link to="/about/changelog" className="text-primary hover:underline">
              Changelog
            </Link>
          </li>
          <li>
            <a
              href="/docs/releases/VERSIONING-POLICY.md"
              className="text-primary hover:underline"
              target="_blank"
              rel="noreferrer"
            >
              Versioning Policy
            </a>
          </li>
          <li>
            <a
              href="/docs/releases/RELEASE-HISTORY.md"
              className="text-primary hover:underline"
              target="_blank"
              rel="noreferrer"
            >
              Release History
            </a>
          </li>
        </ul>
      </section>
    </div>
  );
}

export function ChangelogPage() {
  return (
    <div className="mx-auto max-w-3xl space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Changelog</h1>
        <p className="text-muted-foreground">
          All notable changes to FOPX PM are documented here.
        </p>
      </div>

      <section className="rounded-lg border p-4">
        <h2 className="text-lg font-medium">1.0.0-rc.1 — 2026-10-28</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Initial product baseline. Release candidate.
        </p>
        <p className="mt-2 text-sm">
          This is the first formal release of FOPX PM, establishing the baseline
          for the enterprise project, program, and portfolio management platform.
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Release gates remain open. See the versioning policy for details.
        </p>
      </section>
    </div>
  );
}

export function VersionDisplay() {
  return (
    <span className="font-mono text-xs text-muted-foreground">
      {productVersion.version}
    </span>
  );
}