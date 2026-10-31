# Customer and loyalty screens (T5.6 / #55)

Open **Customers** from the desktop navigation. Search by name,
phone, or email; normal search excludes inactive profiles. Include inactive
profiles to inspect historical addresses, orders, balances, and loyalty entries.
Search shows up to 100 matches and asks the operator to narrow a full result set.

The existing desktop layout remains: customer directory on the left, profile,
saved addresses, and loyalty ledger in the middle, and the next-order card and
recent orders on the right. Page-local styles use the cream, charcoal, and brass
theme with readable contact text, named profile actions, and a prominent loyalty
balance. Search and its button share one surface; the inactive filter refreshes
results immediately, and empty results offer Clear search.

Desktop columns scroll independently. Narrow windows retain the existing stacked
reading order with all controls reachable by scrolling. Loyalty entries show
signed point changes and balances, local dates, and shortened order/reversal
references with the complete references in tooltips and accessibility names.
Order totals use two decimal places. The next-order heading reflects whether the
form currently attaches a profile or continues as a walk-in.

An operator can create, edit, or deactivate a profile and add, edit, deactivate,
or choose a default saved address. Deactivation requires confirmation and keeps
historical data. An inactive profile can be inspected but cannot be attached to
a new order. History is read-only, uses 20-entry pages, and displays point changes,
resulting balances, order references, and reversal references. Recent orders show
the latest 20 orders, as defined by the customer query contract.

The **Customer for the next order** section supports either an attached active
profile or a walk-in, with saved or order-only delivery information. Order-only
addresses are not saved to a customer. Recipient name, phone, and address are
required for delivery even when no profile is attached. Choosing a customer does
not create or complete a sale. The shared selection survives page navigation;
**Continue as walk-in** clears both the profile and delivery selection.

## Composition and sales integration

`App` registers `CustomerCommandHandler`, `CustomerQueryHandler`, and
`CustomerOrderSelectionHandler`. `CustomerWorkspace` and `CustomerOrderContext`
are singletons containing presentation state; every operation creates and disposes
a service scope so EF tracking never outlives an operation. Pages and view models
use Application contracts without referencing database types. Page-local styles
reuse the existing serif/sans fonts, semantic brushes, and visible focus outlines.

The POS implementation in T6.9 should consume `CustomerOrderContext.Request` using
`CustomerOrderSelectionHandler.CreateDraftAsync(draftRequest, selectionRequest)`.
This reloads the selected profile/address, rejects inactive or foreign addresses,
and creates normalized delivery snapshots. Its `CustomerOrderDraft` result contains
both `Order` and optional `Delivery`; the sales persistence workflow must persist
them together. The helper prepares a draft; it does not confirm, pay, or persist it.
It supports dine-in (with a table), takeaway, and delivery, including walk-ins.

After the sales workflow commits completion and calls the existing
`LoyaltyCommandHandler.EarnForCompletedOrderAsync`, call
`CustomerWorkspace.RefreshAfterOrderCompletionAsync(customerId)` on the UI thread.
The method reloads balance, recent orders, and the first page of history without
writing points. Sales owns order completion and earning; the customer screen never
awards points merely because a profile is opened or refreshed. The loyalty handler
requires a real current operator and configured loyalty settings in its sales host.
T6.9/T6.10 remain responsible for the POS/completion screens and invoking this hook.

## Error handling

Loading disables controls and exposes progress. Empty search, address, history,
and recent-order states have their own messages. Application validation details and
conflicts remain visible in the editor without closing it. Database exceptions
show a recovery message without connection details. After an uncertain write or
failed refresh, mutation is disabled until reload succeeds. A successful save whose
refresh fails closes the editor and explicitly tells the operator not to resubmit.
Saved order selections are refreshed after edits and cleared if unavailable.

## Verification

The native `--verify-shell --verify-customers` launch captures the page at 1440
and 600 logical pixels, checks the original three-column/stacked layouts, and
confirms selection is preserved through seven resize steps. It exercises empty
search and recovery, independent directory scrolling, delivery-form drafts, and
an unsaved profile editor across resizing without submitting writes. Current screenshots are in [ui-evidences](ui-evidences).

```powershell
dotnet build CoffeeShop.slnx
dotnet test tests/CoffeeShop.WinUI.Tests
dotnet test tests/CoffeeShop.Application.Tests --filter FullyQualifiedName~CustomerOrderSelectionTests
# Point this at a disposable PostgreSQL instance with CREATE DATABASE permission.
$env:COFFEESHOP_TEST_POSTGRES = "Host=localhost;Database=postgres;Username=<test-user>;Password=<test-password>"
dotnet test tests/CoffeeShop.Infrastructure.Tests --filter FullyQualifiedName~CustomerLoyaltyPersistenceIntegrationTests
```

The workspace tests cover profile/address lifecycle, active selection, walk-in
delivery, field validation, conflicts, loading, pagination, database recovery, and
failed refresh after a successful write. Application tests prove draft creation
revalidates saved addresses and preserves delivery snapshots. PostgreSQL tests
rebuild a database, execute Application services through fresh workspace scopes,
and prove a paid completed order changes the visible balance once across retries.
CI runs the customer PostgreSQL tests alongside the catalog integration suite.

Verified on 2026-10-14: solution build (zero warnings/errors), repository format
verification, and 926 passing tests with PostgreSQL 16 (zero failures/skips).
The native client smoke check confirmed empty results, validation without closing
the editor, profile and saved-address creation, readable expanded layout,
accessible row names, saved-address delivery selection, navigation persistence,
and the database-error recovery message against an unavailable connection.

For a native smoke check, migrate a disposable database, set
`ConnectionStrings__CoffeeShop`, and run the WinUI client. Check empty search,
create/edit/deactivate dialogs, saved and order-only addresses, history pagination,
walk-in selection, and navigation persistence. Restart with an unavailable database
to check the recovery message. At narrow window widths the profile moves below
the search list; keyboard focus remains visible and controls retain 48px targets.
