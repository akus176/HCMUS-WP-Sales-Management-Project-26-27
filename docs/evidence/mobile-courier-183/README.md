# Courier workflow — issue #183

Scope: [#183](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/183),
legacy Mobile 4.5 and canonical [#95](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95) tasks 8.1–8.4.

The courier sees active and completed assignment summaries scoped by the API to the current user. Completed summaries omit contact and address data, and the detail endpoint refuses completed or unassigned deliveries. A protected detail route displays active delivery contact and address, opens device navigation, and offers server-confirmed transitions. Failed delivery requires a reason; conflicts refresh server state. Detail queries are session memory only, discarded on navigation or logout, and completion removes the detail and active-list caches.

## Verification

- `npm run check` in `apps/staff-mobile`: type-check, lint, formatting and 173 component/unit tests passed.
- `npm test -- --runInBand tests/delivery.test.tsx tests/delivery-detail.test.tsx tests/module-navigation.test.tsx`: 12 tests passed, covering list filters, navigation, rejected deep links, both outcomes, conflict recovery, reason validation, completion cleanup and logout cleanup.
- `npm test -- --runInBand tests/delivery-detail.test.tsx`: 7 tests passed after adding permission-revocation cleanup.
- `dotnet test tests/CoffeeShop.Api.Tests --filter FullyQualifiedName~CourierAssignment_EnforcesOwnership --no-restore -m:1` with `COFFEESHOP_TEST_POSTGRES` pointed at a disposable PostgreSQL database: passed; covers both outcomes, failure reason persistence, owner isolation, conflicts and detail access after completion.
- `dotnet format CoffeeShop.slnx --verify-no-changes --no-restore --include src/CoffeeShop.Api/DeliveryEndpoints.cs tests/CoffeeShop.Api.Tests/StaffIdentityHttpTests.cs`: passed.
- `npm run check:android-health` with the emulator profile and disposable API: Android reached `/health` with HTTP 200.

Android end-to-end acceptance has not been run. The current CI runs mobile tests and an Android bundle export, but does not run Maestro or an Android emulator; these checks do not establish the required native delivery outcomes. Task 8.3 remains open.

## Android end-to-end reproduction

1. Start a disposable migrated PostgreSQL database and shared API on `127.0.0.1:5232`. Provision synthetic Manager and DeliveryStaff users. Start the Android emulator and Metro with `EXPO_PUBLIC_APP_ENV=emulator` and `EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:5232/api/v1`; install the current debug app and run `adb reverse tcp:8081 tcp:8081`.
2. Set `COFFEESHOP_MOBILE_TEST_API_BASE_URL=http://127.0.0.1:5232/api/v1`, the synthetic manager's `COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME` and `COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD`, and `COFFEESHOP_MOBILE_TEST_COURIER_NAME` to the synthetic courier's full name.
3. From `apps/staff-mobile`, run `./scripts/courier-fixture.ps1 -Action Seed -FixturePath .maestro/results/courier-fixture.json`. Set `MAESTRO_DELIVERED_ORDER` and `MAESTRO_FAILED_ORDER` from its output, and set `MAESTRO_STAFF_USERNAME` and `MAESTRO_STAFF_PASSWORD` to the synthetic courier credentials.
4. With Maestro 2.12.1 on `PATH`, run `npm run test:e2e:courier`. Then run `./scripts/courier-fixture.ps1 -Action Verify -FixturePath .maestro/results/courier-fixture.json` using the manager environment to verify both outcomes from a separate API session.

Fixtures and Maestro outputs can contain synthetic account inputs and operational details; use only disposable data and keep `.maestro/results` untracked.
