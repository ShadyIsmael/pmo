# Enterprise Identity (Placeholder)

Enterprise identity provider integration for Microsoft Entra ID, Active Directory, SAML, OpenID Connect, and SCIM. This plugin is a placeholder and not yet implemented.

**Status:** Placeholder

## Features (Planned)

- Microsoft Entra ID (Azure AD) authentication
- Active Directory LDAP integration
- SAML 2.0 identity provider support
- OpenID Connect authentication
- SCIM provisioning for user/group sync
- Role mapping from identity provider groups

## Configuration

Configuration schema is defined but not yet functional. Settings will be available once implemented:

| Setting | Type | Description |
|---------|------|-------------|
| `provider` | enum | Identity provider type |
| `tenantId` | string | Tenant ID for the identity provider |
| `clientId` | string | Client ID for OAuth/OIDC flows |
| `scimEnabled` | boolean | Enable SCIM provisioning |

## Dependencies

- `nexus.module.identity`

## How to Enable

Enable the `enterpriseIdentity` feature flag. Note that this plugin is a placeholder — the implementation is not yet available. Watch for future releases.
