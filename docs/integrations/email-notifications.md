# Email notifications

Email is an optional channel of the existing Notifications module. Event producers create canonical notifications through `INotificationService`; the durable `NotificationDelivery` queue and `NotificationDeliveryWorker` handle email and push. SMTP failure does not block saving the in-app notification. Producers must not send mail directly or create a second event stream.

## Connector and profile scopes

The connector is standards-based SMTP. It has no provider/vendor selector and makes no assumptions about a mail company. It can connect to internal/on-premises or cloud mail servers when those servers expose a compatible SMTP endpoint and the configured authentication method. A cloud service that exposes only a provider-specific HTTP API is not supported by this connector; a future API adapter can be added behind the same delivery channel without adding a vendor dropdown.

Profiles are stored at Platform, Tenant, or Organization scope. Effective selection is Organization → Tenant → Platform. An explicitly saved disabled profile is an override and stops fallback; deleting/resetting that override returns resolution to the next scope. An Organization profile is only used for notifications with a resolved Organization ID. Unscoped notifications use the Tenant or Platform profile.

Each profile contains:

- SMTP hostname and port.
- Required STARTTLS or implicit TLS (`TLS on connect`). STARTTLS must be negotiated successfully; the connector fails closed if it is unavailable. Certificate-chain and revocation checks remain enabled. There is no plaintext fallback.
- Username/password SMTP authentication, or Trusted Relay when the selected destination has explicit deployment authorization.
- Sender address and display name, optional reply-to, and enabled/disabled state. The application validates address syntax; the SMTP server must authorize the sender. `Send Test Email` can confirm server acceptance for that sender but does not confirm mailbox delivery.

OAuth2/XOAUTH2 is not available in this release. The settings API and UI report it as unavailable and reject attempts to save it. Do not configure OAuth as username/password or assume a provider supports SMTP AUTH. Trusted Relay has no user-configurable network allowlist: a deployment operator must explicitly approve the exact hostname and scoped network before the option can be used.

SMTP passwords are encrypted at rest with the existing secret-protection service. Plaintext is accepted only on the server-side update request and is never returned by settings, logs, audit records, or browser state. The UI displays whether a password is stored; leaving the password blank preserves the stored value, while an explicit clear action removes it. Secret decryption failure makes the profile unavailable for sending.

## Network and transport security

SMTP destinations are treated as server-side egress. The connector resolves DNS once for each operation, validates every resulting address against the destination policy, and pins the socket connection to an approved address while validating TLS against the original configured hostname. Loopback, link-local, shared, reserved, multicast, and other special-use targets stay blocked even when a rule lists them. A private SMTP server must resolve to an RFC1918 IPv4 or IPv6 ULA address and match a deployment-owned allowlist for both its exact hostname and scoped CIDR. More-specific Organization or Tenant rules take precedence over broader rules. A profile cannot grant itself network access.

Trusted Relay additionally requires an exact hostname and scoped network entry in the separate Trusted Relay allowlist. Use this mode only when the mail relay itself applies the required network restrictions and the deployment owner has explicitly authorized the route. Keep allowlists narrow and review them as part of deployment configuration. Never expose SMTP credentials in tracked settings files, source control, test output, or logs.

## Administration and permissions

The Email Notifications settings screen reads authoritative profile data from the API. `EmailNotifications.View`, `EmailNotifications.Manage`, `EmailNotifications.Test`, and `EmailNotifications.Logs` are separate tenant-scope permissions. Organization override operations also require the existing explicit Organization Settings access for that Organization. Platform profiles use the existing `PlatformSettings.Read` and `PlatformSettings.Write` policies. New email permissions are not added to an existing role or protected Admin baseline automatically. Effective authorization, including Active Deny and tenant/organization scope, remains authoritative on the server.

`Test Connection` only connects and authenticates; it does not send a message. `Send Test Email` is a separate operation and only targets the authenticated active user's own account address. Test operations are rate-limited and audited. Failure responses and delivery logs contain sanitized status information, not SMTP host credentials, message bodies, or recipient addresses.

## Notification delivery behavior

Email remains opt-in per event and user preference. The `EmailAvailable` indicator reflects whether a usable profile is configured for the selected Tenant scope. On event creation, an email delivery intent is queued only when the user's email preference is enabled and a profile is available. Before sending, the worker rechecks the current same-tenant active user, preference, and effective profile; a preference turned off or a disabled/missing profile cancels the queued item. A settings lookup failure does not prevent the in-app notification from being saved.

The worker uses the existing unique notification/channel key, lease, bounded retry/backoff, and idempotent event producers. The application reports `Pending`, `Retrying`, `Accepted`, or `Failed` as appropriate. SMTP `Accepted` means that the SMTP server accepted the submission; it does not mean inbox delivery, and it is not reported as `Delivered` without a receipt. An SMTP server may accept a message while the application loses the acknowledgement. Retrying that ambiguous outcome can resend the message, so SMTP cannot guarantee exactly-once delivery. Existing event-specific recipient authorization remains in force; external attendee addresses are not added as recipients by this integration.

The mail adapter supplies a plain-text alternative and escaped HTML content from the canonical notification. It sets the HTML language and text direction from that content, so existing producers that emit Arabic produce RTL mail and English producers remain LTR. The integration preserves localized text when a producer emits it; this release does not add a per-user email-language preference or translate English-only event content. Deep links use public identifiers and must revalidate authorization when opened; an email link never grants access. Do not put internal persistence IDs, secrets, or unnecessary personal data into subject/body content.

## Schema and deployment

Profile persistence and Organization attribution require an additive EF migration. Apply it through the documented database delivery gate after verifying the exact Development target, reviewing generated SQL, and confirming an independent backup. Do not reset any database. The implementation is not deployment-ready until the target schema is synchronized and the migration history and physical schema are verified.

Configure destination allowlists through deployment-owned configuration, not tenant profiles. Store credentials through the application settings UI or another approved secret-management path. Start with the Platform or Tenant profile, verify connection, then send a test email to the signed-in administrator's own address. Configure an Organization override only when the organization has an explicit authorized need. Review delivery logs for sanitized outcomes and verify normal in-app delivery still works when SMTP is unavailable.

## Product Owner browser UAT

After the migration is deployed to the authorized Development environment, run the feature rows in `docs/quality/MASTER-UAT-CHECKLIST.md` using separate users and Organizations with positive, missing, and Active Deny grants. Verify English and Arabic including RTL, narrow viewport behavior, profile fallback and disabled override, secret preservation/clear, Test Connection versus Send Test Email, own-address restriction, rate limiting, log scoping, internal relay allowlisting, TLS/auth failures, email preference opt-in, retry/dedupe, and in-app availability during SMTP failure. Use a test relay and controlled accounts. Browser/runtime UAT is not complete based on source inspection, build, or isolated unit tests.
