# Staff mobile API contract baseline (#137)

Date: 4 November 2026. OpenSpec change: `add-staff-mobile-app`, tasks 2.1–2.3.
This baseline is for the shared `/api/v1` ASP.NET Core API and PostgreSQL state. It
does not change the WinUI client or duplicate its domain rules. The active OpenAPI
document is served at `/openapi/v1.json` in Development and contains only mapped
routes. The P0 route decisions below are reserved for #142. Issue #151 has
published a subset of shared workflows; see `docs/shared-workflow-api-151.md`
for the routes now available.

## Available authentication contract

| Route | Request | Success | Authorization and failure |
| --- | --- | --- | --- |
| `POST /api/v1/auth/login` | `LoginRequest` (`username`, `password`) | `200 TokenResponse` | Anonymous; bad credentials or inactive user: `401` |
| `POST /api/v1/auth/refresh` | `RefreshRequest` (`refreshToken`) | `200 TokenResponse` with rotated refresh token | Anonymous; expired, revoked, reused or inactive session: `401` |
| `GET /api/v1/auth/me` | Bearer access token | `200 StaffSession` | Authenticated; missing, expired, invalid or inactive session: `401` |
| `POST /api/v1/auth/logout` | Bearer token and `RefreshRequest` | `204` | Authenticated; invalid or foreign refresh token: `401` |
| `POST /api/v1/auth/logout-all` | Bearer access token | `204` | Authenticated; invalid session: `401` |

`TokenResponse.user` and `/auth/me` return the same session shape: `id`,
`fullName`, `roles[]`, and sorted effective `permissions[]`. Permissions are the
union of active role grants and are loaded again for each authorized request;
the client must refresh `/auth/me` after a role change. The access token is a
Bearer JWT, expires in ten minutes, and does not carry the permission list.
Refresh tokens rotate and are never sent in a URL. Auth responses set
`Cache-Control: no-store`. No client-supplied actor ID is authoritative.

## P0 route and DTO decisions for #142

The target mobile contract uses `PageResponse<T>` with `items`, `page`,
`pageSize`, and `totalCount`; an empty result is `200` with `items: []`.
Route parameters are GUIDs. Query filters and ordering must be implemented
and verified by #142 before publication. Some transport records below are now
used by #151 routes and appear in the served OpenAPI document. The #151 routes
return arrays until #142 adds page envelopes and mobile aliases.

| Workflow and reserved route | Response / request contract | Required permission and server rule |
| --- | --- | --- |
| `GET /api/v1/preparation-tickets?status=confirmed,preparing` and `GET /api/v1/preparation-tickets/{orderId}` | `PreparationTicketResponse` with item, size, quantity and addon snapshots | `orders.fulfill`; show only confirmed/preparing tickets. No customer contact or payment fields. |
| `POST /api/v1/preparation-tickets/{orderId}/start` and `/ready` | `204`, then refetch ticket | `orders.fulfill`; server moves Confirmed → Preparing → Ready and records session actor/time. Changed or cancelled ticket: `409`. |
| `GET /api/v1/discounts/pending` | `PageResponse<PendingDiscountResponse>` | `discounts.approve`; include only pending requests and server-calculated money. |
| `POST /api/v1/discounts/{discountId}/approve` and `/reject` | `200 DiscountDecisionResponse`; no actor field | `discounts.approve`; decision and totals come from the domain transaction. Decided/stale discount: `409`. |
| `GET /api/v1/ingredients` and `GET /api/v1/ingredients/low-stock` | `PageResponse<IngredientBalanceResponse>` | `inventory.manage` for balances, `reports.lowstock` for low stock; low stock means active quantity at or below reorder threshold. |
| `POST /api/v1/ingredients/{ingredientId}/adjustments` | `StockAdjustmentRequest` → `200 StockAdjustmentResponse` | `inventory.adjust`; signed `quantityDelta` in the ingredient base unit, nonzero, nonnegative resulting balance, nonblank `reason`; actor from session and immutable movement history. |
| `GET /api/v1/courier/assignments` | `PageResponse<CourierAssignmentResponse>` | `delivery.update`; scope to authenticated courier ID and active assignments. List excludes recipient contact/address. |
| `GET /api/v1/courier/assignments/{orderId}` | `CourierAssignmentDetailResponse` | `delivery.update` and current assignment owner only; contact/address available only for active assignment. Other courier's assignment: `404`. |
| `POST /api/v1/courier/assignments/{orderId}/start`, `/complete`, `/fail` | `/fail` takes `FailDeliveryRequest`; success `204`, then refetch | `delivery.update` and current assignment owner; Assigned → Out for delivery → Delivered/Failed; nonblank failure reason. Stale/cancelled state: `409`. |

The API must bind every write to the authenticated staff ID. It must load
current records and enforce permissions and state transitions on the server,
using existing Application/Domain handlers and the same PostgreSQL database
as WinUI. A client must refetch after `204` or `409`. Issue #151 supplies
idempotency keys for draft, payment, and stock adjustment writes, plus
state-based conflict responses for delivery writes. The exact #142 mobile
aliases and pagination remain to be verified.

## HTTP errors and authorization

The P0 routes in #142 must return `application/problem+json` and RFC 7807
`type`, `title`, `status`, and `detail` fields. Existing auth and catalog
routes use problem JSON for the tested failures. Domain/Application failures
also include a stable `code`; validation may include an `errors` object keyed
by field. Clients should branch on HTTP status and `code`, never parse `detail`.
Do not include credentials, tokens, customer contact data, or internal stack
traces in problem responses.

| Status | Meaning for all contracts |
| --- | --- |
| `400` | Invalid GUID, missing reason, zero/invalid quantity, or malformed request; no mutation. |
| `401` | Missing/invalid/expired token or inactive account; clear the session if refresh fails. |
| `403` | Authenticated staff lacks the required effective permission; no mutation. |
| `404` | Record absent or not visible to this courier; no cross-assignment disclosure. |
| `409` | Changed order, discount, or delivery state; refetch before a new decision. |
| `422` | Other domain business rule failure when the existing handler returns that category. |

Issue #151 adds PostgreSQL-backed endpoint tests for its business routes.
OpenSpec mobile tasks 2.2 and 2.3 remain incomplete until the exact #142 P0
aliases, page envelopes, and generated-client verification are implemented.

## Reproduction

From a current `main` checkout with .NET 10 and a disposable PostgreSQL 18
server, set `COFFEESHOP_TEST_POSTGRES` to an administrator connection string
and run:

```sh
dotnet test tests/CoffeeShop.Api.Tests --configuration Release
```

The integration suite creates, migrates, and drops isolated databases. The
`HostTests` compare OpenAPI bytes across two request origins, assert the
Bearer scheme and protected operations. `StaffIdentityHttpTests` exercise six roles, combined
permissions, inactive accounts, refresh-token rejection, and live permission
changes. `CatalogHttpTests` exercise allowed/forbidden, invalid, not-found,
and conflict responses. No mobile or WinUI shared-state demonstration is
claimed by these tests.

The baseline results below predate #151; see its PR checks for current results.
On 4 November 2026, the full PostgreSQL-backed API suite passed 24/24,
Domain passed 413/413, Application passed 422/422, and Infrastructure passed
258/258. `openspec validate add-staff-mobile-app` passed. These results cover
the current implementation; they do not count as P0 endpoint tests for #142.
