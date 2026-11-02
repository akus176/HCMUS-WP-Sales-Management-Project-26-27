# Staff-mobile delivery baseline (S0 / #136)

Verified on **2 November 2026** against `main` at
`9ab252752e2d0aac1ec8c1b938ec67f0d547ea8a`.
Owner: [kyrie25](https://github.com/kyrie25); required reviewer:
[longtoZ](https://github.com/longtoZ).
Work item: [#136](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/136).

The host can run the API, but **no P0 mobile business contract is available**.
Android and physical-device verification are blocked by the environment described below.
This baseline permits planning and scaffolding after S0 review; it is not evidence of a
working mobile workflow. All mobile functions remain Planned in the
[function list](final-submission/mobile-function-list.md).

## Current readiness and evidence

| Dependency | Observed readiness | Evidence and limit |
| --- | --- | --- |
| Shared API | Host startup verified; no business routes | [Program.cs](../src/CoffeeShop.Api/Program.cs) registers only OpenAPI and health checks. Runtime `/health` returns `200 Healthy`; `/api/v1` returns `404`. |
| OpenAPI | Development document available, contract missing | [Captured document](evidence/mobile-baseline-136/openapi.json): OpenAPI 3.1.1, zero paths, zero schemas, no security scheme. Bytes match across repeated fetches and two process starts at the same URL. This does not establish reproducibility across different hosts/URLs; `servers` contains the request origin. |
| PostgreSQL | Persistence exists; API is not composed with it | [DbContext](../src/CoffeeShop.Infrastructure.Postgres/CoffeeShopDbContext.cs) and [registrations](../src/CoffeeShop.Infrastructure.Postgres/PostgresServiceCollectionExtensions.cs) cover catalog, identity, customers, inventory, and sales. API startup calls neither the persistence registration nor a database health check. `Healthy` proves no database connection. |
| Database verification | Local model checks pass; local live server unavailable | Five `InitialMigrationTests` pass, including pending-model and migration-script checks; they do not connect to PostgreSQL. No PostgreSQL service/process was discovered and `pg_isready -h 127.0.0.1 -p 5432` returned no response. The [successful baseline CI run](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/41399593589) supplies separate PostgreSQL 18 integration evidence. No local migration/seed round trip was performed. |
| Identity and permissions | Domain/mappings and application seams exist; HTTP enforcement missing | Identity entities, refresh-token mapping, and `ICurrentUser` exist. API has no authentication/authorization services, middleware, login/refresh/logout, or effective-permission endpoint. A JwtBearer package reference alone is not authentication. |
| Windows shared state | Direct-database midterm composition remains | [App.xaml.cs](../src/CoffeeShop.WinUI/App.xaml.cs) uses in-process services. [PosOperatorSession](../src/CoffeeShop.WinUI/ViewModels/PosOperatorSession.cs) is operator selection, not login. Final WinUI consumption of the shared authenticated API is not implemented. |
| Mobile app/toolchain | Not scaffolded | `apps/staff-mobile` does not exist. Host versions: .NET 10.0.403, Node v26.5.0, npm 11.17.0. These are observations, not an Expo-supported version pin; #138 must select compatible pinned tooling. |

The API health test passes (1/1). Sanitized runtime results are in
[api-probe.json](evidence/mobile-baseline-136/api-probe.json) and
[network-probe.json](evidence/mobile-baseline-136/network-probe.json).
No business API integration, login, mobile launch, or multi-client synchronization is claimed.

## P0 dependency and ownership map

Issue ownership below was read from GitHub on the verification date. A named owner is
accountable for the cited existing issue, not an assertion that its work is complete.
All linked S1/S2 issues are open. #137 defines the API baseline; #142 implements/finalizes
P0 contracts. Neither is evidence of an available endpoint.

| P0 dependency | Existing reusable behavior / exact missing boundary | Owner and GitHub work item | Completion evidence still required |
| --- | --- | --- | --- |
| Authentication | Identity entities/mappings exist; credential verification, active-account checks, JWT issuance, rotated refresh/revocation and logout are not exposed | akus176: [#137](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/137); kyrie25: [#143](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/143) mobile sessions | API valid/rejected/inactive login and refresh tests; mobile secure storage, concurrent refresh, and logout tests |
| Effective permissions | `ICurrentUser` and some application permission checks exist; authenticated permission resolution and HTTP policies are missing | akus176: #137 / [#142](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/142); JakeConal: [#144](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/144) shell | API allowed/forbidden/multi-role tests and mobile route-guard tests |
| Preparation tickets and transitions | `FulfillmentOperations.cs` has operational queries and `orders.fulfill` transition handling; no preparation DTO/query/detail/transition HTTP contract | akus176: #137 / #142; mobile workflow remains under [#95](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95), task 4.2 | Permission-filtered ticket detail; Confirmed → Preparing → Ready; stale/cancelled conflict and actor tests |
| Discount approvals | `DiscountCommandHandler` checks `discounts.approve`; operational orders expose pending IDs; no protected pending inbox/detail/decision HTTP contract | akus176: #137 / #142; mobile workflow #95, task 4.3 | Pending/empty/forbidden queries, approve/reject, authoritative totals and conflict tests |
| Ingredient balances / low stock | Inventory readers and low-stock query exist; no authenticated list/search/low-stock HTTP contract | akus176: #137 / #142; mobile workflow #95, task 4.4 | Authorized/forbidden/empty/filter query tests and shared inventory evidence |
| Stock adjustment | `InventoryCommandHandler.RecordStockAdjustmentAsync` records changes/history; no HTTP authorization, authenticated actor binding, or documented retry/idempotency contract | akus176: #137 / #142; mobile workflow #95, task 4.4 | Required reason, invalid quantity, actor, permission, conflict/retry and immutable movement tests |
| Courier assignments | Delivery persistence and operational queries exist; no query scoped to the authenticated courier, nor protected contact/address detail contract | akus176: #137 / #142; mobile workflow #95, task 4.5 | Own/other courier isolation, empty list, limited fields, session/completion cleanup |
| Delivery transitions | `DeliveryCommandHandler` and domain transitions exist; HTTP must bind the actor and enforce assignment/permissions rather than trusting client-supplied actor IDs | akus176: #137 / #142; mobile workflow #95, task 4.5 | Out-for-delivery/delivered/failed tests, mandatory failure reason, invalid/stale/cancelled state and cross-courier rejection |
| OpenAPI, transport and errors | `CoffeeShop.Contracts` has no DTO source files; no P0 schemas or RFC 7807 HTTP mapping | akus176: #137 / #142; kyrie25: #143 generator/transport | Deterministic implemented contracts, validation/401/403/404/409/network handling, no generated-client drift |
| Manual refresh, reconnect and online-only writes | No mobile cache/connectivity boundary; no business HTTP query to refetch | vt-Toan: [#145](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/145); depends on #139/#142/#143 | Reconnect/refetch, conflict recovery, and no queued/offline-success writes |
| Authenticated operational events | No hub, authenticated groups, or after-commit publisher. #137, #142 and #145 explicitly exclude real-time delivery | **External planning blocker:** #95 tasks 2.5/4.7 have no assigned implementation issue. kyrie25 tracks this gap in #136; no teammate is assumed to own its implementation | Assign and review a backend hub/publisher issue and mobile subscription issue; verify assignment isolation, rollback suppression and missed-event recovery |
| One mobile-to-Windows shared-state demonstration | Windows still uses direct PostgreSQL; no final API-backed WinUI integration or simultaneous-client proof | **External planning blocker:** #95 task 6.4 has no assigned implementation issue. kyrie25 tracks the missing ownership in #136 | Assign the final WinUI/API integration and shared-state demonstration work; show both clients observe one committed state and preserve audit/conflict rules |

The later workflow-screen issues under #95 are not yet assigned; #142 owns their backend
dependencies only. The two explicit external blockers above must receive implementation
owners/work items before those workflows can be claimed complete. Manual refresh does
not satisfy the specified authenticated-event requirement. Existing assignment creation
is a P1 manager action; P0 courier tests still need a backend-created assigned-delivery fixture.
This baseline does not create P1/P2 work or implement the missing boundaries.

## Reproduce the verified host path

From the repository root, in PowerShell:

```powershell
dotnet test tests/CoffeeShop.Api.Tests --configuration Release
dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~InitialMigrationTests
dotnet run --project src/CoffeeShop.Api --configuration Release --no-launch-profile -- --environment Development --urls http://127.0.0.1:5232
```

In a second PowerShell terminal:

```powershell
(Invoke-WebRequest http://127.0.0.1:5232/health).Content
$contract = Invoke-RestMethod http://127.0.0.1:5232/openapi/v1.json
@($contract.paths.PSObject.Properties).Count
(Invoke-WebRequest http://127.0.0.1:5232/api/v1 -SkipHttpErrorCheck).StatusCode
```

Expected at this baseline: `Healthy`, `0`, `404`. Stop the API with Ctrl+C.
Development OpenAPI is enabled explicitly; the ordinary HTTP launch profile uses port
5232 as well. `/api/v1` is the planned prefix, not a usable current business base path.
For PostgreSQL provisioning/migrations, follow the existing [setup](midterm-postgres-role.md)
and [seed instructions](midterm-seed-data.md) with a disposable database and locally supplied
credentials. Keep PostgreSQL on the host/loopback; Android never needs database credentials
or a firewall opening for port 5432.

## Android and device-network readiness

**Exact local blocker:** `adb`, `emulator`, and `java` are not on PATH;
`ANDROID_HOME`, `ANDROID_SDK_ROOT`, and `JAVA_HOME` are unset. The standard Windows SDK
directory `%LOCALAPPDATA%\Android\Sdk`, Android Studio directory
`C:\Program Files\Android\Android Studio`, and `%USERPROFILE%\.android\avd` are absent.
No emulator/ADB/Android Studio process was discovered. A custom installation elsewhere
has not been ruled out. No authorized Android device was available for verification.

Recovery is tracked by kyrie25 in #138 (emulator/app tooling) and JakeConal in
[#139](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/139)
(environment/LAN configuration). Install Android Studio and its SDK/platform tools/emulator,
use its bundled JDK, and create an AVD with Device Manager using the API level supported
by the Expo version selected in #138. Accept SDK licenses through the local tools.
After that setup, the following diagnostic commands are **not yet executed on this host**:

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
& "$env:ANDROID_HOME\emulator\emulator.exe" -list-avds
# Replace the example with a name returned above; run in a separate terminal.
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd '<existing-avd-name>'
& "$env:ANDROID_HOME\platform-tools\adb.exe" wait-for-device
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices -l
& "$env:ANDROID_HOME\platform-tools\adb.exe" shell am start -a android.intent.action.VIEW -d http://10.0.2.2:5232/health
```

Run the loopback API command first. `10.0.2.2` is the standard Android Emulator alias
for the host loopback interface; emulator `localhost` means the emulator itself.
The browser must show `Healthy` before recording emulator reachability as verified.
The actual Expo install/start command remains #138 work because no mobile app exists.

For a physical device, use the same trusted LAN as the host and restart the API with:

```powershell
dotnet run --project src/CoffeeShop.Api --configuration Release --no-launch-profile -- --environment Development --urls http://0.0.0.0:5232
```

Open `http://<host-private-lan-ip>:5232/health` in the device browser. Replace the placeholder
locally; do not commit the address. Host-to-own-LAN-adapter HTTP was verified as
`200 Healthy` using this bind, but **no remote device request was verified**. The current
Windows network profile is Public. No firewall/profile changes were made. If inbound
access is blocked, #139 must verify a trusted private-network setup and a temporary TCP
5232 rule restricted to the Private profile and LocalSubnet; do not disable the firewall
or expose PostgreSQL. Guest Wi-Fi isolation, VPNs, and inbound rules can block a device
even when the host's own adapter request succeeds. Stop the listener after the check.

After a trusted Private profile is established, an elevated PowerShell terminal can create
and later remove this narrowly scoped rule (not executed for this baseline):

```powershell
New-NetFirewallRule -Name CoffeeShop-Mobile-Dev-5232 -DisplayName 'CoffeeShop mobile health development' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5232 -Profile Private -RemoteAddress LocalSubnet
# After the device check, remove only this temporary rule.
Remove-NetFirewallRule -Name CoffeeShop-Mobile-Dev-5232
```

These HTTP checks carry only non-secret health data. #139 must define the development
build's cleartext/TLS policy and validate the eventual non-secret API-origin setting;
the ASP.NET development HTTPS certificate is not automatically trusted by Android.
A browser check alone does not verify native app networking, auth, or an Expo launch.

## S1 handoff and review gate

- #138: select pinned Expo-compatible Node/package tooling, install and verify Android,
  scaffold and capture an actual starter-screen launch. Do not infer Expo support from
  the host's installed Node version.
- #137 / #142: define then implement the missing authenticated contracts above and their
  permissions, actor binding, error/idempotency semantics and OpenAPI evidence. Until
  those land, client fixtures must be labeled fixtures; no guessed route is a server contract.
- #139: implement validated emulator/device API origins and verify the actual device path.
  #140 (vt-Toan) owns shared controls; #141 (longtoZ) owns S1 quality/CI evidence.
- Track the unassigned event and WinUI shared-state work explicitly under #95/#136.
  The merged local task sequence adds baseline task 1.1; #95 still starts with the older
  scaffold task 1.1. Do not check that scaffold item for this baseline or infer that
  OpenSpec planning completion means implementation is complete.
- **longtoZ must record a review decision in an issue comment before #136 closes and S1
  begins.** Passing checks or this PR alone do not provide that review. The issue stays open
  at the implementation handoff; remaining environment/ownership gaps remain visible.

Mobile will consume the shared ASP.NET Core API only. Domain/Application services remain
authoritative for all transitions, permissions, audit actors and validation. No mobile
PostgreSQL connection, duplicated JavaScript business rules, offline write queue,
production credentials, or customer records are introduced by this baseline.
