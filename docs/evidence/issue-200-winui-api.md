# Issue #200: signed-in WinUI inventory and delivery evidence

Issue: [#200](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/200).
Parent OpenSpec checklist: [#26](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/26), tasks 11.3, 11.4 and 11.7.

The native Windows run on **2026-11-15** passed the inventory and delivery checks below. Screenshots came from the signed-in Release WinUI process; the request log contains its actual HTTP traffic. Separate API and database reads verified the resulting state and audit counts. **@kyrie25 approval remains required before closing #200.**

## Build and disposable environment

| Item | Recorded value |
| --- | --- |
| Windows / SDK | NT 10.0.26300.0 x64 / .NET 10.0.404 |
| Source | `74cad992b7fc99a71d79e793ee0bfcef0f322e00`; base `fba422ea3ad45e6d7ee62342a5082dc6bd61ff97` plus the required-reason fix |
| Native build | Release, `net10.0-windows10.0.19041.0/win-x64`, 0 warnings/errors |
| API | Same source base; `http://127.0.0.1:52302/api/v1`; readiness HTTP 200 |
| WinUI API address | `http://127.0.0.1:52300/api/v1/`, through the loopback tracing proxy |
| PostgreSQL | Fresh isolated PostgreSQL 16.15 cluster, database `issue200`, loopback port 55400 |
| Seed | Migrations, `scripts/postgres/midterm-seed.sql`, then synthetic `I200` fixtures |
| Accounts | `issue200_manager`: Manager; `issue200_courier`: DeliveryStaff; `issue200_cashier`: Cashier |

The physical Windows scratch checkout was exported from the base commit and received the four changed source/test files before building. Their SHA-256 hashes and the compiled client hash are in [environment.json](issue-200-winui-runtime/environment.json); they matched the committed source. A human entered the generated synthetic credentials in WinUI. No passwords or tokens are included. [Fixture IDs](issue-200-winui-runtime/fixture.json) and [readiness](issue-200-winui-runtime/api-health.json) identify this run.

## Native inventory results

| WinUI action | Observed result | Evidence |
| --- | --- | --- |
| Search `I200 Verification`; enable low-stock filter | One matching ingredient at 100 g, threshold 100 g, marked LOW | [Low stock](issue-200-winui-runtime/inventory-low-stock.jpg) |
| Enter -5 with blank reason before the fix | Save was enabled; code would substitute `Manual stock adjustment`. The blank adjustment was **not submitted** | [Defect](issue-200-winui-runtime/before-blank-reason-enabled.jpg) |
| Repeat on fixed build | Required reason labelled; Save disabled until explicit reason entered | [Guard](issue-200-winui-runtime/required-reason-blocks-submit.jpg) |
| Submit -5, reason `I200 native count correction` | One POST returned 200; authoritative reread showed 95 g | [Adjustment](issue-200-winui-runtime/adjustment-server-balance.jpg) |
| Post draft `I200-RECEIPT-001`, +10 g | One native POST returned 204; receipt became Posted and Post action disappeared | [Posted receipt](issue-200-winui-runtime/receipt-posted-once.jpg) |
| Read inventory/history again | 105 g, stock OK; receipt +10 and adjustment -5 each once, attributed to Issue200 Manager | [105 g](issue-200-winui-runtime/inventory-authoritative-105.jpg), [history](issue-200-winui-runtime/native-stock-movement-history.jpg) |

The draft receipt and +100 opening movement were prepared through backend fixture HTTP. Posting and the -5 adjustment were native actions. An independent repeat-post request returned **409**, leaving one receipt movement. [Inventory assertions](issue-200-winui-runtime/inventory-server-check.json) and [final API reads](issue-200-winui-runtime/api-final-check.json) confirm 105 g and both native movements with the Manager actor. Fixture requests bypassed the native tracing port.

The focused fix removes the fallback reason, validates blank/whitespace/overlength reasons before submission, labels the field required, and limits input to 255 characters. Five regression cases cover empty, whitespace, explicit reason, 255 and 256 characters. **151 WinUI tests passed, 0 failed, 0 skipped**; [summary](issue-200-winui-runtime/test-summary.json). Earlier API adapter coverage merged through [PR #208](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/208) and [PR #211](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/211).

## Native delivery results

Three ready orders were prepared through backend HTTP. The Manager assigned `complete` and `fail` in WinUI. The `conflict` order was assigned through fixture HTTP. Manager UI displayed Assigned and hid courier transition controls: [first assignment](issue-200-winui-runtime/manager-delivery-assigned.jpg), [second assignment](issue-200-winui-runtime/manager-second-delivery-assigned.jpg). The courier saw only its three active assignments and no management navigation.

| Native courier action | HTTP / visible result | Evidence |
| --- | --- | --- |
| Start fail after its synthetic DeliveryStaff role was temporarily removed in the disposable database | 403; Authorization feedback; remained Assigned with no added history. Original role restored before continuing | [Forbidden](issue-200-winui-runtime/courier-forbidden.jpg), [UI state](issue-200-winui-runtime/courier-forbidden-ax.txt) |
| Start conflict from stale Assigned form after independent backend HTTP already started it | 409; Reload required; stale Start disabled. Refresh loaded Out For Delivery and finish actions | [Conflict](issue-200-winui-runtime/courier-conflict.jpg), [disabled action](issue-200-winui-runtime/courier-conflict-ax.txt), [reload](issue-200-winui-runtime/courier-conflict-reloaded-ax.txt) |
| Start fail, then fail with `I200 recipient unavailable` | 204 then 204; success; active queue 3 → 2 | [Reason](issue-200-winui-runtime/courier-failure-reason.jpg), [failed](issue-200-winui-runtime/courier-failed.jpg) |
| Start complete, then mark delivered | 204 then 204; authoritative Out For Delivery; success; queue 2 → 1 | [Started](issue-200-winui-runtime/courier-out-for-delivery.jpg), [delivered](issue-200-winui-runtime/courier-delivered.jpg) |

Independent final HTTP reads confirmed complete=delivered, fail=failed, conflict=out_for_delivery, and only conflict active. Manager access to the courier queue and Courier access to inventory returned **403**. Repeating completion returned **409**. These independent probes used backend port 52302; [results](issue-200-winui-runtime/api-final-check.json).

The [database audit snapshot](issue-200-winui-runtime/server-state.json) has exactly **3 / 3 / 2 history rows** for complete / fail / conflict: one assignment and one start per order, plus one terminal transition for the first two. Assignments carry the Manager actor; starts and terminal transitions carry the Courier actor. Rejected native 403/409 requests added no transition. The conflict fixture's successful start was the deliberate independent HTTP action.

## HTTP-only evidence

[winui-requests.jsonl](issue-200-winui-runtime/winui-requests.jsonl) retains only UTC time, method, path and status. It excludes headers, bodies, query strings, passwords and tokens. Only WinUI used the forwarding port. Every observed business route is under `/api/v1`.

Exactly ten native business POSTs were observed: adjustment 200; receipt post 204; two assignments 204; forbidden start 403; stale start 409; fail start/fail 204/204; complete start/complete 204/204. Successful writes are followed by authoritative GETs. No duplicate successful business POST was observed.

[Process samples](issue-200-winui-runtime/winui-network.jsonl), with [summary](issue-200-winui-runtime/network-summary.json), contain **1,182 samples** of the rebuilt native process (PID 2392), 12:17:09–12:39:23 UTC. All 244 observed connection rows target **127.0.0.1:52300**; none target PostgreSQL port 55400. This is process-owned connection polling, not uninterrupted packet capture. The complementary Release dependency/assembly scan passed: no PostgreSQL infrastructure, Npgsql, EF persistence or PostgreSQL connection settings in client output.

## Reproduction

Use a physical Windows checkout and a fresh disposable database. All role fault injection below is limited to its synthetic courier.

1. Initialize a separate PostgreSQL 16 cluster and empty `issue200` database at loopback:55400. Privately set `ConnectionStrings__CoffeeShop`, `Jwt__Issuer`, `Jwt__Audience`, and a generated `Jwt__SigningKey` for the API. The observed issuer/audience was `CoffeeShop.Issue200`.

   ```powershell
   dotnet build src/CoffeeShop.Api/CoffeeShop.Api.csproj --configuration Release
   dotnet src/CoffeeShop.Api/bin/Release/net10.0/CoffeeShop.Api.dll --migrate
   psql -h 127.0.0.1 -p 55400 -U issue200_owner -d issue200 -v ON_ERROR_STOP=1 -f scripts/postgres/midterm-seed.sql
   ```

2. Provision each synthetic account with the built API's `--provision-staff` option. Privately supply `ProvisionStaff__Username`, `Password`, `Email` (use `example.test`), `FullName`, and `Roles` from the table above. Start the API from its output directory with `ASPNETCORE_URLS=http://127.0.0.1:52302`; confirm `/health/ready` is 200.

3. Privately set `ISSUE200_MANAGER_PASSWORD` and run the [fixture script](issue-200-winui-runtime/prepare-fixture.ps1). It creates the opening movement, draft receipt and ready orders; save the generated IDs. In another terminal start the [tracing proxy](issue-200-winui-runtime/trace-proxy.mjs).

   ```powershell
   ./docs/evidence/issue-200-winui-runtime/prepare-fixture.ps1 -OutputPath fixture.json
   node ./docs/evidence/issue-200-winui-runtime/trace-proxy.mjs winui-requests.jsonl
   ```

4. Build/test below. Launch the Release executable with `COFFEESHOP_API_BASE_URL=http://127.0.0.1:52300/api/v1/`. Start [sample-network.ps1](issue-200-winui-runtime/sample-network.ps1) with its exact `-ClientPath`, a JSONL `-OutputPath`, and a `-StopFile` path. Create that stop file to finish sampling.

   ```powershell
   ./scripts/verify-winui-api-release.ps1
   dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --configuration Release --logger 'trx;LogFileName=winui-200.trx'
   dotnet format whitespace tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --verify-no-changes --no-restore --include tests/CoffeeShop.WinUI.Tests/StockRoomInteractionTests.cs src/CoffeeShop.WinUI/ViewModels/StockRoomInteraction.cs
   openspec validate build-coffee-shop-management-system --strict
   ```

5. Sign in as Manager. Search/filter the ingredient; verify blank-reason Save disabled; submit -5 with the explicit reason. Post the prepared receipt once, then reread inventory/history. Assign complete/fail through Service board. Assign conflict through independent backend HTTP: `POST /orders/{id}/assign-delivery`, body `{ "staffId": "<courierId>" }`.

6. Switch to Courier. Open fail's Assigned form, temporarily remove only that user's DeliveryStaff link from `identity.user_roles`, and click native Start. Confirm 403/no change, then restore the original link and refresh. Open conflict's Assigned form; independently send `POST /courier/assignments/{id}/start` as Courier to backend 52302, then click stale native Start. Confirm 409/disabled action, refresh, and check Out For Delivery. Start/fail fail with its reason, then start/complete complete through WinUI.

7. Independently GET the ingredient, movements, Manager orders and Courier queue through backend 52302. Repeat receipt posting and completion (409); check the cross-role denials (403). Query `inventory.stock_movements`, `sales.deliveries` and `sales.delivery_status_history` for fixture IDs and assert quantities, states, counts and actors above. Capture synthetic UI and sanitized request/connection fields.

The observed run passed the listed build/tests, targeted formatting, OpenSpec strict validation and `git diff --check`. Runtime services and plaintext disposable login/configuration files are cleaned up after collection.

## Scope and review gate

Android coordination: [#198 / PR #209](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/209), [inventory record](mobile-inventory-198/README.md); [#201 / PR #210](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/210), [workflow record](mobile-workflows-201/README.md). These are separate Android runs with different fixture IDs. This Windows run does not assert simultaneous state convergence or Android execution; shared-state acceptance remains in #202/#203.

Keep parent tasks 11.3, 11.4 and 11.7 unchecked until their full scope and review pass. Keep #200 open until **@kyrie25 records APPROVED**. Historical sign-in-only/direct-HTTP observations in PR #208/#211 are superseded for native acceptance by this signed-in evidence.
