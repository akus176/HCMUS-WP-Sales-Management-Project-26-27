# Staff Mobile App — Counter + Preparation Demonstration Plan

This plan follows the [current scope decision](../mobile-scope-206.md). It supersedes
the earlier six-workspace video plan. It is a plan, not a recorded demonstration or
new acceptance evidence. #206 verifies selected P1 flows before they are claimed;
#95 Mobile 6.6 owns the final video/timestamps/submission work.

## Before recording

1. Record mobile/API commits, device/build versions, seed version, synthetic account permissions and accepted test links.
2. Confirm retained P0/shared-state coverage and #206 acceptance; do not demonstrate unverified selected actions as accepted features.
3. Use a disposable API/database and synthetic customers, addresses, menu, available table, ready delivery and eligible assigned staff. Keep credentials/tokens/connection strings out of the recording.
4. Prepare distinct Counter and Preparation permissions, or a multi-permission account; authorization remains server-side.
5. Map the chosen scenes to F-01–F-04, F-10–F-13 and F-16–F-20 in the function list. Excluded rows are not scenes.

## Proposed continuous sequence

1. Identify the scope/spec, exact build and function list. Sign in and show permitted Today/Counter/Preparation/More navigation; More contains Account only (F-01/F-02).
2. In Counter, search/filter the menu, choose a size/add-on, cancel once without changing the cart, then add items and adjust independent lines (F-18).
3. Show service selection and the dine-in available-table picker. Show optional existing-customer/saved-address selection and explain manual delivery-address editing; no Tables overview or customer administration is advertised (F-12/F-19).
4. Save a disposable draft, show authoritative snapshots/total and review confirmation. If accepted and selected, demonstrate a reviewed discount request or loyalty redemption on a separate eligible draft (F-18/F-20). Discount approval remains a WinUI task, not a mobile inbox.
5. Confirm a reviewed ticket and show the same authoritative order in Preparation and WinUI/shared API. Start preparation, cancel a ready review without a write, then confirm ready (F-03/F-04/F-10).
6. Show order lookup/status and a supported reviewed payment with the server-confirmed paid amount/balance (F-12).
7. With eligible manager permissions and separate seeded records, demonstrate cancellation with a reason and assignment of a ready delivery to eligible staff (F-13). No mobile courier execution is claimed.
8. If current native push/device evidence is accepted, demonstrate a generic urgent Preparation notification, authenticated opening, and permission re-evaluation. Otherwise record the limitation rather than claiming delivery (F-16).
9. Show offline write protection with no automatic replay after reconnection, then Account connection details/sign-out and protected-data cleanup (F-11/F-01).
10. Show accepted revision-bound evidence and honest function-list statuses. Keep release/submission claims separate.

## Evidence needed

| Proof          | Required record                                                                                            |
| -------------- | ---------------------------------------------------------------------------------------------------------- |
| Current scope  | Linked owner decision, merged specification/function list and reviewer decision                            |
| Shared backend | Current Counter/Preparation state visible to a second authorized client; no direct database use by mobile  |
| Safe writes    | Invalid/stale/offline actions preserve server state; explicit retries keep their stable identity           |
| Privacy/access | No credentials or real customer data in video/previews; protected and retired links deny access            |
| Traceability   | Actual commands/results, exact revisions/device/seed and accepted evidence; final timestamps per shown row |

## After recording

Publish the selected final video as Unlisted, review it for secrets, and add its URL
and exact timestamps to the function list. Final submission remains the separate
course/LMS gate. Do not include mobile approvals, inventory, receipts, administration,
courier screens or a standalone Tables view to satisfy historical scope.
