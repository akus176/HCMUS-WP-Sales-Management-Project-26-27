# Shared staff identity (#150)

The shared API issues ten-minute HS256 access tokens and opaque fourteen-day refresh tokens. Refresh tokens are stored only as SHA-256 hashes. Each renewal rotates the token; reuse revokes the entire family. Logout revokes the presented family and `/auth/logout-all` revokes every refresh family owned by the authenticated account. Access tokens otherwise expire naturally; every protected request also checks the current account state and resolves current roles/permissions from PostgreSQL. Role removal and account deactivation therefore affect the next request without waiting for JWT expiry.

## Local setup

Apply the existing migrations using the [API foundation procedure](api-foundation.md). Set these through ignored local environment configuration or a local secret provider:

- `ConnectionStrings__CoffeeShop`: PostgreSQL connection.
- `Jwt__Issuer` and `Jwt__Audience`: agreed non-secret identity namespace and staff-client audience.
- `Jwt__SigningKey`: a randomly generated secret of at least 32 bytes. There is no default or development fallback.

Never put signing keys, passwords, or returned tokens in tracked settings, command arguments, screenshots, or evidence. Authentication responses use `Cache-Control: no-store`. Use HTTPS outside trusted local development; HTTP examples from the baseline are health-only and are not permission to send staff passwords over an untrusted network.

Run the following from the repository root after configuring the connection and JWT settings:

```powershell
dotnet run --project src/CoffeeShop.Api -- --seed-staff-roles
```

This idempotently creates six operational role bundles and their explicit permission links. It never creates a default account or password. For a locally provisioned account, supply `ProvisionStaff__Username`, `ProvisionStaff__Email`, `ProvisionStaff__FullName`, `ProvisionStaff__Password`, and `ProvisionStaff__Roles` through local environment/secret configuration, then run:

```powershell
dotnet run --project src/CoffeeShop.Api -- --provision-staff
```

Run the role-seed command again after deploying table creation (#229) to grant `tables.manage` to existing Admin and Manager roles. Cashiers retain `orders.create` for table selection but cannot create tables. No schema migration or account reprovisioning is required for this grant.

Roles are comma-separated exact codes: `Admin`, `Manager`, `Cashier`, `Barista`, `InventoryStaff`, `DeliveryStaff`. Provisioning requires a 12–1024-character password, hashes it with ASP.NET Core's versioned salted PBKDF2 password hasher, and refuses to overwrite an existing username/email. Remove the provisioning password from the environment afterward. This is a local operator provisioning path; account administration and final-administrator protection remain Windows task 10.6.

## Administrator Google sign-in (#227)

1. In Google Cloud, configure the OAuth consent screen and create a **Desktop app** OAuth client. Add the approved demo accounts as test users if the consent screen is in Testing. Use the desktop client's ID and secret for the API. WinUI fetches the public client ID and uses a dynamic IPv4 loopback redirect with PKCE.
2. Select an existing active `Admin` account or provision one with `ProvisionStaff__GoogleOnly=true`, `ProvisionStaff__Roles=Admin`, and the profile fields above. Run `--provision-staff` without a password. The command prints the new local account ID. The local account email must match the approved Google email. The deployment operator provisions Google administrators. Windows account creation still provisions password-based staff.
3. Obtain the approved Google account's stable `sub` and verified email from a Google identity assertion verified during private enrollment. Configure one link per administrator with the local account ID, Google `sub`, and email. Keep the assertion private. Use the keys shown in [.env.example](../.env.example). For Compose, put those keys in the ignored `.env.google-admin` next to `compose.yaml`, then recreate the API container. For a standalone API, use environment variables or a private configuration provider. Use successive link indices for multiple administrators.
4. Set `GoogleAdmin__Enabled=true` for the final deployment and restart all API instances together. Validate at least one configured, active administrator before the demo. The API denies password sign-in and pre-existing password sessions for all Admin accounts in this mode. New Google-only accounts have no usable local password. Local and CI environments can leave Google disabled for their existing credential fixtures.
5. In WinUI, choose **Administrator sign-in with Google**, complete sign-in in the default browser, and return to WinUI. Use an HTTPS API address for remote connections. WinUI stores only the app refresh token using its current Windows-protected storage. Mobile staff continue to use username and password.

The link grants sign-in to an existing account with an active Admin role and matching email. It never provisions an account or adds a role. To remove or change the link, update private configuration and restart the API. Access and refresh tokens bound to the earlier link are rejected. Deactivating the last active linked administrator is also blocked in Google mode. Keep the client secret, enrollment claims, callback URL, codes, and tokens out of screenshots, logs, and evidence.

For native acceptance, use a linked test administrator and an unlinked Google test account. Verify the linked account reaches the Windows workspace and restores after an app restart. Sign out, verify the unlinked account is denied, and check that canceling the browser flow returns to an enabled login form. Record the result and HTTP status only. The automated API tests cover inactive accounts, mismatched subject or email, wrong audience, invalid signatures, and app token logout and renewal.

## Implemented HTTP contract

All paths have prefix `/api/v1` and return RFC 7807 failures.

| Method/path | Input/result | Authorization |
| --- | --- | --- |
| POST `/auth/login` | `{username,password}` → `{accessToken,refreshToken,expiresAt,user:{id,fullName,roles,permissions}}` | Anonymous. Generic 401 for unknown, invalid, inactive, or Google-mode administrator accounts. |
| GET `/auth/google/config` | `{clientId}` for the Windows OAuth client | Anonymous. 404 when disabled. |
| POST `/auth/google` | `{code,codeVerifier,redirectUri}` → same app token response as `/auth/login` | Anonymous. Generic 401 for invalid or unlinked identities. 503 when disabled or temporarily unavailable. |
| POST `/auth/refresh` | `{refreshToken}` → rotated token response | Anonymous; generic 401 for expired, revoked, reused, or unknown tokens |
| GET `/auth/me` | Current staff identity, roles, effective permissions | Bearer token and active persisted account |
| POST `/auth/logout` | `{refreshToken}` → 204 | Bearer token; token must belong to caller |
| POST `/auth/logout-all` | No body → 204 | Bearer token; account derived from caller |
| POST `/users` | `{fullName,username,email,phone?,role,password}` → public staff account; 400 invalid, 409 duplicate | `users.manage`; server-issued administrator session |

Existing category writes require `menu.manage`. All named operational permission policies are registered for subsequent workflow endpoints. Admin and manager can manage the menu; cashier, barista, inventory, and delivery roles cannot. Barista preparation uses the existing Application permission `orders.fulfill`, rather than granting cashier order-creation rights. Role bundles are explicit in `StaffRoleSeed.Bundles`; multi-role accounts receive their permission union. Neither client-side role names nor caller-supplied permission fields are trusted.

`POST /tables` accepts `{ "tableNumber": "T12", "seats": 4 }` and returns the new available table (201). It requires `tables.manage`; invalid inputs return 400, and a duplicate table number returns 409. Counter refreshes the shared table list and selects the new table for an unsaved dine-in ticket. `GET /pos/tables` remains available to staff with `orders.create`.

Refresh, login, and logout serialize through a PostgreSQL user-row lock. Two requests presenting the same refresh token cannot both rotate successfully: the second observes revocation, rejects reuse, and revokes the successor family. Independent login sessions remain separate families. Security audits contain event type, account identifier where available, time, outcome, and remote IP. Persistence also records account-state and role-assignment changes with identifier-only metadata, in the same save as the change. Passwords and raw tokens are never audit fields.

Deactivation and password-hash changes revoke every refresh family atomically with the account update, under the same user-row lock. Reactivation cannot restore a previously revoked session.

The administrator creation endpoint assigns one of the six seeded operational roles, hashes the initial password, and records `account_created` with the creator's ID and target account ID. Neither the response nor security-event query exposes the password or hash. The active identity spec still requires profile/role update and reactivation; issue #228 implements creation only, and its audit proposal about password reset does not remove those requirements. Windows 10.6 stays open until those remaining administration operations are implemented and verified.

## Verification

Run the complete API suite against a disposable PostgreSQL server using `COFFEESHOP_TEST_POSTGRES` as a local admin connection. Each integration test creates a uniquely named database, applies committed migrations, and drops that test database after execution:

```powershell
dotnet test tests/CoffeeShop.Api.Tests --configuration Release
```

The repository PostgreSQL CI job already supplies this variable and runs the API suite. Without it, PostgreSQL tests are explicitly skipped; that run alone is not security verification. The tests exercise real HTTP, real signed JWTs, persisted role/permission joins, refresh-family rotation/reuse, concurrent refresh, logout ownership, account-wide revocation, inactive accounts, role removal, minimal token claims, and audit redaction. They use public test-only credentials and a test-only signing key, never a deployed secret.

Scope: Windows 10.1–10.5/10.7 and Mobile 2.1. Feature endpoint implementation remains #151/#142; client credential storage remains #143/#152. No client receives a database connection or database password.

## Recorded verification — 3 November 2026

Verified from `feat(identity)/staff-auth-permissions`, based on merged API host `23e6ccc`, using .NET SDK 10.0.403 and a disposable PostgreSQL 18.0 cluster bound to host loopback. Database suites used `COFFEESHOP_TEST_POSTGRES`; no test was skipped.

| Command | Actual result |
| --- | --- |
| `dotnet test tests/CoffeeShop.Api.Tests --configuration Release --logger "trx;LogFileName=identity-api.trx" --results-directory TestResults` | 20 passed, 0 failed, 0 skipped |
| `dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --logger "trx;LogFileName=identity-infrastructure.trx" --results-directory TestResults` | 258 passed, 0 failed, 0 skipped |
| `dotnet test tests/CoffeeShop.Domain.Tests --configuration Release --logger "trx;LogFileName=identity-domain.trx" --results-directory TestResults` | 413 passed, 0 failed, 0 skipped |
| `dotnet format src/CoffeeShop.Api/CoffeeShop.Api.csproj --no-restore --verify-no-changes --verbosity quiet` and the same check for `tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj` | Exit 0 |
| `git diff --check` | Exit 0 |

The ignored `TestResults` directory contains exact local TRX results; the PostgreSQL CI job reruns the API and infrastructure suites and uploads its TRX artifacts. Provisioning is tested by persisting an explicitly assigned multi-role account, signing in through HTTP, verifying the persisted password differs from the input, and rejecting a repeated provisioning attempt. Missing signing configuration fails without echoing a signing key. JWT tests reject tampering, wrong issuer/audience, and expiry; refresh tests reject unknown/expired tokens and prove rotation, reuse, concurrent reuse, family isolation, logout ownership, password-change invalidation, and deactivation/reactivation invalidation.

Tracking note: #150 maps Mobile 2.1 to the authentication foundation in the local mobile plan. Canonical parent #95 still uses an older checklist where 2.1 means workflow queries. That unrelated checkbox must remain unchanged; this delivery's mobile authentication evidence is tracked by #150.
