# Service Board

The board keeps the cream, charcoal, and brass theme, with yellow confirmed notes,
peach preparing notes, and sage ready notes. A smaller page heading leaves more
room for work. Each column has an aligned heading, count, short explanation, and
empty state. Notes show the amount due and use 44-DIP action targets.

The Service board focuses on active orders and their ticket details; it does not
show a separate overview of all dining tables. Dine-in tickets retain their table
number. Use the **Tables** workspace under **Manage** to inspect current
availability and manage table setup.

The ticket inspector uses a dark identity header and a light body so items, sizes,
add-ons, two-decimal prices, totals, and the primary action remain readable. It
shows one next action for the selected order. Payment, discount requests,
delivery, and cancellation have separate disclosures. Delivery controls follow
the current delivery stage and appear only on delivery orders. Discount requests
show their value instead of raw identifiers.

Unpaid ready notes expose **Review payment**. This selects that note and opens
the payment form without changing order state. **Collect payment** in the inspector
does the same. The amount defaults to the balance due and updates after partial
payments. Switching tickets clears payment references and cancellation/failure
drafts, resets the payment method, and refreshes the amount and courier selection.

Layout follows the page's available width after the navigation sidebar is taken
into account. With enough room, three columns sit beside a 320-DIP inspector;
the lists and inspector can scroll separately. At medium widths the inspector
moves below the three columns. Narrow layouts stack columns and header controls,
and selecting a note brings its inspector into view.

Notes can advance using their own action button. Confirmed tickets expose Start
preparing, preparing tickets expose Mark ready, and fully paid ready tickets expose
Complete. Unpaid ready tickets retain their current state until payment has been
recorded and the operator explicitly completes the order.

Confirmed notes can also be dragged into Preparing, and preparing notes into Ready.
Other destinations reject the drag. Ready notes use their completion action rather
than dragging, because the board has no Completed column. All actions require an
operator, selectable on the board. Buttons remain the keyboard alternative to drag.

Both interactions call `OperationsWorkspace.MoveOrderAsync` with the note's own
order ID. The workspace validates the current state and payment eligibility, then
uses the existing transactional fulfillment handler. It refreshes columns and counts
after success and reports errors without moving the note. Completed orders leave
the active columns. Keyboard activation restores focus to the moved note.

Column headers have matching heights and share the paper's left/right edges. Native
list padding and selection indentation are removed; the existing paper outline marks
the selected note. Paper colors, tape, folds, and shadows are preserved.

## Verification

```powershell
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj
dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj -r win-x64
# With COFFEESHOP_TEST_POSTGRES set to the local test administrator connection:
dotnet test tests/CoffeeShop.Infrastructure.Tests --filter FullyQualifiedName~BoardActions_TargetTheRequestedNote
# Launch the built client with:
CoffeeShop.WinUI.exe --verify-shell --verify-service-board
```

The database test uses a temporary database. It verifies that an action moves its
requested ticket even when another is selected, rejects skipped/reversed states,
blocks unpaid completion, and completes after payment. The workspace suite includes
operator and state eligibility tests.

The native verifier reads live orders and temporarily selects a local operator for
capture without submitting business commands. `boardUx` in the local
`shell-verification.json` records heading alignment, measured paper/title positions,
drag eligibility, action state for each note, payment disclosure, form clearing,
and contextual delivery controls. Resize checks also record column count,
available note width, and content/inspector bounds at each shell breakpoint.
Screenshots include wide, compact, payment, and delivery views; reviewed copies
are under [ui-evidences](ui-evidences).

The `ui-ux-pro-max` WinUI layout search supports adapting the layout to available
space. Its disclosure query returned no relevant match; the separate form
sections follow the skill's general forms/feedback priority guidance instead.
