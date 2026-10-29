# Counter workspace

The Counter keeps the existing cream, charcoal, brass, serif headings, and packaged
menu photography. Page-local styles increase control and reading text without
changing the other workspaces.

- At an available workspace width of 980 DIPs and height of 640 DIPs, the menu and
  ticket sit side by side. The ticket heading, totals, and save/confirm actions stay
  visible while its details scroll. Smaller or shorter windows use a stacked page.
- Menu cards distribute evenly across the available width. Search, category
  selection, size/add-on customization, and cart changes keep the existing flow.
  The compact page header and viewport-based card height show at least three
  complete rows at the 1440 by 900 desktop reference size, with the ticket actions
  visible alongside them. Photos fill the remaining card height; item names use
  one line with full names in tooltips, accessible labels, and the item dialog.
- Activating an item opens a compact native dialog with a larger photograph,
  size, quantity, compatible add-ons, and Add to ticket/Cancel actions. Invalid
  input stays in the dialog with an inline error. Cancel and Escape dismiss it
  without changing the ticket, and focus returns to the menu item.
  The native title overlay, outer frame, and button surface use the same cream
  ground and fine rule brushes as the content.
- Search uses one 36-DIP outlined surface with an inset icon and a lightly divided
  action. Hover and focus keep the input on the same paper-toned surface. Enter
  still submits the search. Category buttons use 32-DIP rows, compact spacing,
  and a taupe fill with a brass underline for the selected category.
- The ticket has a charcoal header, a cream interior with readable item names,
  44-DIP quantity and remove buttons on every row, and a fixed summary/action area.
  Order items come first. Service and operator fields appear below the items
  after the first item is added; dine-in reveals the table picker and
  delivery reveals the recipient/address fields. Saved tickets show their actual
  service details and immutable line snapshots.
- Service tabs use one centered radio group below the order. Only the selected option gets
  a fill and brass underline; hover changes opacity. Dine-in requires a table,
  takeaway shows pickup guidance, and delivery shows recipient, phone, and address
  independently of the optional customer chooser. Switching modes retains typed
  delivery details. Saved-address selection remains available after using a
  manual address and switching service modes.
- A new empty ticket displays the order and empty-state guidance, with service
  setup hidden. The header remains "Your order" while service choices change.
- Empty searches provide a Show all items action. Saving without an operator
  focuses the visible operator picker and shows an error inside the ticket.
  Missing table and delivery details are also checked before submission.
  Estimated totals remain distinct
  from persisted totals, and confirmation still requires a saved draft.
- Save and review is the primary action for a new ticket. A saved draft exposes
  Confirm order; active confirmed orders expose Service board. The helper text
  explains the current stage, and conflicts expose Refresh ticket.
- Cart updates preserve the menu source so changing quantity does not rebuild the
  product grid. Independent line identities keep identical items separate and let
  edits target a line after its quantity changes. Keyboard focus returns to the
  same action after a quantity update. Saved ticket item counts sum quantities.

## Verification

```powershell
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -r win-x64
# Launch the built client with:
CoffeeShop.WinUI.exe --verify-shell --verify-counter
```

The optional Counter verification uses catalog reads and an unsaved local cart.
It checks the item dialog, invalid-input retention, cancellation, addition, actual
row buttons, keyboard focus, total updates, missing-operator feedback, menu
stability, empty-search recovery, and review of an existing saved order when one
is available. It never saves or confirms an
order. Results appear under `counterUx` in
`%LOCALAPPDATA%\CoffeeShop\shell-verification.json`.

The shell verifier exercises 1440, 1008, 1007, 641, 640, and 600 DIP widths and
checks page preservation. The Counter flag adds `native-counter.png`,
`native-counter-compact.png`, and a `native-counter-customization.png` capture of
the complete dialog frame in the same local directory. It also captures a
populated ticket and workspace using an unsaved local cart, then clears that cart.
Checked-in captures are in `docs/ui-evidence/`, including `counter-populated.png`
and `counter-ticket.png` for the refactored panel.

The combined Counter and Service Board refactor was verified on 2026-10-29:
104 workspace tests and 18 PostgreSQL integration tests passed. The database tests create and remove
isolated temporary databases and exercise draft creation, review, confirmation
for all three service channels, conflicts, validation, and reload recovery.
Native checks passed for row buttons, keyboard focus retention, total updates,
and required-operator feedback. Saved-order review is covered by the database
tests; the checked-in native report skips it when no eligible saved order exists.
Additional native service-form checks cover exclusive selection, selected-state
styling, mode-specific fields, and delivery input retention. Per-mode captures
are `counter-dine-in.png`, `counter-takeaway.png`, and `counter-delivery.png`.
