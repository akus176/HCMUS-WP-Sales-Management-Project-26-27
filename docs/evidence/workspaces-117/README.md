# Menu, Customers, and Reports verification (#117)

Status: functional verification passes and the user approved the four current native compositions on 2026-10-29. The targeted concept/spec revision is [recorded here](../../ui-concepts/workspaces-117-native-revision.md). PR merge still requires passing CI and the required fresh reviewer approval.

The HTML reference concepts are commit `0ec5463dcd850d00fbc0223a7e4528a09d12d879`, based on main `3bd95aa`. The user first approved the capability changes recorded in that commit on 2026-10-29: fixed decorative drink icons, a text-only editor, omission of seasonal flags/catalog audit and performance/customer lifetime totals, current-period reporting and order channels, and Refresh in place of Export. In a separate review, the user approved the current native compositions from implementation commit `9e619622f30366f3f258716a9e004a602879e46d`, including the enumerated layout/control differences, real fixture state, and editor window framing. The [comparison](concept-native-comparison.png) preserves the concrete reviewed result.

## Captures

| Workspace | Native 1440 × 900 | Native 600 × 900 | Revised concept |
| --- | --- | --- | --- |
| Menu | [Expanded](native-menu-1440.png) | [Compact](native-menu-600.png) | [Reference](../../ui-concepts/menu.png) |
| Customers | [Expanded](native-customers-1440.png) | [Compact](native-customers-600.png) | [Reference](../../ui-concepts/customers.png) |
| Reports | [Expanded](native-reports-1440.png) | [Compact](native-reports-600.png) | [Reference](../../ui-concepts/reports.png) |

[Native Menu editor](native-menu-editor.png) / [revised editor concept](../../ui-concepts/menu-picture-editor.png). The updated editor image is an actual window capture (1442 × 933), including operating-system borders. It shows the new 760-DIP cream dialog, name/category fields, description, Cancel, and Save drink. It was captured before the invalid-save smoke check and cancelled without saving. It is not a matched 1440 × 900 reference capture.

The six workspace images come from native `RenderTargetBitmap` captures, using live data from a disposable PostgreSQL 18 database migrated and populated with `scripts/postgres/midterm-seed.sql`. They are not browser mockups. The concept samples use different people, products, and reporting dates. This fixture difference was explicitly approved for the targeted revision; the captures do not claim to pixel-match the original HTML.

## Verified behavior

- The complete solution suite passes: Domain 413, Application 422, Infrastructure 258, WinUI presentation 108, API 1; 1,202 total, zero failures or skipped tests. Infrastructure includes real PostgreSQL integration and midterm demonstration flows. These totals include the Counter/Service board changes merged from main at `3306dc4`.
- Release WinUI build: zero warnings/errors. OpenSpec strict validation and formatting verification pass.
- Catalog regression tests cover clearing filtered-out details, clearing previous selection before the loading notification, save feedback, and existing lifecycle/recipe/compatibility workflows. The native empty-search check also verifies the explanatory message becomes visible after loading.
- Customer tests preserve profile/address/default-address/deactivation, loyalty history, delivery, next-order selection, walk-in, and failure/retry behavior. Successful commands and walk-in selection now expose explicit success feedback.
- Reports load revenue, best sellers, channels, and current low stock in independent scopes. A failing region leaves other results available, exceptions do not expose provider details, Refresh retries, and every previous-period value is cleared before the new period is published. Invalid date input retains the last loaded period.
- Native seeded totals for 01–29 October 2026: recognized revenue 2,260.10 across 19 distinct orders; channels 741.60 / 1,059.50 / 459.00; ten best-seller rows and two current low-stock ingredients. Application accounting handlers are unchanged.
- [workspace-verification.json](workspace-verification.json) records all three pages at widths 1440, 1008, 1007, 641, 640, 600, and 1440. All 21 checks preserve the page instance and compared selection/filter/customer-order fields. The report includes rendered button dimensions at each width. Primary actions are at least 48 DIPs; secondary text/icon actions use smaller targets.
- [editor-verification.json](editor-verification.json) records an unsaved drink-name draft surviving 600 → 1440 resizing while the saved record remains unchanged. Cancel is 60 × 48 and Save drink is 72 × 48 at both widths. The TextBox's internal Delete control is secondary.
- Native keyboard smoke: the drink editor cycles name → category → description → Cancel → Save drink → name, with visible focus and no escape into the page. Blank-name Save shows local validation, re-enables input, and keeps the dialog open; Cancel closes it without changing the saved drink. Customers tabs from Delivery order through recipient, contact, saved-address choice, order-only address, and Set for next order. The report filter flyout tabs from payment method to order channel. UI Automation's focused-element field was stale during these checks; visible native focus was inspected instead.
- Native inspection confirmed meaningful Menu item names, editor field labels, report bar summaries, and reachable Menu edit/deactivate actions by vertical scrolling. Size actions expose Edit size and price, Edit recipe, and Deactivate size. Compact Menu includes a category selector; Customers and Reports stack their existing regions in vertical scroll content. Dialogs use the light console palette; delivery fields remain readable inside the dark next-order panel. These are representative checks, not an exhaustive assistive-technology audit.

## Accepted visual revision

Direct comparison with the HTML reference showed the following differences, now explicitly accepted in the targeted revision:

- Native fields, checkboxes, category rows, functional icons, tab labels/counts, selected navigation/card rules, and header action spacing differ. Menu/Customers retain Refresh in the header; category creation remains available in the Menu rail and Categories view.
- Menu now has brass selected-card framing, compact size rows with action flyouts, a recipe summary, and compatible-topping actions. Its record metadata, size actions, recipe selection, and footer positioning still differ from the concept.
- Customers now has initials avatars, a loyalty balance strip, address action flyouts, and the dark next-order panel. Search/inactive filtering, ledger paging, real order rows, and the explicit profile/delivery form change its density and geometry.
- Reports now has the native donut/legend and best-seller progress tracks. Supported payment/channel filters are in a flyout and daily details have a flyout; chart labels, stock rows, panel spacing, and selected-period copy differ.
- The Menu editor now uses a 760-DIP custom dialog with name/category in two columns, full-width description, and Cancel/Save at opposite footer edges. The concept stacks the fields, places both footer buttons on the left, and includes additional header decoration/close affordance. Native Cancel and Escape are available.

The user answered “Approve these native compositions” to the question covering these differences, the real seeded fixture state, and native editor framing. The approval is recorded in the concept supplement, OpenSpec design, and specification. The four captures remain unchanged from `9e61962`; this documentation records acceptance rather than silently replacing the original references. All seven #117 implementation items and its acceptance items can now be evaluated against that approved revision and the behavioral evidence above. The parent change's all-six-workspace verification and unrelated implementation items remain outside this evidence's scope.

## Reproduce

Configure a disposable seeded PostgreSQL database through `ConnectionStrings__CoffeeShop`, then launch the Release executable with `--verify-shell --verify-workspaces`. The verification mode submits no business commands and writes captures/JSON under `%LOCALAPPDATA%\CoffeeShop`.

```powershell
dotnet test CoffeeShop.slnx
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -c Release
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
openspec validate migrate-winui-service-console --strict
```

For the PostgreSQL tests, set `COFFEESHOP_TEST_POSTGRES` to a disposable local PostgreSQL administrative connection; the test harness creates isolated databases.
