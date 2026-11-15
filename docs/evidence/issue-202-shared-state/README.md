# Issue #202: Windows/API verification and Android handoff

Issue: [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202).
Implementation: [draft PR #214](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/214).
Date: **15 November 2026**.

The Windows/API checks below passed. **Simultaneous native WinUI/Android acceptance has not run**: the user confirmed that this environment has no Android test app. Keep #202/#203 and the remaining shared-state parent tasks open. No reviewer approval or release acceptance is claimed.

## Environment and source

- Business/API source: `5683fd14a56ef6d5a816288d769a894e7833970b`, including merged #200/PR #213. This PR changes verification tooling, CI and documentation; existing business code and C# test assertions are unchanged.
- Windows: NT 10.0.26300.0, x64; .NET SDK 10.0.404; Release test binaries from a physical Windows export of that source.
- Local database: a dedicated disposable Docker `postgres:18-alpine` server, PostgreSQL **18.7**, with an independent database per integration test. Docker Engine 29.5.3. Existing development services were not reseeded or replaced.
- Docker collection: the repository's clean Compose stack in [CI run 43720801852](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/43720801852), tooling commit `f8f5b31`. The API runs in its normal Docker image and talks to its own PostgreSQL service; the collection addresses `http://api:8080` inside that isolated project.
- Compose applied **11 migrations** and ran the midterm seed twice. Both runs produced `5|20|40|40|9|3|2|12|8|24` for categories/products/sizes/recipes/ingredients/suppliers/receipts/customers/tables/orders.
- The collection provisioned generated synthetic Manager and Cashier accounts. Integration tests provisioned synthetic Admin, Manager, Cashier, Barista, InventoryStaff, DeliveryStaff and multi-role accounts. No credentials or tokens are published.

[Environment metadata](environment.json), [Docker log excerpt](docker-collection-log.txt).
The local cold-image download was interrupted after CI supplied the completed clean-Docker result; it is not counted as a local Compose pass.

## Observed results

| Check | Exact result | Evidence |
| --- | --- | --- |
| Windows repeated concurrency | **5 complete rounds × 23 tests = 115 passed**, zero failures/skips | [Per-round names and counts](windows-concurrency.json) |
| Complete Windows API suite against real PostgreSQL | **65 passed**, zero failures/skips | [Test names and totals](windows-api-suite.json) |
| Unchanged Postman collection against Docker API | **9 requests, 9 assertions, zero failures** | [Sanitized results](collection.json) |
| Invalid-credential negative control | All 9 requests executed; 3 assertions failed as expected; runner returned failure and the enclosing verification accepted that expected rejection | [Negative control](negative-control.json) |
| Runner regressions | **5 passed**, zero failures/skips | [CI log excerpt](docker-collection-log.txt) |
| Locked npm dependencies | `npm audit` reported zero findings | Pinned `tests/api/package-lock.json` |
| OpenSpec | Both `build-coffee-shop-management-system` and `add-staff-mobile-app` passed strict validation | Commands below |

The 20 Infrastructure and 3 API concurrency cases include all five mapped risk areas: order-confirmation retries/stock effects; competing table occupancy; cancellation/restoration retries; append-only loyalty reversal; and refresh-token reuse/family revocation. Additional catalog, discount, stock-adjustment and payment races also run. The selection explicitly includes parallel restoration and catalog lock/wait cases whose names do not contain `Concurrent`. The runner rejects skipped/empty suites and requires the original confirmation, table, cancellation/restoration, loyalty and refresh cases plus those lock/wait cases in every round.

The 65-case API run includes the six-role permission matrix, account/role changes, credential-safe administration responses, ownership/privacy checks, authenticated inventory and receipt operations, payment retry, cancellation, delivery and actor persistence. These are real ASP.NET integration pipelines backed by isolated PostgreSQL databases. They are not Android UI execution or native WinUI HTTP tracing.

Earlier overlapping Windows/Linux attempts were **not** complete passes. One attempt timed out during migrations; its cleanup removed the server shared with the other attempt. Those partial results are excluded. The final Windows five-round run used its own PostgreSQL 18 server and completed sequentially, with the original test assertions and timeouts unchanged.

The initial tooling commit read the older Newman result shape. The real pinned Postman CLI emits `run.summary`, `requestExecuted` and `tests`; commit `f8f5b31` corrects that mapping. Regression coverage now verifies a genuine report-shaped success, failure/skip rejection, URL derivation, secret-free evidence, and replacement of an old passing result before a failed run. The first failed CI attempt is not claimed as passing evidence.

## Docker requests

| Request | Observed status |
| --- | --- |
| GET `/health`, GET `/health/ready` | 200, 200 |
| Anonymous GET `/api/v1/categories` | 401 |
| Manager/Cashier POST `/api/v1/auth/login` | 200, 200 |
| Manager GET `/api/v1/categories` | 200 |
| Cashier POST `/api/v1/categories` | 403 |
| Manager blank-category POST `/api/v1/categories` | 400 |
| GET unknown API route | 404 |

The collection does not successfully write business data. Fixture preparation, authentication audits and isolated integration-test business writes are separate. Results retain only names, methods, paths, statuses, assertion names/outcomes and counts. Headers, bodies, query strings, session tokens, passwords, raw error messages and raw CLI reports are excluded. CLI event reporting is disabled. Temporary private CLI files and CI Compose services/volumes are removed by their owners.

## Commands and reproduction

The [collection runbook](../../../tests/api/README.md) documents a complete clean-Docker run and private environment-variable names. No request payload editing is needed:

```sh
COFFEESHOP_API_COLLECTION_RESULTS=/tmp/coffee-shop-api-results \
  bash scripts/verify-compose.sh
```

Against an already running disposable API, configure the Manager/Cashier variables privately and run:

```sh
npm ci --prefix tests/api
npm test --prefix tests/api
npm run verify --prefix tests/api -- /tmp/collection.json
```

For repeated concurrency, privately set `COFFEESHOP_TEST_POSTGRES` to a disposable administrative test connection:

```sh
COFFEESHOP_CONCURRENCY_ITERATIONS=5 \
  COFFEESHOP_CONCURRENCY_RESULTS=/tmp/coffee-shop-concurrency \
  bash scripts/verify-concurrency.sh
```

The recorded Windows run executed this equivalent selection five times, sequentially, using the physical checkout and its own Docker PostgreSQL server. It parsed every TRX, rejected missing/failed/skipped cases, and ran the same required-case validator used by `verify-concurrency.sh`:

```powershell
$filter = 'FullyQualifiedName~Concurrent|FullyQualifiedName~Cancel_DifferentOrdersSharingIngredients|FullyQualifiedName~CancelledReplacement_StopsWhileWaitingForParentLock|FullyQualifiedName~RecipeReplacement_WaitsForIngredientUpdateAndRejectsStaleValidation'
foreach ($round in 1..5) {
    foreach ($suite in @('Infrastructure', 'Api')) {
        dotnet test "tests/CoffeeShop.$suite.Tests/CoffeeShop.$suite.Tests.csproj" --configuration Release --filter $filter --logger "trx;LogFileName=$suite.trx" --results-directory "TestResults/concurrency/$round"
        if ($LASTEXITCODE -ne 0) { throw 'Concurrency verification failed.' }
    }
}
dotnet test tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj --configuration Release --no-build --no-restore --logger 'trx;LogFileName=api-all.trx'
```

The recorded runs reused the built binaries with `--no-build --no-restore`; the commands above also support an unbuilt checkout. Run them from the physical Windows checkout. The Bash runner manages round numbers and complete-result validation automatically. CI repeats three complete rounds on Linux and preserves their TRX and summary artifacts. The full 65-case API suite was recorded separately on the same unchanged business/test source.

```text
openspec validate build-coffee-shop-management-system --strict
openspec validate add-staff-mobile-app --strict
git diff --check
```

OpenSpec validation used the physical Windows export because the Windows CLI's UNC glob discovery misses existing spec files. Canonical checklist progress remains on GitHub #26/#95; legacy `tasks.md` was not changed.

## Reconciliation of #180 and S4 prerequisites

- **#180 is closed but its evidence explicitly left simultaneous Windows/Android verification pending.** Its Release dependency check is valid evidence for the API-only build boundary, not proof of cross-client state.
- **#200 / merged PR #213** adds approved signed-in native Windows inventory/delivery screenshots, actual HTTP requests, process connection samples and authoritative database counts. [S4 Windows record](../issue-200-winui-api.md).
- **#198 / PR #209** supplies the focused physical Android inventory/receipt run, 17/17. [Record](../mobile-inventory-198/README.md).
- **#201 / PR #210** supplies the focused physical Android barista/discount/courier run, 19/19 cases and 42/42 native steps. [Record](../mobile-workflows-201/README.md).
- These runs used **different environments and fixture IDs**. They do not satisfy Mobile 6.4 together. The remaining same-database gate is explicitly tracked by **#202/#203**.

No parent OpenSpec checkbox is changed by this evidence. The full mapped Windows 9.8/10.5/11.7/13.3 and Mobile 6.4 requirements retain their canonical issue states until the applicable full verification/review gate passes.

## Android/shared-state handoff: still required

The user initially preferred reusing an Android environment, then confirmed there is no Android test app in this environment. No existing development database was assumed to be a disposable Android seed. Once the #203 owner has a running app, agree one reachable API and one disposable PostgreSQL database before either side records acceptance.

| Remaining case | Required evidence from the coordinated run |
| --- | --- |
| Identity of both clients | WinUI Release commit/hash, Android APK and JavaScript commit/build, API commit/address, database/seed identity, and distinct synthetic account IDs/roles |
| Windows → Android | Native WinUI adjustment/order change; Android receives an authenticated event or deliberately refreshes; both show the same server record and audit actor |
| Android → Windows | A native Android mutation; native WinUI refresh shows that same persisted record and actor |
| Conflict and permissions | A competing change wins once; the stale client gets a real rejection and reloads without replacing newer state; forbidden responses expose no protected data |
| Retry and audit | Repeated applicable writes retain one stock/payment/delivery/cancellation effect; persisted actors match the authenticated account on each client |
| Network boundary | Both clients use the agreed API; current native WinUI process tracing still shows no direct PostgreSQL traffic |

Use the existing collection/concurrency runners for the API portion, then add a run manifest containing the **same** record IDs, API/database identity, both native captures, sanitized requests and final server/audit snapshots. Keep Android UI execution in #203. Do not combine the independent S4 fixtures into a claimed simultaneous run.

The known #203 inventory event/query-key mismatch is still visible in this source: `operationalEvents.ts` maps inventory events to `inventory`, while `InventoryWorkspace.tsx` uses `inventory-balances`, `inventory-movements`, `purchase-receipts` and `purchase-receipt`. The predicate matches the first key exactly. This is already tracked in [#203's handoff](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203#issuecomment-6874402246); its fix and genuine Android event/reconnect/offline proof remain with that issue.

The draft PR can supply the repeatable Windows/API tooling and independent results. **#202 is not complete** until matching Android/shared-database evidence is linked and **@JakeConal approves**.
