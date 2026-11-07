# Mobile inventory evidence for Issue #184

## Scope and traceability

Issue #184 implements the inventory tasks that exist in
`openspec/changes/add-staff-mobile-app/tasks.md`:

- **4.4** — ingredient balance, low-stock view, stock adjustment with a required
  reason, and movement history.
- **5.6** — prepared receipt review and posting with one stock effect per
  receipt.

The Issue #184 table also labels rows “Mobile 9.1–9.4”. Those task IDs do not
exist in the mobile change. The mobile OpenSpec file defines inventory work as
4.4 and receipt work as 5.6. The parent change's 9.1–9.4 tasks describe API
contracts and backend endpoints. This implementation follows the actual mobile
task IDs.

## API boundary checked

The mobile client calls the generated OpenAPI client. It does not access
PostgreSQL or store inventory records in a second business-data store.

The current API contract contains these required operations:

- `GET /api/v1/ingredients` — paged ingredient balances and search.
- `GET /api/v1/ingredients/low-stock` — the shared low-stock query.
- `POST /api/v1/ingredients/{id}/adjustments` — an adjustment with a unique
  request ID and mandatory reason.
- `GET /api/v1/ingredients/{id}/movements` — ingredient movement history.
- `GET /api/v1/purchase-receipts` and
  `GET /api/v1/purchase-receipts/{id}` — receipt list and review data.
- `POST /api/v1/purchase-receipts/{id}/post` — server-side receipt posting.
- `GET /api/v1/stock-movements` — recent shared movement history.

The API requires inventory permissions for each route. The mobile screen also
limits reads and actions to the effective permissions returned by the API.
Server authorization remains authoritative.

## Verification

Verified on 8 November 2026:

- `npm run check` — passed: type check, lint, formatting, 17 test suites, 154
  tests, 0 failures, and 0 skipped tests.
- `npm run smoke:android` — Android bundle export passed: 1,253 modules and a
  2.8 MB Hermes bundle. This is a build check; it is not a device interaction
  or screenshot.
- `openspec validate add-staff-mobile-app --strict` — passed.
- .NET 10 Release API build — passed with 0 warnings and 0 errors in Docker.
- PostgreSQL-backed API integration tests — 2 passed, 0 failed, 0 skipped in
  disposable Docker PostgreSQL. The tests cover adjustment permissions,
  validation, idempotency, and one-time receipt posting.

The generated OpenAPI drift script was not run on the host because the host
does not have .NET 10. The API build and PostgreSQL tests ran with .NET 10 in
Docker. GitHub Actions remains the final repository-wide check.

Use Node 26.5.0 and npm 11.17.0, as pinned by the mobile app.

```text
npm ci
npm run check
npm run smoke:android
```

The Android export command needs these local emulator values:

```text
EXPO_PUBLIC_APP_ENV=emulator
EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:5232/api/v1
```

The Android export bundles the mobile app. It does not replace a live Android
device or WinUI integration run.

The PostgreSQL-backed API integration tests are:

```text
dotnet test tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj --filter "FullyQualifiedName~InventoryAdjustment_EnforcesPermissionValidationAndIdempotency|FullyQualifiedName~ReceiptPosting_ChangesSharedBalanceAndAddsOneAuditedMovement" --verbosity minimal
```

Run this command with `COFFEESHOP_TEST_POSTGRES` set to a disposable PostgreSQL
database. The tests verify adjustment idempotency, rejection of invalid and
unauthorized adjustments without balance or movement changes, receipt retry
rejection, and one receipt movement.

## Remaining cross-client gate

This run verifies the API and PostgreSQL behavior. It does not claim a live
WinUI screen result. On the current source, `InventoryAuditWorkspace` is still
registered by the direct PostgreSQL client, and the API client does not yet
register a WinUI inventory API source. Issue
[#153](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/153),
owned by `@vt-Toan`, is still open for that Windows migration.

Keep the WinUI parity acceptance evidence pending until the team runs WinUI and
mobile against the same temporary database and captures the server-confirmed
balance or movement in both clients. No screenshot or Windows UI run is claimed
by this evidence file.
