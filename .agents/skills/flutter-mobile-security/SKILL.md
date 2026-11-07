---
name: flutter-mobile-security
description: "Review or implement security-sensitive Flutter and Dart applications, including mobile platform configuration and dependencies, against current OWASP MASVS/MASTG guidance. Use for Flutter mobile security reviews, auth, storage, crypto, network, or platform integration work."
---

# Flutter Mobile Security

Use this skill for security-sensitive Flutter/Dart code, Flutter dependencies, Android or iOS integration, and requested mobile security reviews. It guides source review and scoped testing; it is not itself a scanner.

## Scope and method

- Identify the Flutter/Dart versions, target platforms, relevant modules, data sensitivity, and whether the requested work is source review, implementation, dependency review, or runtime testing. Keep the work within the user's requested scope.
- For reviews, use the applicable OWASP MASVS control groups as a coverage map: storage, cryptography, authentication and authorization, network communication, platform interaction, code quality, resilience, and privacy. Use MASTG for relevant verification procedures; don't claim full MASVS coverage from a source-only review.
- Inspect platform-specific files for the selected targets: Android manifest, Gradle/build settings, network security configuration, exported components, backup behavior, and permissions; iOS Info.plist, entitlements, transport security, keychain usage, URL schemes, and universal links.
- Review Flutter plugins and direct/transitive dependencies using `pubspec.yaml` and `pubspec.lock`. If a dependency scanner is available and the user requested a scan, check its current ecosystem coverage and report any gaps. Do not treat an unreviewed advisory suppression as a fix.

## Security areas

- **Secrets:** distinguish public client identifiers from credentials that must remain server-side. Values compiled into a mobile app—including values supplied through `--dart-define`, `.env` assets, or obfuscation—must not be treated as confidential server secrets.
- **Storage and privacy:** trace tokens, keys, and personal data into preferences, files, logs, analytics, backups, and crash reports. Use platform-backed secure storage where appropriate, and verify the package's behavior on the target platforms. Consider backup requirements instead of applying a blanket backup setting.
- **Network:** flag cleartext traffic and disabled certificate validation. Review credential handling, redirects, and transport configuration. Treat certificate pinning as a threat-model and operational decision, not a universal requirement.
- **Identity and authorization:** client-side route or widget checks are not an authorization boundary. Verify that the backend enforces identity, roles, ownership, and sensitive actions. Review deep links and callback validation.
- **Platform and plugins:** inspect WebViews, platform channels, intents, URL handlers, permissions, clipboard use, and plugin trust boundaries for the selected Android/iOS targets.
- **Cryptography and input:** avoid custom cryptographic algorithms and non-cryptographic randomness for security-sensitive values. Trace untrusted input into parsers, storage, and API requests; client validation does not replace server validation.
- **Release posture:** distinguish code obfuscation, signing, integrity checks, and debug settings from actual confidentiality or server-side access control. State which checks require a built app or device.

## Tool boundaries

- **Semgrep CE:** it is installed in this environment. Before relying on it for Dart, verify the current engine and rule coverage; parser support alone does not establish meaningful security coverage. Do not claim a scan ran unless it actually did.
- **CodeQL:** its supported language set includes C# and JavaScript/TypeScript but not Dart/Flutter. The CodeQL skill is installed, but the CLI is not currently installed and private-repository eligibility has not been confirmed. Do not invoke CodeQL on private code until license eligibility is confirmed.
- **OWASP ZAP:** use it for an expressly authorized, in-scope web/API target when runtime testing is requested. Mobile-client review does not authorize requests to an API, production, or any other host. Do not start active scans without explicit target authorization.
- Static source review cannot establish runtime behavior, backend authorization, or full mobile resilience. Separate reviewed evidence from checks not performed and identify follow-up MASTG tests where useful.

## Reporting

Support each finding with a file and line or other reproducible evidence, explain the security impact and affected boundary, and give a proportionate fix. Mark uncertainty and coverage gaps. Never report a tool or control as passing when it was not checked.

## Primary references

- [OWASP MASVS](https://mas.owasp.org/MASVS/) and [OWASP MASTG](https://mas.owasp.org/MASTG/) for mobile security controls and test procedures.
- [Flutter security guidance](https://docs.flutter.dev/security) for Flutter SDK and dependency maintenance.
- [CodeQL supported languages](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-code-scanning) for language eligibility.
- [Semgrep CE capabilities](https://semgrep.dev/products/semgrep-vs-ce/) for current Community Edition coverage limits.
