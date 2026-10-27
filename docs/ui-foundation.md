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
- A temporary Stock room section menu keeps Balances, Low stock, Receipts, Suppliers, Adjustments, and Movements reachable while the later consolidation replaces their page interiors.
- The Windows manifest enables per-monitor DPI awareness. Decorative visuals are excluded from input and accessibility; actionable icon-only controls have explicit names and shared keyboard focus uses the brass focus treatment.

## Reproduce verification

```powershell
python scripts/ui/sync-concept-assets.py
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -r win-x64
```

Launch the built executable with `--components` to inspect and exercise the shared component gallery without a database. Add `--capture-components` for a native XAML reference capture. Launch with `--verify-shell` against the configured local database to exercise all eleven routes and resize the actual window through 1440, 1008, 1007, 641, 640, and 600 DIPs. Verification does not submit business commands or write business data.

The verification modes write measured target sizes, resolved-resource counts, connection/default-route results, page-identity checks across resize, and optional native PNG captures to `%LOCALAPPDATA%\CoffeeShop`. `RenderTargetBitmap` provides a complete 1440 × 900 content capture when the monitor cannot display the full logical height. Caption buttons are operating-system chrome outside that XAML capture.

## Verified results

- The original 39 WinUI tests passed before visual replacement; the expanded 58-test suite passes, including legacy routing and inventory failure/selection characterization.
- Native build succeeds without warnings or errors, and all merged resources resolve during the component smoke run.
- All eight representative primary gallery controls measure at least 48 × 48 DIPs. Keyboard focus is visible and the primary action updates its feedback; the icon-only add action exposes `Add item`.
- The actual connected shell opens Counter by default. All eleven existing routes reach their original page types through the six primary destinations.
- Native resize checks preserve the exact same Counter page instance and route across every tested breakpoint. At 125% DPI, requested widths round to physical pixels; 641 DIPs selects Medium and 1008 selects Expanded.
- Disconnected startup blocks workspace navigation and exposes an actionable retry. Starting the local PostgreSQL server and invoking Retry transitions to connected Counter.
- Visual review covers the shared shell and component gallery: geometry and alignment, font assets, palette, vector icon/ornament treatment, paper/tape/fold states, and focus treatment. Workspace-interior fidelity and full transactional smoke verification remain assigned to later checklist sections.

Review captures and measurement reports are in `docs/evidence/ui-foundation/`. The shell capture deliberately contains the pre-migration Counter interior; it is evidence for the shell, not acceptance of the future Counter layout.
