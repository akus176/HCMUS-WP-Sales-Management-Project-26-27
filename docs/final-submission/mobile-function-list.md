# Staff Mobile App — Function List

This is the submission-facing function list for the mobile staff application.
Update the Status, Evidence, and Video fields only after the cited work exists.

| ID | Role | Function | Priority | Status | Required API or shared-state proof | Automated evidence | Video evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-01 | All staff | Sign in, renew session, and sign out securely | P0 | Planned | Authenticated API session and server permission resolution | Session and logout tests | Login and logout segment |
| F-02 | All staff | See only permitted mobile modules and actions | P0 | Planned | Effective permissions from the shared API | Permission-navigation tests | Role home screen |
| F-03 | Barista | View confirmed and preparing tickets | P0 | Planned | Shared preparation-ticket query | Ticket-list component tests | Preparation queue |
| F-04 | Barista | Start preparation and mark a ticket ready | P0 | Planned | Server order state transition and audit actor | Android end-to-end state-transition test | Confirmed to Ready workflow |
| F-05 | Manager | Review and approve or reject a pending discount | P0 | Planned | Shared discount request and order-total update | API and mobile approval tests | Approval workflow |
| F-06 | Inventory staff | View ingredient balances and low-stock items | P0 | Planned | Shared inventory and low-stock queries | Inventory query tests | Low-stock screen |
| F-07 | Inventory staff | Record a stock adjustment with a required reason | P0 | Planned | Server adjustment command and movement history | Valid and missing-reason tests | Adjustment workflow |
| F-08 | Delivery staff | View only assigned deliveries and open device navigation | P0 | Planned | Assignment-scoped delivery query | Assignment isolation tests | Assigned delivery detail |
| F-09 | Delivery staff | Mark delivery as delivered or failed with a reason | P0 | Planned | Server delivery state transition and audit actor | Android delivery end-to-end test | Delivery completion or failure |
| F-10 | All P0 roles | Receive current state after another client changes a record | P0 | Planned | Authenticated event or manual refresh against the shared API | Multi-client synchronization test | Mobile-to-Windows state proof |
| F-11 | All P0 roles | Prevent state-changing actions when offline | P0 | Planned | Connectivity-aware mutation guard | Offline-write prevention test | Offline state and disabled action |
| F-12 | Cashier | Search orders, inspect tables, and record supported payments | P1 | Planned | Shared order, table, and payment endpoints | Payment idempotency test | Optional P1 segment |
| F-13 | Manager | Authorize cancellations and assign ready deliveries | P1 | Planned | Shared cancellation and assignment endpoints | State-rule and audit tests | Optional P1 segment |
| F-14 | Admin | Search accounts, deactivate an eligible account, and view security events | P1 | Planned | Permission-protected account and security endpoints | Final-admin protection tests | Optional P1 segment |
| F-15 | Eligible staff | Receive privacy-safe actionable notifications | P1 | Planned | Device registration and notification provider | Payload privacy and deep-link tests | Optional P1 segment |
| F-16 | Team | Run release hardening, CI, accessibility, and physical-device validation | P2 | Planned | Build pipeline and documented release procedure | CI and accessibility evidence | Optional appendix |

## Status rules

- **Implemented:** The feature has the required API behavior, automated evidence, and current video proof.
- **Planned:** The feature is specified but lacks one or more required proof items.
- **Out of Scope:** The feature is explicitly excluded from the first mobile release.

Do not change a status to Implemented because a design, mockup, issue, or API proposal exists.
