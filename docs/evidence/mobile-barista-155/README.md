# Mobile barista progression — #155

Issue: [#155](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/155).
Required reviewer: `JakeConal`. OpenSpec mapping: Mobile **4.2**, the barista
integration of Mobile **4.6**, and consumption of the existing Windows **9.6**
preparation endpoints. No broader Windows or mobile task completion is claimed.

## Behavior and boundaries

`apps/staff-mobile/src/barista/PreparationScreen.tsx` mounts only after the existing
permission boundary grants `orders.fulfill`. It calls the generated client for
`GET /api/v1/preparation-tickets`, ticket detail, and `POST .../{id}/start` or
`.../{id}/ready`. Lists use server filtering/pagination. Detail exposes only
preparation-relevant API fields, with no customer, address or payment data.

Each mutation requires confirmation, disables repeat interaction and uses the
shared online-only mutation hook. It never optimistically changes a ticket or
replays a write. Success refetches server state; `409` refreshes with an explanatory
message. A `404` removes stale detail/actions. A `403` hides retained feature
content. Transport failures make no success claim and request current server
state before another deliberate action. Shared connectivity feedback/manual
refresh remain in the app root; disconnected/reconnecting writes are disabled.

No API, Domain, persistence schema, credentials, permissions, real-time hub or
unrelated workflow is introduced. The existing preparation API does not expose
preparation timestamps or overdue thresholds; the client does not invent them.
The older canonical #95 tasks 6.1/6.2 therefore remain unchecked, as do 6.3
(pending emulator acceptance) and 6.4 (requires actual WinUI evidence).

## Executed verification

Verification started on 5 November 2026, from `3aa934c` in isolated worktree
`codex/mobile-barista-155`, then fast-forwarded to `0a257a1` after mobile quality
PR #175 merged. Final mobile checks/export ran against that combined tree.
Results below distinguish executed checks from the pending Android acceptance flow.

| Check | Command | Result |
| --- | --- | --- |
| Clean mobile install | `npm ci --no-audit --no-fund` in `apps/staff-mobile` | Passed, 1,313 packages |
| Strict TypeScript, ESLint, formatting, full Jest suite | `npm run type-check`; `npm run lint`; `npm run format:check`; `npm test -- --ci --json --outputFile=mobile-test-results.json` | Passed; 135 tests / 15 suites, 31.125 seconds; normal process exit |
| Focused screen/router tests | `npx jest --runInBand tests/barista.test.tsx tests/module-navigation.test.tsx` | 11 passed before teardown cleanup was added |
| API Release rebuild | `dotnet build tests/CoffeeShop.Api.Tests --configuration Release --no-incremental --no-restore` | Passed, zero warnings/errors |
| PostgreSQL API suite | `dotnet test tests/CoffeeShop.Api.Tests --configuration Release --no-build --logger "trx;LogFileName=api-155.trx" --results-directory <temporary-results>` | Passed; 37 tests, zero failures/skips, 7m58s |
| Generated-client drift | `npm ci --no-audit --no-fund` then `node node_modules/openapi-typescript/bin/cli.js openapi.json --output schema.d.ts` in `contracts/mobile-p0`; `git diff --exit-code -- schema.d.ts` | Passed; pinned 7.10.1, no content diff |
| Android native build/install | `npm run android -- --no-bundler` from short verification checkout `F:\cs155` | Passed; BUILD SUCCESSFUL in 8m21s, 326 tasks; x86_64 APK installed on API 36 / Android 16 emulator |
| Final-source Android export | `npm run smoke:android` with emulator profile / shared API convention | Passed; 1,243 modules, 2.7MB Hermes bundle |
| C# formatting | `dotnet format tests/CoffeeShop.Api.Tests --verify-no-changes --no-restore --include tests/CoffeeShop.Api.Tests/StaffIdentityHttpTests.cs` | Passed |
| OpenSpec | `openspec validate add-staff-mobile-app --strict` | Passed |

`tests/barista.test.tsx` covers loading/empty/error states, refresh, filters,
pagination, detail fields, confirmation, pending/double-tap protection,
server-confirmed progression, stale/cancelled conflict recovery, unauthorized
routes and API rejections, and disabled offline confirmation with no reconnect
replay. `StaffIdentityHttpTests.Preparation_RejectsUnauthorizedAndStaleTransitionsWithoutChangingNewerState`
adds real PostgreSQL checks for anonymous/forbidden access, cross-client stale
start, repeated ready, cancelled-ticket rejection, persisted state, actors,
timestamps and no duplicate history. It passed in the complete 37-test API suite.

Sanitized test results are in [api-results.json](api-results.json) and
[mobile-results.json](mobile-results.json). The debug
APK SHA256 is `21B4630BCB1C7730CEB50A3599E194763AD00FD0124B31CA6BBE56D2DB51B91D`.
Native build/install does not prove rendered business screens or end-to-end
progression. The native build predates the quality merge, which changed no native
dependencies; the final JavaScript export includes its connectivity fix.

An initial successful Jest run retained query-cache GC timers. The new test-only
cache helper disables GC timers and clears caches after observer teardown. A
`--detectOpenHandles` diagnostic under concurrent native-build load timed out two
router tests and emitted overlapping-act warnings. The final normal full command
passed all 135 tests and exited cleanly without changing assertions or timeouts.

The first .NET test attempt encountered an empty ignored runtime configuration
left by an interrupted build. The clean Release rebuild regenerated it; no source
or SDK workaround was needed.

### Follow-up verification — 7 November 2026

Merged main `51c85b5`, resolving the module route conflict by retaining both
Preparation and manager discount approvals. `npm run check` passes TypeScript,
zero-warning ESLint, formatting and **142/142 tests across 16 suites** (16.159s).
The forbidden-response regression now leaves subsequent reads stalled and proves
cached preparation details disappear without waiting for a refetch.

The fixture now uses the actual `initialVariants`/`sizeName` API request fields,
finds its ticket across every queue page, and supplies the page count to Maestro.
Its Ready verifier also checks every page. Against a fresh disposable loopback
API/database, seeded 16 tickets, found a fixture on page 2, advanced it through
start/ready using a synthetic barista API session, and verified Ready and absence
from the active queue using a separate manager session. This is **protocol-only**
fixture verification; no Android workflow acceptance is claimed.

PR #177 already had all five original hosted checks passing. JakeConal is now
explicitly requested as reviewer; request assignment is not reviewer approval.
Updated-head CI must be checked separately after the follow-up push.

## Android gate and reproduction

**Not executed:** real-API Android emulator Confirmed → Preparing → Ready, native
visual/accessibility checks, and simultaneous WinUI/mobile operation. Per user
direction, physical-device acceptance is not a gate for this PR. No screenshots
from another issue or JS-only tests are used as device evidence.

The earlier combined setup command was rejected with `blocked by policy`.
Following the user's request to fix that blocker, explicit foreground migration,
synthetic staff provisioning and loopback API startup all succeeded on 7 November.
Automatic approval review then rejected the standalone Metro `npm start` command,
again returning only `blocked by policy` with no specific reason. No bypass was
attempted. #155 stays Blocked pending emulator acceptance. PR #177 is currently
open and ready (it was made ready before this follow-up); that status does not
represent completed Android acceptance.

For an authorized verification session:

1. Follow the [environment guide](../../mobile-environment-139.md) and
   [app setup](../../../apps/staff-mobile/README.md). Use the short physical path
   required by native builds on Windows, JDK 17, API 36 emulator, the documented
   Android SDK and Maestro 2.12.1. Build with the **emulator** profile and
   `http://10.0.2.2:5232/api/v1`. Run Metro with `npm start`.
2. Start the shared API on `http://127.0.0.1:5232` with a disposable migrated
   PostgreSQL database. Use process-only JWT configuration and the documented
   `--provision-staff` command to create separate synthetic **Manager** and
   **Barista** accounts. Never use real staff or production data.
3. Set the manager-only test environment variables below in a local terminal;
   supply the account's password privately. Seed one confirmed takeaway order:

   ```powershell
   $env:COFFEESHOP_MOBILE_TEST_API_BASE_URL = 'http://127.0.0.1:5232/api/v1'
   $env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = '<disposable-manager>'
   # Set COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD privately in this terminal.
   $fixture = Join-Path $env:TEMP 'barista-155-fixture.json'
   ./scripts/barista-fixture.ps1 -Action Seed -FixturePath $fixture
   $env:MAESTRO_ORDER_NUMBER = (Get-Content $fixture -Raw | ConvertFrom-Json).orderNumber
   $env:MAESTRO_QUEUE_PAGE_SKIP = (Get-Content $fixture -Raw | ConvertFrom-Json).queuePageSkip
   $env:MAESTRO_STAFF_USERNAME = '<disposable-barista>'
   # Set MAESTRO_STAFF_PASSWORD privately in this terminal.
   npm run test:e2e:barista
   ./scripts/barista-fixture.ps1 -Action Verify -FixturePath $fixture
   ```

   Run these commands from `apps/staff-mobile`. Maestro logs can record input;
   use only disposable credentials and never commit raw debug artifacts.
   Keep the disposable queue stable between seeding and running Maestro so its
   recorded page count remains valid. Other active fixture tickets are supported.
4. The Maestro flow signs in, opens the exact fixture ticket, confirms both
   transitions and requires server-confirmed Ready. The separate manager-session
   verifier requires Ready and absence from the active preparation queue. Record
   sanitized JUnit and confirmed/preparing/ready screenshots from this run.
5. Run the API suite with `COFFEESHOP_TEST_POSTGRES` pointed at an isolated
   administrator connection. It creates/drops only its own uniquely named test
   databases:

   ```powershell
   dotnet test tests/CoffeeShop.Api.Tests --configuration Release
   ```

The fixture script has passed protocol-only verification against the real API;
the Maestro flow has not executed. Final-video
timestamps, enlarged-text phone/tablet checks and named-reviewer approval remain
pending. Submission statuses are not promoted to Implemented without that evidence.
