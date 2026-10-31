# Six-workspace integration verification (#119)

Status: automated behavioral checks pass; exact visual acceptance and final legacy removal remain open. No concept/spec exception is approved by this evidence.

Review [all seven compositions side by side or as overlays](comparison.html). The deck can also compare Menu, Customers, Reports, and the editor with their specifically approved #117 native references. The [manifest](manifest.json) records the base implementation commit, dirty source hashes, executable hash, concept commit, and both fixture hashes. The [native report](parity-verification.json) records actual fixture state, selection, dimensions, names, targets, focus, resize checks, and smoke results.

## Ticket status

| Item | Result |
| --- | --- |
| 8.1 Legacy cleanup | Gated by replacement visual acceptance. The five old inventory pages, Sketch dictionaries, and route compatibility remain available. |
| 8.2 Supported affordances | Audit passes; no unsupported global search, notification, persisted-picture editor, export, or shift-management action is exposed. |
| 8.3 Automated suite | 1,202 passed, zero failures/skips, including 258 infrastructure tests with PostgreSQL 18. |
| 8.4 Integrated smoke and visual review | All 42 layout rows and 12 read-only native smoke runs pass. Exact matched-state visual parity, complete transactional native smoke, and full keyboard review remain open. |

## Verification matrix

Each workspace has 1440 x 900 and 600 x 900 content captures. Every workspace also runs at 1008, 1007, 641, and 640 DIPs and returns to 1440; subpixel rounding is allowed within one DIP for layout measurements. Captured images retain the requested logical dimensions. Primary targets are measured in DIPs, separately from image pixels.

| Composition | Expanded | Compact | Reference / current state |
| --- | --- | --- | --- |
| Counter | [1440](native-counter-1440.png) | [600](native-counter-600.png) | [Concept](../../ui-concepts/counter.png); real menu, local unsaved populated ticket, no database write. |
| Service board | [1440](native-service-board-1440.png) | [600](native-service-board-600.png) | [Concept](../../ui-concepts/service-board.png); MIDTERM-007 selected, one Confirmed, one Preparing, two Ready notes. |
| Menu | [1440](native-menu-1440.png) | [600](native-menu-600.png) | [Concept](../../ui-concepts/menu.png), [approved native](../workspaces-117/native-menu-1440.png); first real item selected. |
| Stock room | [1440](native-stock-room-1440.png) | [600](native-stock-room-600.png) | [Concept](../../ui-concepts/stock-room.png); Inventory, first selected ingredient, read-only details. |
| Customers | [1440](native-customers-1440.png) | [600](native-customers-600.png) | [Concept](../../ui-concepts/customers.png), [approved native](../workspaces-117/native-customers-1440.png); first real customer selected. |
| Reports | [1440](native-reports-1440.png) | [600](native-reports-600.png) | [Concept](../../ui-concepts/reports.png), [approved native](../workspaces-117/native-reports-1440.png); actual applied period and report data are in the native report. |
| Menu editor | [Native dialog](native-menu-editor.png) | Draft tested at 600 | [Concept](../../ui-concepts/menu-picture-editor.png), [approved native](../workspaces-117/native-menu-editor.png); unsaved name, Save/Cancel; native popup framing. |

The original artboards use sample names, dates, amounts, and counts that differ from this fixture. These comparisons expose differences; they do not establish equivalent-state pixel parity. The editor image captures its popup, not the entire 1440 x 900 shell. The old #117 framing/fixture approval is limited to its enumerated captures and does not automatically approve this newer implementation.

## Native checks and coverage limits

- Exactly six primary destinations, Counter after connection, a cached page instance and preserved presentation state through every resize.
- A keyboard-focusable enabled action in every workspace at each width, a separate `native-<workspace>-focus.png` capture, meaningful native button names, and measured primary targets of at least 48 x 48 DIPs. Counter's composite New ticket button now has an explicit accessible name. Menu, Customers, Reports, and Service board primary styles inherit the shared 48-DIP minimum.
- No horizontal scrolling on the page root. Counter's local category strip can scroll horizontally; its overflow is explicitly recorded and is not classified as page scrolling. Other workspace layout measurements remain in the report.
- Counter: customization, invalid quantity, cancellation, cart add/change/remove, keyboard focus after quantity changes, total updates, required-operator validation before writes, empty-search recovery, channel forms, and review of an existing saved ticket.
- Service board: populated column/header alignment, operator-dependent action availability, valid next-status labels, opening payment for an unpaid Ready order, reset of recoverable form input when selection changes, and contextual delivery controls. The supplement supplies the missing Confirmed and unpaid Ready states; these form checks pass at both widths.
- Menu: independent scrolling, exclusive tabs, active/inactive record action bindings, filtering/reset, and editor draft preservation across resize without saving.
- Stock room: isolated adjustment drafts, projected-balance validation, optional reason, scoped Cancel, low-stock filtering/reset, and history context without posting a movement.
- Customers: empty-search recovery, directory scrolling, delivery draft preservation, and profile-editor draft preservation without saving.
- Reports: filters/reset, daily details, invalid-period feedback, refresh of the applied period, empty periods, and stock independent of period.

These are programmatic native smoke checks. They do not replace a complete Tab/Shift+Tab traversal, assistive-technology inspection, or transactional operator workflow at both sizes. Persistence, inventory, accounting, and order-boundary behavior are separately covered by the complete automated suite.

## Affordance audit

| Capability | Production result |
| --- | --- |
| Federated search | Absent from shell; supported Menu, Counter, Customers, and ingredient-local search remain wired to existing queries. |
| Notifications | No notification action in shell or workspaces. Unused bundled icon files do not expose controls. |
| Persisted pictures | Drink photos/icons are decorative mappings. The editor contains supported text/category/size fields, with no upload or picture-save action. |
| Export | Reports exposes real Refresh, period, payment/channel filters, and daily details; no Export action. |
| Authenticated shifts | No shift/start/end/logout control. Existing demo operator selection remains a required workflow context, not an authentication claim. |

The active six XAML pages and their event handlers were inspected together with `MainWindow`. The native smoke report verifies supported filter/editor/action paths; no inert concept placeholder was added.

## Unresolved visual differences

These require correction to the reference or an explicitly approved targeted revision before task 8.4 and cleanup can be accepted:

- Shared shell: current sidebar selection inset, label/icon treatment, tracking, and header/status/date rendering differ from the original artboard. Live time/date differ from the sample clock.
- Counter: header/search/category arrangement, menu card frames/crops/density, cream ticket body, service/operator forms, totals/actions, and live fixture differ. The concept's direct Charge/pay controls conflict with the existing confirmation/payment boundary; Counter retains Save/review/confirm and routes payment to Service board. The current capture contains a real local draft rather than hard-coded concept totals.
- Service board: operator picker and supporting feedback, real queue population/selection, inspector actions and density differ. All three note colors are populated. Tape, folds, and shadows must still be compared against the reference with equivalent selected states.
- Stock room: native inventory rows, controls, detail card and balance/editor composition differ from the HTML. The approved four-tab/filter/draft behavior remains required; it does not by itself establish final image parity.
- Menu, Customers, Reports, editor: the original #117 exceptions remain documented in [the approved supplement](../../ui-concepts/workspaces-117-native-revision.md). PR #127 later changed photography/cards, spacing/type, native forms, report filters, and sidebar inset. Those newer differences need review against that bounded approval. The restored 48-DIP primary actions also require refreshed captures.
- Fixture: real seeded people/products/order statuses, selected records, reporting dates, totals, and editor draft text differ from the original artboards. Record and resolve the exact intended comparison state per screen; do not hard-code production data to hide it.

## Cleanup inventory after acceptance

The shell already constructs only six workspace types. Once Stock room replacement parity is accepted, remove the unreferenced `IngredientsPage`, `LowStockPage`, `PurchaseReceiptsPage`, `SuppliersPage`, and `InventoryAuditPage` XAML/code-behind pairs. Trace and remove their exclusive dependencies; keep dialogs and row types still used by Stock room and tests.

`App.xaml` still merges `SketchColors`, `SketchTypography`, `SketchShapes`, and `CustomerStyles`. Their consumers must be eliminated together before removing the dictionaries/fonts. Do not remove shared dialog resources by filename alone.

The old `pos`, `operations`, and `catalog` names are compatibility aliases rather than primary destinations. Inventory entry routes remain a specified capability: Reports currently calls `lowstock`, and the Stock room spec explicitly preserves low-stock and adjustment entry behavior. Retire temporary aliases only after migrating callers and coverage; do not remove required inventory entry routes indiscriminately.

## Reproduce

Use a disposable PostgreSQL 18 database. Apply the existing migrations, run `scripts/postgres/midterm-seed.sql`, then `scripts/postgres/ui-parity-seed.sql`. The supplement adds three namespaced active/inactive add-ons and one compatibility link because the base fixture has no add-ons. It also represents MIDTERM-023 as Confirmed and MIDTERM-016 as Ready with payment outstanding, updating their payment/history fixture rows consistently. It was run twice successfully; it changes fixture data, not schema or application rules.

```powershell
dotnet tool restore
# Set ConnectionStrings__CoffeeShop to the disposable capture database.
dotnet ef database update --project src/CoffeeShop.Infrastructure.Postgres -c Release
# Run both SQL fixture files in the capture database with ON_ERROR_STOP=1.
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -c Release
./scripts/verify-ui-parity.ps1 -OutputDirectory artifacts/ui-parity-new-run
python scripts/ui/build-parity-review.py artifacts/ui-parity-new-run

# Set COFFEESHOP_TEST_POSTGRES to a disposable administrative connection.
dotnet test CoffeeShop.slnx -c Release --logger trx --results-directory artifacts/ui119-tests
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
openspec validate migrate-winui-service-console --strict
```

Use a fresh capture output directory each time; the runner refuses stale directories. It runs the Release executable with `--verify-shell --verify-parity --verify-exit`, validates the native results, and records provenance without connection credentials. Screenshots and reports are produced under the chosen directory; ordinary launches retain their default behavior.
