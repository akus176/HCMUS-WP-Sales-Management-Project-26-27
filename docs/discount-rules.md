# Discount rules (T6.5)

`Order.RequestDiscount` supports `fixed` and `percentage` requests. A request records
its type, original value, mandatory trimmed reason, requester, UTC time, and pending
state. Pending and rejected requests never change `Total` or `DiscountTotal`.

Values must be positive, fit `numeric(12,2)`, and have at most two decimal places.
Percentage values cannot exceed 100. Percentages use the **remaining payable total**,
after earlier approvals and loyalty redemption, and round down to cents, as loyalty
does. Calculated amounts below 0.01 or above the current total are rejected. A fixed
discount equal to the total or a 100% discount can reduce the total to zero.

Both request and approval validate the current payable amount. Approval recalculates
and stores `ComputedAmount` once, increments ordinary `DiscountTotal`, and subtracts
only that approved amount from `Total`, preserving the separate loyalty discount.
Rejection records its actor and time without changing totals. Decisions cannot predate
the request. Approved and rejected requests cannot be decided again; retries return
a conflict without duplicate effects. Completed and cancelled orders cannot accept
new requests or decisions. Other order states follow the existing payment workflow.
Recorded payments remain unchanged; outstanding balance reflects the new total.

`DiscountCommandHandler` requires an authenticated current user with
`discounts.request` to request and `discounts.approve` to approve or reject. The
manager permission bundle is supplied by the current-user adapter; role names alone
do not grant access. An approver with that permission may decide their own request;
this ticket does not require a separate requester and approver. Actor identity and
time come from `ICurrentUser` and `IClock`, never from client-supplied approval data.
The handler returns shared validation, business-rule, not-found, authentication,
authorization, and conflict errors.

The PostgreSQL repository locks the order within an explicit transaction before
loading its discounts. Approval state, audit fields, and payable totals save in the
same transaction. This serializes competing approvals with the existing payment
and loyalty order locks. A second fixed request that no longer fits remains pending.
Application handlers are composed with the current-user and clock adapters in the
host; the PostgreSQL service extension registers the repository.
New discounts and payments are explicitly added through their repositories before
saving. Their supplied identifiers must not make EF interpret them as existing rows
when they enter a tracked order's collection.
Order status-history identifiers are also assigned by the domain; their mapping
keeps the database UUID default but disables EF key generation so completing a
discounted and paid order inserts its new history records correctly.

The migration adds reasons and rejection audit fields. Historical requests receive
the explicit placeholder `Legacy discount request (reason not recorded)`; the default
is removed so new requests must supply a reason. The decision constraint uses
PostgreSQL `NOT VALID` to retain old scaffold records with missing audit data without
inventing actors or times. It enforces consistency for every new or updated record.
Old incomplete decision rows remain historical; validation of those existing rows
requires recovering their original audit data first.

Coverage includes domain boundaries and rounding, application permissions and
shared errors, persistence across scopes, concurrent approvals, duplicate decisions,
transaction rollback after a save, database constraints, and migration upgrades.
PostgreSQL tests use the existing `COFFEESHOP_TEST_POSTGRES` connection setting.
