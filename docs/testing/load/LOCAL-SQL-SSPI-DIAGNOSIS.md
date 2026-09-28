# Local SQL TLS / SSPI Client Diagnosis

Date: 2026-08-31  
Scope: read-only local SQL connectivity diagnostics; no API or k6 load was run.

## Conclusion

The failure is primarily a Windows SSPI/domain identity problem with a secondary TLS/provider symptom. It is not a TCP listener, NEXUS application, login-query, or SQL database availability problem.

Evidence:

- `MSSQLSERVER` is `RUNNING` as the default instance `MSSQL17.MSSQLSERVER`, service account `NT Service\\MSSQLSERVER`.
- SQL Server is listening on TCP 1433 for IPv4 and IPv6; `Test-NetConnection localhost -Port 1433` succeeds.
- SQL Server reports `ready for client connections` after restart.
- SQL startup event 26067 reports failure to register `MSSQLSvc/Shady:1433` and `MSSQLSvc/Shady`, with Windows error `0xffffffff`, state `63`.
- SPN queries cannot reach the domain directory (`LDAP 0x51 / server down`).
- `klist` reports no Kerberos credentials cache.
- The same SSPI failure occurs with both `System.Data.SqlClient` and ODBC clients, so it is not isolated to one provider.

The SQL Server startup log also says a self-generated certificate was loaded, but `ForceEncryption` is `0` and no configured certificate thumbprint was present. There are no matching Schannel errors in the accessible Windows event logs. ODBC 18 still reports an encryption/SSL-provider message even when optional encryption and `TrustServerCertificate` are selected, but the cross-provider and cross-mode results show that certificate trust is not the primary blocker.

## Client inventory

| Client/provider | Version/evidence |
|---|---|
| Microsoft ODBC Driver 17 | installed; sqlcmd 16.0.1000.6 |
| Microsoft ODBC Driver 18 | installed; sqlcmd 17.0.1000.7 |
| System.Data.SqlClient | installed runtime provider; fails with `Failed to generate SSPI context` |
| Microsoft.Data.SqlClient | application provider DLL version 6.11.25226.3; API previously failed with `Cannot generate SSPI context` |

## Controlled connectivity matrix

Every combination below failed before `SELECT @@SERVERNAME` executed:

| Host | ODBC 17 default/trust | ODBC 18 default | ODBC 18 optional + trust | ODBC 18 strict + trust | ODBC 18 mandatory + trust |
|---|---|---|---|---|---|
| `localhost` | fail | fail | fail | fail | fail |
| `Shady` | fail | fail | fail | fail | fail |
| `127.0.0.1` | fail | fail | fail | fail | fail |

The common client errors were:

```text
Encryption not supported on the client.
SSL Provider: No credentials are available in the security package.
```

The .NET Framework provider independently returned:

```text
Failed to generate SSPI context.
```

Changing only the diagnostic client encryption mode did not restore integrated authentication. No persistent connection string was changed.

## Failure boundary

1. Name resolution and TCP reachability succeed.
2. SQL Server starts, loads its self-generated encryption certificate, and listens on 1433.
3. The client fails while acquiring the Windows security context / negotiating the encrypted integrated-auth session.
4. SQL Server records no rejected database login or query error for these attempts; the failure is before the login query and before database authorization.

## Least-invasive recovery

No automatic Windows, SPN, certificate, registry, or SQL security change was made.

The appropriate manual recovery is to restore the machine’s Windows/domain security context and then restart SQL Server once more:

- restore access to the configured domain/DNS/credential provider so SQL Server can register or resolve its `MSSQLSvc/Shady` SPNs; or
- have the DBA/security administrator confirm the approved local NTLM/Kerberos policy for this standalone SQL service account and correct the service identity/SPN mapping if domain authentication is required.

Do not disable encryption, trust arbitrary certificates, weaken Windows authentication policy, or register an SPN against an unknown account. After the approved environment correction, verify with:

```powershell
sqlcmd -S localhost -d master -E -Q "SELECT @@SERVERNAME, DB_NAME()"
```

Only after that command succeeds should the Release API and login load validation be started.
