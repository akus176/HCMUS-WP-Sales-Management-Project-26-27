# Service console foundation and shell

Part 1 of the WinUI migration implements issue #111 checklist sections 1 and 2. It establishes the reusable concept system and the six-workspace shell. The existing workspace interiors remain functional and are recomposed in the later migration parts.

## Approved reference

The reference art revision is `d85ff63dadcfa5456e7a942a88ea98de7c565498`. The user approved 48-pixel primary controls and navigation rows and removal of unsupported global search, notifications, ticket counters, operator avatars, and shift controls. All six HTML artboards and their PNGs were regenerated. Native caption buttons retain operating-system behavior.

The common shell uses a 216-DIP rail, 40-DIP title area, 52-DIP workspace bar, EB Garamond statements, Instrument Sans controls, cream/charcoal/brass colors, the original café vector ornaments, and 48-DIP navigation targets. Dates and the clock use the running system. Connection status reflects the actual PostgreSQL check.

## Implementation

- `Theme/ServiceConsoleColors.xaml`, `ServiceConsoleTypography.xaml`, `ServiceConsoleGeometry.xaml`, `ServiceConsoleControls.xaml`, and `ServiceConsoleSurfaces.xaml` define the shared resources. Existing `Sketch*` keys are compatibility aliases for pages awaiting migration.
- `Controls/ConceptNote` provides status-colored paper, translucent tape, a folded corner, selected emphasis, and a native composition shadow. `ConceptPattern` tiles the original lattice without intercepting input or entering the accessibility tree.
- `scripts/ui/sync-concept-assets.py` packages the exact concept fonts, photos, ornaments, and patterns and exports standalone SVG icons from the concept sprite. Font licenses are included alongside the packaged fonts.
- `ServiceConsoleNavigation` defines six destinations, all eleven existing route aliases, Stock room section routing, and the 641/1008-DIP breakpoints. The shell caches pages and changes navigation presentation without recreating the active page on resize.
- Stock room has four tabs: Inventory, Receipts, Suppliers, and History. Low stock is an Inventory filter and stock adjustment is a selected-ingredient action. All six legacy inventory functions remain internal aliases; the shell does not duplicate the section selector. The user approved this refinement on 2026-10-28.
- The Windows manifest enables per-monitor DPI awareness. Decorative visuals are excluded from input and accessibility; actionable icon-only controls have explicit names and shared keyboard focus uses the brass focus treatment.

## Reproduce verification

```powershell
python scripts/ui/sync-concept-assets.py
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -r win-x64
```

Launch the built executable with `--components` to inspect and exercise the shared component gallery without a database. Add `--capture-components` for a native XAML reference capture. Launch with `--verify-shell` against the configured local database to exercise all eleven routes and resize the actual window through 1440, 1008, 1007, 641, 640, and 600 DIPs. Verification does not submit business commands or write business data.

Add `--verify-stock-room` to `--verify-shell` to run the resize checks with the consolidated Stock room active and capture it at 1440 by 900 and 600 by 900. This writes `stock-room-verification.json`, `native-stock-room.png`, `native-stock-room-compact.png`, and `native-stock-room-adjustment.png`. Route results include the canonical tab, filter and editor state; `stockBalanceRendered` verifies that row text survives template changes. Default captures show read-only details. A separate editor capture temporarily fills a local -120 adjustment and calibration reason, then cancels it without submitting. Counts, balances, and movements remain live database values. Successful navigation and page preservation do not establish exact visual acceptance against the concept.

The native Stock room card uses the approved editorial refinement: a cream surface, charcoal balance block, explicit status, and the same brass lattice used below the inventory table. Its bottom aligns with that table rule on wide windows; compact editors retain their natural height. Save and Cancel stay outside the scrolling form. Current screenshots are the [Stock room](ui-evidences/native-stock-room-1440.png) and [adjustment editor](ui-evidences/native-stock-room-adjustment.png) images in `docs/ui-evidences/`.

The verification modes write measured target sizes, resolved-resource counts, connection/default-route results, page-identity checks across resize, and optional native PNG captures to `%LOCALAPPDATA%\CoffeeShop`. `RenderTargetBitmap` provides a complete 1440 × 900 content capture when the monitor cannot display the full logical height. Caption buttons are operating-system chrome outside that XAML capture.

## Verified results

- Historical baseline: 39 WinUI tests passed before visual replacement, and the earlier expanded suite reported 58 tests. The current WinUI suite contains 108 passing tests, including legacy routing and inventory failure/selection characterization.
- Native build succeeds without warnings or errors, and all merged resources resolve during the component smoke run.
- All eight representative primary gallery controls measure at least 48 × 48 DIPs. Keyboard focus is visible and the primary action updates its feedback; the icon-only add action exposes `Add item`.
- The actual connected shell opens Counter by default. All eleven existing routes remain available through the six primary destinations; inventory aliases now share the Stock room host and enter the corresponding tab, filter or editor.
- Native resize checks preserve the exact same Counter page instance and route across every tested breakpoint. At 125% DPI, requested widths round to physical pixels; 641 DIPs selects Medium and 1008 selects Expanded.
- Disconnected startup blocks workspace navigation and exposes an actionable retry. Starting the local PostgreSQL server and invoking Retry transitions to connected Counter.
- Visual review covers the shared shell and component gallery: geometry and alignment, font assets, palette, vector icon/ornament treatment, paper/tape/fold states, and focus treatment. Workspace-interior fidelity and full transactional smoke verification remain assigned to later checklist sections.

Historical foundation captures and measurements are [archived in Git history](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/ui-foundation/). Current UI screenshots are in [ui-evidences](ui-evidences). The shell capture deliberately contains the pre-migration Counter interior; it is evidence for the shell, not acceptance of the future Counter layout.

## Menu, Customers, and Reports verification

Launch with `--verify-shell --verify-workspaces` to load real seeded data in Menu, Customers, and Reports, exercise an empty Menu search, resize each page through the shell breakpoint boundaries, compare presentation state, and capture expanded/compact native images. See the [#117 evidence archive](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/tree/d877088/docs/evidence/workspaces-117) and the [current UI evidence index](ui-evidences/README.md) for the reference revision, automated results, native captures, and visual acceptance status. This mode does not submit business commands. Successful resize checks alone do not satisfy the concept fidelity gate.
