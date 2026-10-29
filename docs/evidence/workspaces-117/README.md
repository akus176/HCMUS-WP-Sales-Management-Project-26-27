# Menu, Customers, and Reports verification (#117)

Status: functional implementation available for review; exact visual acceptance remains open. Do not close #117 or check off the affected #111 implementation items from this evidence alone.

The reference concepts are commit `0ec5463dcd850d00fbc0223a7e4528a09d12d879`, based on main `3bd95aa`. The user approved the capability changes recorded in that commit on 2026-10-29: fixed decorative drink icons, a text-only editor, omission of seasonal flags/catalog audit and performance/customer lifetime totals, current-period reporting and order channels, and Refresh in place of Export. Other visual differences were not approved.

## Captures

| Workspace | Native 1440 × 900 | Native 600 × 900 | Revised concept |
| --- | --- | --- | --- |
| Menu | [Expanded](native-menu-1440.png) | [Compact](native-menu-600.png) | [Reference](../../ui-concepts/menu.png) |
| Customers | [Expanded](native-customers-1440.png) | [Compact](native-customers-600.png) | [Reference](../../ui-concepts/customers.png) |
| Reports | [Expanded](native-reports-1440.png) | [Compact](native-reports-600.png) | [Reference](../../ui-concepts/reports.png) |

[Native Menu editor](native-menu-editor.png) / [revised editor concept](../../ui-concepts/menu-picture-editor.png). The editor image is an actual window capture (1442 × 933), including operating-system borders, taken after opening Edit drink and cancelled without saving. It is not a matched 1440 × 900 reference capture.

The six workspace images come from native `RenderTargetBitmap` captures, using live data from a disposable PostgreSQL 18 database migrated and populated with `scripts/postgres/midterm-seed.sql`. They are not browser mockups. The concept samples use different people, products, and reporting dates; these captures therefore do not establish a matched-state visual pass.

## Verified behavior

- The complete solution suite passes: Domain 413, Application 422, Infrastructure 257, WinUI presentation 104, API 1; 1,197 total, zero failures or skipped tests. Infrastructure includes real PostgreSQL integration and midterm demonstration flows.
- Release WinUI build: zero warnings/errors. OpenSpec strict validation and formatting verification pass.
- Catalog regression tests cover clearing filtered-out details, clearing previous selection before the loading notification, save feedback, and existing lifecycle/recipe/compatibility workflows. The native empty-search check also verifies the explanatory message becomes visible after loading.
- Customer tests preserve profile/address/default-address/deactivation, loyalty history, delivery, next-order selection, walk-in, and failure/retry behavior. Successful commands and walk-in selection now expose explicit success feedback.
- Reports load revenue, best sellers, channels, and current low stock in independent scopes. A failing region leaves other results available, exceptions do not expose provider details, Refresh retries, and every previous-period value is cleared before the new period is published. Invalid date input retains the last loaded period.
- Native seeded totals for 01–29 October 2026: recognized revenue 2,260.10 across 19 distinct orders; channels 741.60 / 1,059.50 / 459.00; ten best-seller rows and two current low-stock ingredients. Application accounting handlers are unchanged.
- [workspace-verification.json](workspace-verification.json) records all three pages at widths 1440, 1008, 1007, 641, 640, 600, and 1440. All 21 checks preserve the page instance and compared selection/filter/customer-order fields. This does not establish unsaved modal-input preservation or a complete keyboard audit.
- Native inspection confirmed meaningful Menu item names, editor field labels, report bar summaries, and reachable Menu edit/deactivate actions by vertical scrolling. Dialogs use the light console palette so dark Windows settings do not hide field values. Shared primary button styles enforce 48-DIP minimum targets.

## Remaining acceptance work

Direct comparison with the revised concepts still shows these differences:

- Header spacing, control typography, tab selection, search/filter treatment, and selected-card framing differ.
- Menu detail geometry and size/recipe actions differ from the compact concept rows; its detail illustration and recipe summary treatment remain different.
- Customers directory/profile/avatar composition, address/ledger density, and next-order form geometry differ from the artboard.
- Reports lacks the concept donut/legend and best-seller progress-track treatment; feedback, filters, daily-detail expander, chart spacing, and panel density differ.
- The Menu editor uses native dialog dimensions and button/footer arrangement rather than the reference composition.

Resolve these visible differences, capture equivalent data/state at 1440 × 900 (including the editor), and finish keyboard focus order, actual touch-target, compact action reachability, and unsaved-input checks before marking the corresponding OpenSpec tasks complete. Behavioral tests and the targeted capability approval do not waive this gate.

## Reproduce

Configure a disposable seeded PostgreSQL database through `ConnectionStrings__CoffeeShop`, then launch the Release executable with `--verify-shell --verify-workspaces`. The verification mode submits no business commands and writes captures/JSON under `%LOCALAPPDATA%\CoffeeShop`.

```powershell
dotnet test CoffeeShop.slnx
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -c Release
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
openspec validate migrate-winui-service-console --strict
```

For the PostgreSQL tests, set `COFFEESHOP_TEST_POSTGRES` to a disposable local PostgreSQL administrative connection; the test harness creates isolated databases.
