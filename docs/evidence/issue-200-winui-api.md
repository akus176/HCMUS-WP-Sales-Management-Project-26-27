# Issue #200 WinUI inventory and delivery API verification

Issue: [#200](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/200)
OpenSpec: [`build-coffee-shop-management-system` tasks 11.3, 11.4, and 11.7](../../openspec/changes/build-coffee-shop-management-system/tasks.md)

## Automated client evidence

Run on the PR commit:

```sh
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --configuration Release --no-restore --verbosity minimal
```

The issue-specific checks use synthetic API responses and an in-memory HTTP handler. They do not contact a shared API or a real database.

| Check | Test evidence |
| --- | --- |
| Inventory balance and low-stock threshold data, active-only query, receipt creation/line/post, adjustment reason, movement history, bearer token, authoritative reread, and one receipt-post request | `ApiManagementSourceTests.InventorySource_PostsReceiptAndAdjustmentOnceThenReadsAuthoritativeBalanceAndHistory` |
| Delivery board and delivery-staff reads, assignment request and reread, bearer token | `ApiOperationsSourceTests.Manager_LoadsDeliveryBoardAndAssignsDeliveryThroughAuthenticatedApi` |
| Courier assignment/detail reads and start, complete, and failure transition routes | `ApiOperationsSourceTests.CourierLoadsAssignmentsAndSendsEligibleTransitionsOnce` |
| Forbidden and conflicting transition responses | `ApiOperationsSourceTests.DeliveryTransitionPreservesUnauthorizedAndConflictResponses` |

Current automated result on the Windows host: .NET SDK 10.0.401; **140 passed, 0 failed, 0 skipped**. The additional test checks that the login window uses the light console theme and shared console background brush. The suite does not run the WinUI window.

## Windows build and launch evidence

The focused Release verification script `scripts/verify-winui-api-release.ps1` completed with 0 warnings and 0 errors. Its Release dependency scan confirms the client references Application and Contracts and excludes PostgreSQL/EF persistence. The Windows client launched against the local API at `http://127.0.0.1:54906/api/v1/`. The sign-in window initially rendered poorly under the host's dark system theme; the root window now explicitly requests the light theme and uses `ConsoleGroundBrush`, with a focused regression test. A blank sign-in screenshot is saved at [`issue-200-winui-runtime/signin.jpg`](issue-200-winui-runtime/signin.jpg).

The isolated `pr208verify` Compose database was confirmed empty of users, orders, and deliveries before running the repository midterm seed. Synthetic `issue200_manager` and `issue200_courier` accounts were provisioned with the manager inventory/assignment permissions and courier delivery-update permission. Passwords and tokens are intentionally excluded from this note. The API readiness endpoint returned HTTP 200.

## Live API workflow evidence

On 2026-11-13, direct HTTP requests from the Windows host exercised the seeded API at `http://127.0.0.1:54906/api/v1/` against the isolated `pr208verify` database. Manager and courier login both returned HTTP 200. Inventory and low-stock reads returned 200; Arabica beans started at 500, exactly its reorder threshold. A receipt with one line was posted, the receipt movement showed a +10 quantity delta attributed to the manager, and the balance reread increased to 510. Reposting the receipt returned 409. A separate +1 Black tea adjustment was recorded with its reason and manager actor attribution; its balance reread was 701. A synthetic Americano delivery order was created, confirmed, paid, moved through preparing and ready, assigned to the synthetic courier, started, and completed. Those successful writes returned 201/200/204 as appropriate; repeating completion returned 409. Manager access to the courier queue and courier access to inventory both returned 403. The completed test order consumed 18 Arabica beans, leaving 492; the movement history recorded the order-consumption delta, and Arabica returned to the low-stock list.

The updated PR head's [GitHub Actions run #43269185272](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/43269185272) completed successfully across all CI jobs.

This is live API/database evidence from HTTP requests outside the WinUI process. It does not prove that the WinUI screens made these requests. The actual signed-in WinUI inventory and delivery flows, screenshots of those workflows, a sanitized request trace from the WinUI process, and a runtime connection check showing no WinUI-to-PostgreSQL connection remain unverified.

## Live acceptance evidence still required

The earlier Debian 13 runner could not compile the WinUI XAML target because it invokes Windows-only `XamlCompiler.exe` and receives `Exec format error`. Windows build, native launch, isolated API/PostgreSQL seed, synthetic accounts, blank sign-in screenshot, direct live API workflow evidence, and passing CI are now available. The actual signed-in WinUI interactions, API request trace from the WinUI process, and runtime check for no direct PostgreSQL connection remain pending; this PR does not claim the issue's end-to-end acceptance is complete.

The PR is ready for human code review; keep the issue and OpenSpec task checkboxes open until the remaining native WinUI acceptance evidence is captured and reviewed. Continue in the Windows client against the isolated Compose database and synthetic accounts. Record the Windows build, API URL and health response, seed identity, and account permission bundles without including passwords, tokens, private addresses, or customer data. Then:

1. Sign in with inventory permissions. Search for an ingredient; verify an active low-stock item is shown at or below its reorder threshold.
2. Create a receipt, add a line, post it once, then reload balances and movement history. Confirm the balance changed once and the receipt movement is present.
3. Record a stock adjustment with a reason. Reload and confirm the server balance and one actor-attributed movement. Check validation and conflict feedback with safe synthetic input.
4. Sign in with `delivery.assign` and `delivery.update` accounts as applicable. Assign an eligible delivery, then exercise start and a valid completion or failure. Confirm an unauthorized action and a stale/conflicting transition are rejected by the API and the board reload reflects server state.
5. Capture sanitized UI and request evidence showing business requests go to `/api/v1` over HTTP and no WinUI-to-PostgreSQL connection occurs. Preserve request method/path/status only; redact authorization headers, account details, and contact/address data.

Keep OpenSpec tasks 11.3, 11.4, and 11.7, and issue #200, unchecked until the corresponding full task scopes and Windows runtime evidence are reviewed. Android and cross-client S5 behavior remain out of scope for this issue.
