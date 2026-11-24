# Pinned navigation and cached page changes — 2026-11-23

Screen titles, section navigation, Preparation filters, record-return controls, and normal-size list search/filter controls now use shared fixed header regions. Bottom navigation and existing preparation action docks remain reserved outside scrolling records. At more than 150% text or under 560dp height, search/filter fields reflow into the scroll area so the fixed header does not consume the usable page.

Local workspace changes share the verified `/session` route and reuse loaded query data. They no longer trigger repeated identity checks, suspend the entire workspace tree, or refetch unchanged records on remount. Initial visits and new search/filter keys still load normally. Explicit refreshes, successful edits and committed live events invalidate cached data; reconnects refresh the current views. Session exit clears the query cache. Changed effective permissions discard the previous user's query data. Protected route entry, foregrounding, reconnects and account events revalidate the session; backend authorization and online-only writes are unchanged.

Stock receipt and movement lists initially render 20 rows, with Show more controls retaining access to every returned record. The observed local history contained 200 movements and the receipt list contained 72 records. This bounds rendering rather than discarding data.

## Checks

- Node 26.5.0 / npm 11.17.0: TypeScript, ESLint and formatting pass.
- **353 tests / 29 suites** and **7 forwarding-harness checks** pass.
- The navigation regression warms Orders, Stock balances, Stock history and Today, then performs **15 main-tab/Stock-section transitions with zero additional fetch calls**, including no repeated identity requests. An explicit refresh still refetches.
- Permission removal after foregrounding, protected route re-entry, reconnect/account-event access refresh, cancellation and idempotency regressions remain covered.
- Successful stock adjustments, receipt posts and discount decisions invalidate cached home counts even without a live event; stock writes also invalidate the retained movement history. These checks extend existing successful-write cases without creating live business records.
- Receipt and movement batch tests verify that all 45 fixture records remain reachable without a write.
- Two complete Android Manager flows pass against the real local API, at **100% and 200% text**, with six screenshots each. They verify fixed Stock sections while history scrolls, receipt navigation, Orders navigation/search, Preparation filters, and returning between loaded workspaces.

## Captures

| State | Normal text | 200% text |
| --- | --- | --- |
| Stock tabs and search | [View](normal/stock-pinned-top.png) | [View](large/stock-pinned-top.png) |
| Scrolled movement history | [View](normal/stock-history-pinned-after-scroll.png) | [View](large/stock-history-pinned-after-scroll.png) |
| Receipts | [View](normal/stock-receipts-pinned.png) | [View](large/stock-receipts-pinned.png) |
| Scrolled Orders | [View](normal/orders-search-and-tabs-pinned.png) | [View](large/orders-search-and-tabs-pinned.png) |
| Scrolled Preparation | [View](normal/preparation-pinned-filters.png) | [View](large/preparation-pinned-filters.png) |

The request-count proof is a deterministic component test, not a packet capture or FPS benchmark. Staff/courier screens share the pinning components and have component coverage; this is not a new full native role, tablet, iOS or manual TalkBack acceptance run. Earlier failed/stalled emulator attempts are not counted as passes. Raw logs and credentials are excluded. The original font scale was restored after the large-text run, and the correct real-service preview remains running.
