# Shared workflow API (#151)

The `/api/v1` routes use the same PostgreSQL-backed Application handlers as the
Windows client. All routes require a Bearer staff session. Write actors come
from that session; request bodies never supply an authoritative actor ID.

| Workflow | Routes | Write permission |
| --- | --- | --- |
| Catalog | `/categories`, `/menu-items`, `/addons`, `/recipes/{variantId}` | `menu.manage` |
| Inventory | `/ingredients`, `/suppliers`, `/purchase-receipts`, `/ingredients/{id}/movements` | `inventory.manage`, `inventory.receive`, `inventory.adjust` |
| Sales | `/orders`, `/orders/{id}/confirm`, `/orders/{id}/payments`, `/orders/{id}/discounts`, `/orders/{id}/cancel` | `orders.create`, `orders.confirm`, `payments.record`, `discounts.request`, `orders.cancel` |
| Staff work | `/tables`, `/preparation-tickets`, `/orders/{id}/preparing`, `/orders/{id}/ready`, `/orders/{id}/complete`, `/discounts/pending`, `/discounts/{id}/approve`, `/discounts/{id}/reject` | `orders.fulfill`, `orders.complete`, `discounts.approve` |
| Delivery | `/delivery-staff`, `/orders/{id}/assign-delivery`, `/courier/assignments`, `/courier/assignments/{id}/start`, `/complete`, `/fail` | `delivery.assign`, `delivery.update` |

The exact methods, JSON schemas, and response codes are in `/openapi/v1.json`
when the API runs in Development. Collection responses in this issue are JSON
arrays. Mobile task #142 still owns page envelopes, filters, and its reserved
route aliases.

Order creation uses a client-generated `orderId`; identical retries return the
stored draft. Payment and stock adjustment requests also use client-generated
IDs. A matching retry returns the original effect; a changed retry returns
`409`. Receipt posting, discount decisions, preparation transitions, and
delivery transitions return `409` after their state has moved. Clients should
refetch after a timeout or conflict before issuing a new write.

Courier list responses contain no contact details. Only the assigned courier
may fetch an active assignment's contact/address or change its status. Another
courier receives `404`. Permission failures return `403`, invalid input `400`,
missing resources `404`, and changed state `409` as problem JSON.
