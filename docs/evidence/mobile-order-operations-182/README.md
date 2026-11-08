# Mobile cashier and manager order operations — #182

Scope: [#182](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/182),
OpenSpec Mobile **5.1 / 5.2**, and canonical [#95](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95)
items **7.3 / 7.4 / 7.5**. Prerequisites #143, #144, #142, #151, #155 and #156 were closed at implementation start.

## Implementation

- **Counter:** search the shared order snapshot by number/table, filter order state,
  inspect immutable line/add-on snapshots, table occupancy, fulfillment, paid amount and balance.
  All reads have loading/error/empty states and manual refresh.
- **Payments:** record cash, card or e-wallet payments through the generated API client.
  Card/e-wallet require a reference. Review and confirmation precede submission; a
  synchronous guard prevents repeated taps. The native Expo Crypto UUID identifies
  one payment attempt. An unknown outcome retains the exact command in memory,
  including while returning to order search. Explicit retries reuse that command;
  they do not generate another payment ID. An unsent review is rejected if the refreshed
  balance has fallen below its amount. API responses and refetched summaries supply
  the confirmed paid amount/balance. This records an existing supported payment;
  it does not charge a card or integrate a payment gateway.
- **Manager:** Approvals → Manage cancellations and deliveries opens protected order
  search. Cancellation needs a trimmed reason (maximum 255 characters), review and
  confirmation. Assignment requires a ready, unassigned delivery and a staff option
  returned by the eligibility endpoint. Both actions require their effective permission.
- **Recovery:** conflicts refetch authoritative state, 403 hides retained records,
  404 removes details, and offline writes are disabled and never queued. Payment
  identifiers are session memory, not a persistent offline business store. After
  leaving the module or restarting, refresh and reconcile the server balance before
  entering a new payment. Server state and permissions remain authoritative.
- Existing discount and barista screens, API/domain rules, database schema and generated
  contracts are unchanged. Order reads use the existing `orders.create` policy;
  the UI does not elevate users with only a mutation permission.

## Executed verification

Run commands from the repository root unless an app directory is stated.

| Check | Result |
| --- | --- |
| `npm ci` in `apps/staff-mobile` and `contracts/mobile-p0` | Passed using pinned metadata |
| `npm run check` in `apps/staff-mobile` | TypeScript, zero-warning lint, formatting and **165 tests / 17 suites passed**, 15.326s |
| New order component tests | 22 passed: search/table navigation, refreshed state, payment guards/retry identity, card/e-wallet references, cancellation/assignment, conflicts, authorization, offline behavior and unavailable details |
| PostgreSQL-backed API suite | **41 passed, 0 failed, 0 skipped**, 6m29s |
| `npm run api:check` in `apps/staff-mobile` | Passed; generated TypeScript has no content drift |
| `npm run check` in `contracts/mobile-p0` | Passed; deterministic OpenAPI/TypeScript generation and contract type checks |
| `openspec validate add-staff-mobile-app --strict` | Passed |
| `dotnet format tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj --verify-no-changes --no-restore --include tests/CoffeeShop.Api.Tests/StaffIdentityHttpTests.cs` | Passed |
| Native Android debug build/install | Passed on Android 16 / API 36 x86_64 emulator; 326 tasks, 8m05s |
| Final-source Android export | Passed; 1,257 modules, 2.8 MB Hermes bundle |
| `npm run test:e2e:cashier` in `apps/staff-mobile` | **1/1 passed**, 65.071s ([JUnit](cashier.xml)) |
| `npm run test:e2e:manager-orders` in `apps/staff-mobile` | **1/1 passed**, 105.416s ([JUnit](manager-orders.xml)) |
| Fixture `-Action Verify` after both native flows | **3/3 passed** from a separate manager API session: exact paid balance, cancellation/table release, assigned courier |

PostgreSQL command:

```powershell
$env:COFFEESHOP_TEST_POSTGRES = 'Host=127.0.0.1;Port=55482;Database=postgres;Username=test;Pooling=false'
dotnet test tests/CoffeeShop.Api.Tests --configuration Release `
  --logger 'trx;LogFileName=api-postgres-182.trx' `
  --results-directory "$env:TEMP\coffeeshop-cashier-182-results"
```

The suite used a new loopback-only disposable PostgreSQL 18.0 cluster. Each test
created and dropped a unique database. The new payment regression sends three
concurrent requests per cash/card/e-wallet payment ID, checks exactly one initial
record per method, rejects changed retry data, verifies the shared paid balance,
and checks persisted cashier actors and rejection after cancellation. The manager
regression checks not-ready/ineligible/forbidden assignment, unchanged rejected
state, successful assignment and manager history, required cancellation reasons,
forbidden/repeated cancellation, trimmed persisted reason and manager history.
Existing integration cases also verify dine-in occupancy/release across clients.

Native build used a short verification checkout at `F:\cs182` to avoid Windows
path limits and JDK 17 / the existing task-local Android SDK. Command:
`android\gradlew.bat -p android :app:assembleDebug -PreactNativeArchitectures=x86_64 --console=plain`.
APK SHA256: `48CF41ACB67F64D0ED9D552BC120771B9DE1A1AC23A9CBFC1AEEF1448DC66524`.
Native acceptance used fresh synthetic orders in the disposable API database.
Visually reviewed screenshots: [payment review](cashier-review.png),
[confirmed payment and zero balance](cashier-paid.png),
[confirmed cancellation](manager-cancelled.png), and
[confirmed eligible courier assignment](manager-assigned.png).

Earlier attempts exposed two test-environment issues: Android System UI ANR before
login, and Maestro backspacing from the middle of a retained search query. Stopping
the build daemon/dismissing the observed ANR and reopening the manager search between
actions resolved them. Neither assertions nor timeouts were weakened. After both
final native flows passed, the separate-session script initially failed because
PowerShell retained the REST array as a nested pipeline object. Explicit enumeration
fixed the script; its unchanged state assertions then passed 3/3. No product change
was needed for these failures.

## Reproduce Android workflow verification

Use the [mobile setup guide](../../../apps/staff-mobile/README.md) and
[environment guide](../../mobile-environment-139.md). The native build requires the
new pinned `expo-crypto` module; an older installed APK alone is insufficient.

1. Start an isolated loopback API backed by a disposable database; migrate and provision
   synthetic Manager, Cashier and DeliveryStaff accounts using the existing API commands.
   Create one available synthetic dining table in that database. No real shop account
   or existing database is needed. Keep tokens/passwords in local process variables.
2. Set `COFFEESHOP_MOBILE_TEST_API_BASE_URL=http://127.0.0.1:5232/api/v1`,
   `COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME`, `COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD`,
   `COFFEESHOP_MOBILE_TEST_TABLE_ID` and `COFFEESHOP_MOBILE_TEST_COURIER_NAME`.
3. Seed the fixture from `apps/staff-mobile`:

   ```powershell
   .\scripts\cashier-manager-fixture.ps1 -Action Seed -FixturePath "$env:TEMP\counter-182-fixture.json"
   ```

4. Build/install the Android debug app. Start Metro with the emulator profile and
   `EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:5232/api/v1`; keep it running and use
   `adb reverse tcp:8081 tcp:8081` as described in the setup guide.
5. Set `MAESTRO_STAFF_USERNAME` / `MAESTRO_STAFF_PASSWORD` to the synthetic cashier,
   and `MAESTRO_PAYMENT_ORDER` to the fixture's `paymentOrder`. Run `npm run test:e2e:cashier`.
   It signs in, searches, reviews and records 10 cash, then asserts the service-confirmed
   paid amount and zero balance.
6. Set the staff variables to the synthetic manager, `MAESTRO_CANCEL_ORDER` and
   `MAESTRO_DELIVERY_ORDER` to the fixture values, and `MAESTRO_COURIER_NAME` to the
   fixture courier's name. Run `npm run test:e2e:manager-orders`. It requires a reason,
   confirms cancellation, then searches and assigns the ready delivery to eligible staff.
7. Run the fixture script with `-Action Verify` and the same fixture path. Its separate
   manager API session checks exact paid amount, cancellation/table release, and assignment.
   Preserve sanitized JUnit/screenshots under this evidence directory, with exact counts.
   Keep audit actors and duplicate-row checks backed by the PostgreSQL suite.

Do not store local credentials, token responses, raw API logs, native build directories,
or machine-specific settings in the repository.

## Remaining merge gates

The user started the disposable API and Metro after automatic approval review rejected
their startup with only `blocked by policy`. The setup script's missing JWT
issuer/audience values were fixed before the successful native runs.

All five hosted checks passed on implementation commit `455c9a1` (run `42454161577`).
Final-head CI, mergeability and current review decisions are visible on
[PR #191](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/191).
Required reviewer is **JakeConal**. #182 remains open until that reviewer records
approval. The function list retains Planned status because no final video timestamp
was produced; this does not replace the linked native/API evidence.
