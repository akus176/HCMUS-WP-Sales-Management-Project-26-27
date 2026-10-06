# Loyalty calculation contract (T5.3)

`LoyaltyRules` calculates points and discounts from resolved `Customer`, `Order`,
and `LoyaltySettings` objects. `LoyaltyCalculations` exposes these operations and
settings creation/update through the shared Application `Result<T>` contract.
Neither layer depends on persistence or UI frameworks.

- Rates accept 0 through 9999.9999, with at most four decimal places, matching
  their existing storage precision. The maximum percentage accepts 0 through
  100, with at most two decimal places. Invalid updates leave settings unchanged.
- The customer must be active and attached to the order. Walk-in orders remain
  valid sales, but callers skip loyalty calculations for them.
- Earning requires a completed order with successful paid amount covering its
  final total. Whole points are `floor(order.Total * earnRate)`: the total is
  after ordinary discounts and loyalty redemption. Overpayment earns no extra
  points. Zero earning rate disables earning. A result that would overflow the
  integer customer balance is rejected.
- Redemption is available only on draft or confirmed orders with no existing
  loyalty redemption. `order.Total` is therefore the amount after ordinary
  discounts and before loyalty redemption. A zero redemption rate or zero
  maximum percentage disables redemption.
- The monetary cap is `floor(order.Total * maxPercentage / 100 * 100) / 100`.
  Maximum points are the lesser of the available balance and
  `floor(cap / redeemValuePerPoint)`. This comparison uses the exact conversion
  before rounding so rounding cannot allow extra points beyond the cap.
- Requested points must be positive and no greater than that maximum. The
  discount is `floor(points * redeemValuePerPoint * 100) / 100`; conversions below
  0.01 are rejected to avoid consuming points for no payable reduction. When the
  maximum possible redemption is below 0.01, the maximum quote returns zero.
- Invalid inputs return `loyalty.invalid_input` validation errors; inactive
  customers and ineligible order states return `loyalty.invalid_state` conflicts.

For example, a final paid total of 85 at an earning rate of 0.1 earns 8 points.
A pre-redemption total of 100, a 20% cap, a conversion of 0.50, and a balance of
30 allow redemption of 30 points for 15.00. At a conversion of 0.3333, three
points yield a discount of 0.99.

These operations are quotes: they do not change balances, orders, or transaction
history and do not reserve points. T5.4 supplies append-only transactions and
reversal; T5.5 applies the rules with persistence and retry/concurrency guards.
Those workflows must load current settings and customer/order state, revalidate
at commit time, and apply the balance, order discount, and ledger changes in one
transaction. Reversals must use the original transaction values, not new settings.

## Ledger operations (T5.4)

`Customer.EarnLoyaltyPoints`, `RedeemLoyaltyPoints`, and
`ReverseLoyaltyTransaction` append immutable `LoyaltyTransaction` entries and
update the cached balance and customer timestamp together. `LoyaltyLedger`
exposes these operations through Application results. These are domain ledger
operations on resolved objects; they do not load or save data or change orders.

- Earning and redemption take positive, already validated point amounts.
  Redemption stores a negative delta. A zero earning quote requires no ledger
  call because zero-delta entries are forbidden.
- Callers must load the customer's ledger, validate order attachment,
  eligibility, and current settings with `LoyaltyRules`, and persist the ledger,
  balance, and any order changes in one transaction. A customer constructed with
  an existing balance represents persisted state, not a new earning event.
- Matching retries use the transaction ID, or `(orderId, type)` when an order
  is present. They return the existing entry without changing the balance or
  timestamp. Reusing either identity with a different amount or operation
  returns a conflict. New operations are rejected for inactive customers.
- A reversal locates the original earning or redemption in this customer's
  ledger, negates its points, copies its order reference, and links to its ID.
  It never recalculates from settings or modifies the original entry.
- Reversal retries return the existing reversal by original transaction ID,
  even with a new request ID, no order reference, or later balance changes.
  A request ID belonging to a different operation is rejected. Reversing a
  reversal is rejected. Historical reversals are allowed for inactive customers.
- If earned points have already been spent and reversing them would make the
  balance negative, the reversal returns a conflict without changing any state.
  Balances exceeding `int.MaxValue` are also rejected without mutation.
- Invalid arguments return `loyalty.invalid_input`; state or balance conflicts
  return `loyalty.invalid_state`; an original transaction missing from the
  customer's ledger returns `loyalty.transaction_not_found`.

T5.5 must load sufficient ledger history for these checks and serialize or
concurrency-check customer balance updates. The existing order/type database
index alone does not protect orderless reversals or concurrent operations on
different orders. Persistence needs a unique original-reversal reference and
enforcement against updating/deleting stored ledger entries. In-memory retry
checks are not a substitute for these database guarantees.
