# POS ticket workflow (T6.9)

The Point of Sale navigation item hosts the direct-database midterm workflow.
`PosWorkspace` has no XAML dependency and is the same presentation workflow used
by the PostgreSQL scripted checks. Pages and presentation models call Application
handlers, never EF Core. No schema or migration changes are required.

## Operator path

1. Configure `ConnectionStrings__CoffeeShop` for a migrated local PostgreSQL
   database. Existing active staff, a drink with active size variants, compatible
   add-ons, recipes, stock, and a table are required; this task does not seed data.
2. Select an active operator. This is explicit midterm actor selection, not a
   login or proof of identity. The local session only supplies `discounts.request`,
   never manager approval. The final API must replace this session with its
   authenticated context. The Application handler rechecks active staff on writes.
3. Choose dine-in, takeaway, or delivery. Dine-in requires an available table;
   confirmation rechecks occupancy under the existing table lock. Takeaway and
   dine-in omit all delivery fields.
4. Continue as a walk-in, or use **Choose customer / saved delivery address** to
   open the existing Customers screen. Attach a customer/address there, return to
   Point of Sale, then press **Use selection from Customers**. For delivery,
   provide a recipient, contact number, and either the attached saved address or
   an order-only address. Customer/address availability and ownership are checked
   again when saving and confirming. Saved delivery text is an order snapshot;
   later address edits do not rewrite it.
5. Search the menu, choose a drink and size, select optional compatible toppings,
   and enter a positive whole-number quantity. Each topping is charged once per
   drink, including on multi-drink lines. Add or remove lines before saving.
   The search shows up to 200 matches; narrow the search for additional products.
6. **Save draft and review exact totals** persists current catalog snapshots.
   The ticket shows names, sizes, per-drink prices, topping prices, quantities,
   line totals, customer/channel details, subtotal, discounts, and payable total.
   Unsaved menu prices are estimates; review the saved ticket before proceeding.
   Saved selections are read-only. Start a new ticket to change them; no saved
   order is deleted by doing so.
7. Optionally request a fixed or percentage discount with a reason. Pending
   requests are visible but do not reduce the total. Approval remains in the
   existing discount use case / T6.10 manager workflow. Reload after approval and
   review the total. A total changed by another operator blocks confirmation
   until reloaded, rather than silently confirming a different amount.
8. **Confirm reviewed order** asks for confirmation, then invokes the existing
   atomic confirmation handler. It checks current menu availability, consumes
   stock, occupies the table for dine-in, and records confirmed status. Failure
   leaves the draft and inventory unchanged. A confirmed ticket cannot be sent
   again by the screen; the Application confirmation handler also supports safe
   retries.

The goal for #75 is creating and confirming one ticket per channel. This does
not transition the order to the Domain `completed` state. Payment, preparation,
completion, cancellation, manager approvals, and delivery-operation screens are
T6.10, not part of this page.

## Recovery and navigation

The singleton workspace retains the cart, selected customer, channel, and saved
ticket across navigation. Copy the selectable **Order ID** before starting a new
ticket; **Open saved ticket** reloads it after navigation or application restart.
There is no automatic cross-restart recovery for unsaved carts.

Draft creation uses a stable client-generated order ID. If a database operation
fails unexpectedly, its write may have succeeded. The screen disables further
writes and starting a new ticket until **Reload menu and ticket** resolves that
identity. Reloading an existing ticket does not create another order. If the
initial save never persisted, the same identity can be saved again. A successful
discount request followed by a read failure is likewise reload-only; inspect
pending requests before asking for another discount. Validation keeps entered
values and cart lines. Conflict messages require a fresh reload. Not-found,
empty, loading, stock-shortage, and database-error states are visible.

## Automated checks

```powershell
dotnet build CoffeeShop.slnx --configuration Release
dotnet test tests/CoffeeShop.WinUI.Tests --configuration Release
# Supply an isolated test-server connection through COFFEESHOP_TEST_POSTGRES.
dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~PosWorkspaceIntegrationTests
```

The PostgreSQL tests create uniquely named databases, apply migrations, run the
actual presentation workflow with fresh scopes, and drop only those databases.
They explicitly skip when the test connection is absent. The Ubuntu PostgreSQL
CI job includes these checks. Coverage includes all three channels, saved and
walk-in addresses, shortage rollback, occupied-table conflicts, pending/approved
discounts, changed totals, snapshot preservation, stable-ID retries, inactive
customer selection, loading, input preservation, not-found, and empty states.

These checks are headless; they do not certify pointer interaction or rendering.

## Manual desktop / accessibility check

- At expanded width (1008px+), verify the entry form and hard-shadow paper ticket
  appear side by side. Below that width, verify they stack and remain scrollable.
- At 640px and with increased text scaling, check that long names, addresses,
  errors, and prices wrap without obscuring the confirm/reload controls.
- Tab through operator, channel, customer selection, size, toppings, quantity,
  save, discount, and confirmation. Verify visible blue focus and 48px targets.
- Navigate to Customers and back; confirm selections remain and the attached
  address is visible after **Use selection from Customers**.
- Exercise the confirmation dialog with both Keep reviewing and Confirm order.
  Verify the displayed saved total matches the dialog and resulting ticket.
- Make the database unavailable and verify recovery is reload-only with the
  order ID and inputs preserved. Restore connectivity and reload before retrying.
