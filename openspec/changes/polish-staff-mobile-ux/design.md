# Design

## Context

See proposal.md for motivation. The existing Expo Router shell has permission checks on route focus, online-only mutations, idempotent payment retry, scalable shared controls and a light paper palette. The global banner consumes substantial height. Several workflows stack filters, lists and details in a single scroll view.

## Goals / Non-Goals

**Goals:** expose working content sooner; preserve enlarged text and 48dp controls; make detail transitions and form feedback predictable; distinguish confirmed writes from refresh and transport uncertainty.

**Non-Goals:** new business actions, role changes, bottom-tab navigation, a new visual identity, a dark palette, new dependencies, generic transport retries or a production deployment.

## Decisions

1. Keep the existing routes and permission boundaries. Add reusable page/header/input/selection components. Local account details replace the list while keeping search and filter state mounted. Explicit heading focus and return position replace the current offscreen appended detail. A route for every local view was rejected because it unnecessarily expands protected routing and state restoration.
2. Own top/side insets once at the root; pages own the bottom inset. Use compact connection summaries, an accessible refresh action and expandable guidance within scrollable content. Normal online state describes network connectivity, not guaranteed API health. Keep keyboard avoidance and test the complete rendered shell.
3. Keep touch targets at least 48dp, text wrapping and system font scaling. Short selection groups wrap; the longer order filter group uses an expanded-state disclosure with the current selection visible. Retain programmatic names or update affected native flows explicitly.
4. Add labelled inputs and section heading hierarchy using the current semantic colors and fonts. Errors sit next to their form and are announced; disabled/loading state is both visual and semantic.
5. A successful write is confirmed independently from later reads. Screen success callbacks can run immediately after the confirmed response, while mutations remain guarded until refresh settles. Report refresh failures separately without erasing success or enabling actions from stale records. An unknown transport outcome never claims that a request was not sent. Keep explicit retries and payment IDs unchanged. Avoid unrelated transport timeouts.
6. Keep light-only configuration and verify under both Android system appearance settings. Compare normal/200% text on small and large native layouts. Component semantics cannot establish native geometry or TalkBack traversal.

## Risks / Trade-offs

- Layout changes affect all routes → retain route/session regression tests and verify complete-shell safe areas, keyboard and enlarged text.
- Disclosure adds a tap to change an order filter → keep the selected filter visible and preserve every filter.
- Focus restoration can race native layout → use layout callbacks and stable host refs; do not steal focus during polling/refetch.
- A write may succeed while refresh fails → keep its confirmation and require authoritative refresh before dependent writes.
- Native tools or services may be unavailable → record exact limitations and leave native acceptance unchecked; never substitute browser screenshots for Android evidence.

## Migration Plan

Commit the audit and plan first, shared controls second, workflow changes third, then regression and native evidence. No data migration is needed. Reverting the feature commits restores the prior UI. Issue #219 remains open until all acceptance evidence and @longtoZ approval are recorded.
