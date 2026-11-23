# Back and refresh header actions — 2026-11-23

Back actions sit to the left of the page title; refresh actions sit to the right. Both use 48dp icon targets with specific accessible names, focus feedback, and disabled/loading states. They stay outside scrolling records. Detail titles/actions replace route-level defaults and restore them on return. Manager workflow sheets own their headers and navigation state independently of Approvals.

Applied to Orders/Tables, Preparation, inventory balances/low stock/receipts/history, discount approvals, deliveries, staff administration, manager order operations and protected-route return controls. Receipt details have one return action and an always-reachable status refresh, including when loading fails. Connection recovery retains its Check connection action; online data refresh uses the header.

## Automated verification

Node 26.5.0 / npm 11.17.0: TypeScript, ESLint, formatting, **357 tests in 29 suites**, and **7 forwarding-harness checks** pass. New header checks cover left/right order, placement outside scrolling records at normal and 200% text, loading/disabled state, restoring route actions, and isolating nested sheet headers. The native-navigation regression verifies the ingredient detail header and returning to its list. Existing cache, permission, confirmation, cancellation, idempotency and accessibility scene checks remain passing.

## Android verification

Read-only navigation uses Expo Go on the API 36.1 emulator against the real local service with the Manager test account. The recorded Android hierarchy bounds check one refresh action at the right edge, one appropriate back action at the left when needed, and matching vertical centers. The normal-text flow also returns from a manager order detail and closes its sheet to verify the underlying Approvals header. No business writes are part of these checks.

The normal-text captures include Orders list/detail/scrolled detail, Stock balances, ingredient detail, receipt list/detail/return, Preparation queue/detail, Approvals, manager order list/detail and the return to Approvals. The 200% text flow checks Orders list/detail, Stock balances and receipt detail. Selected screenshots are saved with the hierarchy assertion results.

The floating gray Tools button belongs to Expo Go and was moved below the header for inspection. These results do not establish a new native courier/admin role matrix, iOS/tablet acceptance, manual TalkBack review, or named-reviewer approval. The prior system text scale is restored after the enlarged-text check; the real-service preview remains available.

## Selected captures

- [Orders list](normal/orders-list.png) and [order detail](normal/order-detail.png).
- [Receipt detail](normal/receipt-detail.png) and [Preparation detail](normal/preparation-detail.png).
- [Manager sheet detail](normal/manager-order-detail.png) and [Approvals after closing it](normal/approvals-return.png).
- [Order detail at 200% text](large/order-detail.png) and [receipt detail at 200% text](large/receipt-detail.png).

The normal run's modal assertion was corrected to measure within the sheet rather than applying the full-page top-edge threshold. Earlier launch/timing attempts are excluded from the final results.
