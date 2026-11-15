# Issue #203 inventory event refresh handoff

## Source-level defect

The authenticated SignalR client maps `inventory` events to React Query prefixes that did not match the inventory screen. The screen uses `inventory-balances`, `inventory-movements`, `purchase-receipts`, and `purchase-receipt`, so an event could leave all four views stale until reconnect or manual refresh.

The fix updates only the affected-query allowlist. A regression test checks that all four active query families refetch for the current staff user, while sales and another user's inventory cache remain untouched.

## Android verification delegated to vt-Toan

Run on Android against the same disposable API/PostgreSQL instance used for the matching Windows/API run in #202:

1. Sign in as a synthetic inventory manager and open balances, movement history, receipt list, and a receipt detail.
2. Change stock or post a prepared receipt from the other client/API. Confirm each open affected mobile view refreshes from authoritative server data without manual refresh.
3. Confirm an unrelated sales view and another staff account's cache are not refreshed by the inventory event.
4. Record app/API commits, device and Android version, synthetic roles, seed IDs, event result, and genuine sanitized screenshots or screen recording. Include reconnect/offline and conflict cases required by #203, and link the matching Windows/API evidence from #202.

No device run is claimed in this file. vt-Toan owns the live Android test and should append actual results after running it.
