# Mobile Counter workflow

The [26 November 2026 scope decision](mobile-scope-206.md) selects these Counter
features and Preparation for the current mobile release. #206 remains open for
current Android/shared-API evidence and named-reviewer approval.

The mobile Counter now follows the Windows Counter flow documented in
[counter-ui.md](counter-ui.md). Mobile navigation remains **Today, Counter,
Preparation, More**; More contains Account. The courier workspace, Stock,
administration and discount approval inbox remain outside mobile navigation.

## Making a sale

Counter opens on **New sale**, with **Orders** beside it. Table selection is part of the dine-in ticket form.
Search the live catalog, filter by active category, and customize an item with
an active size, compatible add-ons and a positive whole-number quantity.
Canceling customization does not change the cart. Identical items stay separate
so staff can adjust or remove each customization independently.

Open **Your order** to edit quantities, choose takeaway, dine-in or delivery,
select an existing customer, or enter delivery details. Dine-in requires an
available table. Customer selection uses `customers.manage`; an existing
customer's active default or other saved address can be selected. Editing the
address manually clears the saved address identifier. Switching service modes
retains form values but submits only the fields appropriate to that mode.
The operator is the authenticated staff member, as in the API-backed Windows
Counter; the app never submits another operator's identity.

**Save and review** creates a draft through the existing shared order API.
Menu prices in the cart are estimates. After saving, staff review the server's
persisted item snapshots and total; the draft's contents are locked.
**Confirm order** opens a separate review and submits the exact reviewed total
through `orders.confirm`. Confirmation sends the order to Preparation using
the existing stock/table rules. A changed total or pending discount request
blocks confirmation until staff refresh and review again. Payment remains
available for eligible confirmed orders through the existing payment review.
Existing drafts can also be opened and confirmed from Orders.

Eligible staff can request a discount (`discounts.request`) or redeem loyalty
points on a saved draft (`loyalty.redeem`, selected customer). Discount requests
are not approvals; manager decisions happen outside the mobile approval inbox.
All adjustments require a review before submission. Canceling a review writes
nothing. Customer administration and menu maintenance are not included.

## Retention, refresh and retry

The menu, current cart, service fields and loaded order pages are retained
across warm navigation. Switching pages alone does not invalidate query data.
Header refresh, reconnection, relevant operational events and successful writes
remain explicit freshness triggers. Menu cards render in batches of 20, up to
the API's 200 search matches. Large text uses a single column and the shared
header reflow rules. Your order floats above the bottom navigation; measured clearance keeps the final menu items reachable.

Offline actions do not write or queue a replay. A lost draft-save response locks
the same request body and order identifier for an explicit retry or read-back;
it cannot become a second sale by editing the uncertain ticket. Confirm,
discount and loyalty commands similarly retain their reviewed arguments when
the result is unknown. Starting another ticket is blocked while a ticket action
or payment remains unresolved. Drafts are kept in memory for the signed-in
session, not stored durably on the device; sign-out clears session query data.

Menu photos use the Windows Counter's public bucket keys. Emulator mode defaults
to `http://10.0.2.2:9000/menu-images/`. Device/release builds can set the public
`EXPO_PUBLIC_MENU_PHOTOS_BASE_URL`; absent or failed images use a cup icon.

## Validation

`apps/staff-mobile/tests/counter-sale.test.tsx` covers request payloads,
validation, canceled customization/reviews, retained cart/service state,
authoritative snapshots, reviewed confirmation totals, changed totals,
retry identity, offline writes and permission gates. Native navigation
regressions also exercise the menu-to-Orders entry and warm tab retention.

Per the requested workflow, tests, type checking and Android validation are
left to GitHub CI. No local test run or live sale submission is claimed.
