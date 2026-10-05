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
