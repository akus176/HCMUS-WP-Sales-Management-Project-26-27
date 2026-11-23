# Staff mobile sales contract coverage

Reviewed against `cc03321` for [#232](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/232). The [staff mobile OpenSpec](../../../openspec/changes/add-staff-mobile-app/specs/staff-mobile-app/spec.md) defines the release workflows. The [OpenAPI export](../../../contracts/mobile-p0/openapi.json) contains the shared API used by both clients.

`@kyrie25` owns the disposition of every operation below. `@vt-Toan` reviews the disposition and owns the focused Android follow-up in [#206](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/206). The permission column records the server policy. Client visibility also depends on the effective permissions for the mobile module and action.

## Operations used by Staff Mobile

Paths are relative to `/api/v1`.

| Method and path | API permission | Mobile surface and evidence |
| --- | --- | --- |
| `GET /preparation-tickets` | `orders.fulfill` | Preparation queue, Mobile 4.2, [A] |
| `GET /preparation-tickets/{id}` | `orders.fulfill` | Preparation detail, Mobile 4.2, [A] |
| `POST /preparation-tickets/{id}/start` | `orders.fulfill` | Confirmed to Preparing, Mobile 4.2, [A] |
| `POST /preparation-tickets/{id}/ready` | `orders.fulfill` | Preparing to Ready, Mobile 4.2, [A] |
| `GET /discounts/pending` | `discounts.approve` | Manager approval inbox and selected request, Mobile 4.3, [A] |
| `POST /discounts/{id}/approve` | `discounts.approve` | Manager approval displays the returned decision and total, Mobile 4.3, [A] |
| `POST /discounts/{id}/reject` | `discounts.approve` | Manager rejection displays the returned decision and total, Mobile 4.3, [A] |
| `GET /orders` | `orders.create` | Counter order search and Manager order lookup, Mobile 5.1–5.2, [B] |
| `GET /orders/{id}` | `orders.create` | Counter and Manager order detail, Mobile 5.1–5.2, [B] |
| `GET /pos/tables` | `orders.create` | Counter table occupancy, Mobile 5.1, [B] |
| `POST /orders/{id}/payments` | `payments.record` | Counter payment, Mobile 5.1, [B] |
| `POST /orders/{id}/cancel` | `orders.cancel` | Manager cancellation, Mobile 5.2, [B] |
| `GET /delivery-staff/options` | `delivery.assign` | Eligible courier selection, Mobile 5.2, [B] |
| `POST /orders/{id}/assign-delivery` | `delivery.assign` | Manager assignment of a ready delivery, Mobile 5.2, [B] |
| `GET /courier/assignments` | `delivery.update` | Assigned deliveries, Mobile 4.5, [A] |
| `GET /courier/assignments/{id}` | `delivery.update` | Assignment-scoped contact and address, Mobile 4.5, [A] |
| `POST /courier/assignments/{id}/start` | `delivery.update` | Out for delivery, Mobile 4.5, [A] |
| `POST /courier/assignments/{id}/complete` | `delivery.update` | Delivered, Mobile 4.5, [A] |
| `POST /courier/assignments/{id}/fail` | `delivery.update` | Failed with a required reason, Mobile 4.5, [A] |

- **A** covers Android progression, discount approval and rejection, assignment isolation, validation, conflict, offline and stale access. It verifies outcomes through a separate API session. [#201 Android reports and captures](../mobile-workflows-201/README.md)
- **B** covers payment retry identity, persisted balance, cancellation reason and assignment eligibility with component, API and Android evidence. [#182 verification and Android reports](../mobile-order-operations-182/README.md)

The evidence is tied to the source revisions in those reports. The selected P1 acceptance run remains tracked in #206. Final video timestamps remain tracked in #95, Mobile 6.6.

## Shared operations without a Staff Mobile workflow

The proposed first-release boundary marks these operations as out of scope in the OpenAPI descriptions. The listed alternatives indicate how Staff Mobile performs the supported action.

| Method and path | API permission | Current release disposition |
| --- | --- | --- |
| `POST /orders` | `orders.create` | WinUI POS draft creation |
| `POST /orders/{id}/confirm` | `orders.confirm` | WinUI POS confirmation |
| `POST /orders/{id}/complete` | `orders.complete` | WinUI order completion. Mobile courier completion uses its assignment route. |
| `POST /orders/{id}/discounts` | `discounts.request` | WinUI POS creates the request. Mobile managers use the approval inbox. |
| `POST /orders/{id}/preparing` | `orders.fulfill` | WinUI operations. Mobile uses the preparation-ticket start route. |
| `POST /orders/{id}/ready` | `orders.fulfill` | WinUI operations. Mobile uses the preparation-ticket ready route. |
| `GET /operations/orders` | `orders.fulfill` | WinUI operations list. Mobile uses the preparation-ticket list. |
| `GET /operations/orders/{id}` | `orders.fulfill` | WinUI operations detail. Mobile uses the preparation-ticket detail. |
| `GET /tables` | `orders.create` | Shared table query. Mobile uses `/pos/tables`. |
| `GET /delivery-staff` | `delivery.assign` | Shared staff query. Mobile uses `/delivery-staff/options`. |
| `GET /customers` | `customers.manage` | WinUI customer search |
| `POST /customers` | `customers.manage` | WinUI customer creation |
| `GET /customers/{id}` | `customers.manage` | WinUI customer profile |
| `PATCH /customers/{id}` | `customers.manage` | WinUI customer editing |
| `POST /customers/{id}/deactivate` | `customers.manage` | WinUI customer deactivation |
| `GET /customers/{id}/addresses` | `customers.manage` | Shared customer address list |
| `POST /customers/{id}/addresses` | `customers.manage` | WinUI address creation |
| `PATCH /customers/{id}/addresses/{addressId}` | `customers.manage` | WinUI address editing |
| `POST /customers/{id}/addresses/{addressId}/deactivate` | `customers.manage` | WinUI address deactivation |
| `POST /customers/{id}/addresses/{addressId}/default` | `customers.manage` | WinUI default address selection |
| `GET /customers/{id}/loyalty-transactions` | `customers.manage` | WinUI loyalty history |
| `POST /orders/{id}/loyalty/redeem` | `loyalty.redeem` | WinUI POS redemption |
| `GET /loyalty-settings` | Authenticated staff | Shared loyalty settings read |
| `PATCH /loyalty-settings` | `users.manage` | Shared loyalty settings change |

## Verification

| Command | Result |
| --- | --- |
| `dotnet test tests/CoffeeShop.Api.Tests --configuration Release --filter FullyQualifiedName~HostTests` | 10 passed, including the 24-operation OpenAPI scope assertion |
| `npm run check --prefix contracts/mobile-p0` | OpenAPI and TypeScript regenerated without drift. Contract types passed. |
| `npm run check --prefix apps/staff-mobile` | Type-check, lint, formatting, 235 Jest tests in 27 suites, and seven forwarding-harness tests passed. |
| `openspec validate add-staff-mobile-app --strict` | Passed. |

The OpenAPI comparison against `cc03321` found 24 new descriptions. Each appears as a documentation comment in the generated TypeScript. The Android reports above are the existing workflow evidence, and the PR runs the current native Android build and platform checks.

[A]: ../mobile-workflows-201/README.md
[B]: ../mobile-order-operations-182/README.md
