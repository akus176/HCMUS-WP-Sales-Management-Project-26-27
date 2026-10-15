# Order cancellation (T6.7)

`OrderCancellationCommandHandler.CancelAsync(new CancelOrderCommand(orderId, reason))`
requires an authenticated `ICurrentUser` with `orders.cancel`. The actor comes from
that user. Reasons are trimmed, mandatory, and limited to 255 characters to fit
the inventory audit record.

Draft, confirmed, preparing, and ready orders can be cancelled. Completed orders
remain terminal. A repeat cancellation returns `orders.invalid_state` as a conflict
(the planned HTTP mapping is 409), preserving the original reason, actor, timestamp,
and all compensating records. Orders without recipe consumption and walk-in orders
without loyalty entries can still be cancelled.

## Transaction and retry contract

Cancellation locks the order before reading its current state. It locks the selected
dine-in table, restores stock, reverses applicable loyalty entries, releases a table
occupied by that order, and saves cancellation history inside one database transaction.
A draft never releases a table occupied by another order.

Nested inventory and loyalty commands join the transaction. `ITransactionManager`
rolls back when the owning callback throws or returns a failed application `Result`,
including changes already saved by nested commands. PostgreSQL rollback also clears
EF tracking so a retry in the same scope reads persisted state again.

Restoration uses original consumption movements, rather than current recipes, and
locks affected ingredients in identifier order. An `order_inventory_restorations`
row keyed by order ID prevents duplicate processing. Existing restoration movements
are included when calculating outstanding quantities, supporting older records
without adding stock twice. Restoration can return stock to deactivated ingredients.

Loyalty history remains append-only. Earn and redeem entries are reversed once under
the existing customer lock and unique reversal constraints. If earned points were
spent elsewhere and cannot be reversed without a negative balance, the complete
cancellation returns a conflict and leaves order, stock, table, and loyalty unchanged.
Any pre-existing earned entries on imported orders use the same reversal path;
ordinary completed orders remain outside the cancellation state machine.

Delivery assignment and status updates acquire the same order lock and reject
cancelled orders, including when a scope previously read an active order. Cancellation
preserves delivery history and recorded payments; refund processing is a separate
workflow.

## Persistence and verification

Apply migrations normally with `dotnet ef database update --project
src/CoffeeShop.Infrastructure.Postgres`. The restoration guard adds a table without
changing historical movements. The delivery schema repair registers the existing
history migration and widens status columns to fit `out_for_delivery`, including
databases where newer migrations were applied while the history migration was missing.

For a local PostgreSQL test instance, set `COFFEESHOP_TEST_POSTGRES` to an admin
connection string, then run:

```powershell
dotnet test tests/CoffeeShop.Infrastructure.Tests --filter FullyQualifiedName~OrderCancellationIntegrationTests
```

The tests create and remove isolated databases. They cover changed recipes, stock
and loyalty reversal, table release, walk-ins, draft redemption, sequential and
concurrent retries, shared ingredients, rollback after nested saves, same-scope
recovery, the database restoration key, delivery/cancellation locking, and schema
upgrades. The PostgreSQL CI job runs this suite alongside order confirmation.
