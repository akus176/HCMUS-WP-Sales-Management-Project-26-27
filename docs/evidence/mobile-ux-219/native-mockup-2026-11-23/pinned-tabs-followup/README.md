# Pinned Orders / Tables bar — 2026-11-23

The Orders heading and Orders / Tables selector now reserve space above the scrolling records. Switching sections scrolls their content to the top while retaining order search and filter state. The pinned controls are removed when the workspace is hidden or a detail opens.

The real Android check scrolls the order list three times, verifies that Tables remains visible, opens table occupancy, and returns to order search. All steps pass. The attached flow expects the local Manager account to be signed in already and performs no business writes.

- [Before scrolling](pinned-orders-before-scroll.png)
- [After scrolling](pinned-orders-after-scroll.png)
- [Table occupancy](pinned-tables.png)
- [Return to Orders](pinned-orders-return.png)

TypeScript, ESLint, formatting and 138 focused navigation, order-operation and accessibility component tests pass. Updated regression assertions verify that the pinned controls disappear in Stock and that order search survives switching between sections. This is a targeted normal-text Android check, not a new full release or manual TalkBack acceptance run. Exact source hashes are recorded in `verification.json`.
