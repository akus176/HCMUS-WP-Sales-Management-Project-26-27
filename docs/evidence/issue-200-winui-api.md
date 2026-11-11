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

Current local result: .NET SDK 10.0.401 on Debian 13; **139 passed, 0 failed, 0 skipped**. The test suite does not run the WinUI window.

## Runtime evidence still required

The available runner is Debian 13. A Release build was attempted with `dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj --configuration Release --no-restore -p:EnableWindowsTargeting=true`; it stops when the WinUI XAML target invokes Windows-only `XamlCompiler.exe` and receives `Exec format error`. No Windows build or native WinUI launch, shared API/PostgreSQL seed, synthetic account, UI screenshot, or network trace was available in this run. Release project inspection confirms `COFFEESHOP_API_ONLY` is defined and the Release project-reference graph contains only Application and Contracts; it excludes the PostgreSQL project. Therefore this PR does not claim the issue's end-to-end acceptance is complete.

On a clean Windows host, use the repository [Windows quickstart](../../README.md#start-the-api-and-winui-on-windows) with a disposable Compose database and synthetic accounts. Record the Windows commit/build, API URL and health response, seed identity, and account permission bundles without including passwords, tokens, private addresses, or customer data. Then:

1. Sign in with inventory permissions. Search for an ingredient; verify an active low-stock item is shown at or below its reorder threshold.
2. Create a receipt, add a line, post it once, then reload balances and movement history. Confirm the balance changed once and the receipt movement is present.
3. Record a stock adjustment with a reason. Reload and confirm the server balance and one actor-attributed movement. Check validation and conflict feedback with safe synthetic input.
4. Sign in with `delivery.assign` and `delivery.update` accounts as applicable. Assign an eligible delivery, then exercise start and a valid completion or failure. Confirm an unauthorized action and a stale/conflicting transition are rejected by the API and the board reload reflects server state.
5. Capture sanitized UI and request evidence showing business requests go to `/api/v1` over HTTP and no WinUI-to-PostgreSQL connection occurs. Preserve request method/path/status only; redact authorization headers, account details, and contact/address data.

Keep OpenSpec tasks 11.3, 11.4, and 11.7, and issue #200, unchecked until the corresponding full task scopes and Windows runtime evidence are reviewed. Android and cross-client S5 behavior remain out of scope for this issue.
