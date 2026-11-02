# Staff Mobile App — Function List

This is the submission-facing function list for the mobile staff application.
Update the Status, Automated evidence, and Video timestamp fields only after the cited work exists.

| ID | Role | Function | Priority | Status | GitHub work item | Required API or shared-state proof | Automated evidence | Video timestamp/segment |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F-01 | All staff | Sign in, renew session, and sign out securely | P0 | Planned | #95, 2.1, 3.3–3.4 | Authenticated API session and server permission resolution | Session and logout tests | Record after final video |
| F-02 | All staff | See only permitted mobile modules and actions | P0 | Planned | #95, 4.1–4.2 | Effective permissions from the shared API | Permission-navigation tests | Record after final video |
| F-03 | Barista | View confirmed and preparing tickets | P0 | Planned | #95, 4.2 | Shared preparation-ticket query | Ticket-list component tests | Record after final video |
| F-04 | Barista | Start preparation and mark a ticket ready | P0 | Planned | #95, 4.2 | Server order state transition and audit actor | Android end-to-end state-transition test | Record after final video |
| F-05 | Manager | Review and approve or reject a pending discount | P0 | Planned | #95, 4.3 | Shared discount request and order-total update | API and mobile approval tests | Record after final video |
| F-06 | Inventory staff | View ingredient balances and low-stock items | P0 | Planned | #95, 4.4 | Shared inventory and low-stock queries | Inventory query tests | Record after final video |
| F-07 | Inventory staff | Record a stock adjustment with a required reason | P0 | Planned | #95, 4.4 | Server adjustment command and movement history | Valid and missing-reason tests | Record after final video |
| F-08 | Delivery staff | View only assigned deliveries and open device navigation | P0 | Planned | #95, 4.5 | Assignment-scoped delivery query | Assignment isolation tests | Record after final video |
| F-09 | Delivery staff | Mark an assignment out for delivery, delivered, or failed with a reason | P0 | Planned | #95, 4.5 | Server delivery state transition and audit actor | Android delivery end-to-end test | Record after final video |
| F-10 | All P0 roles | Receive current state after another client changes a record | P0 | Planned | #95, 4.7 | Authenticated event or manual refresh against the shared API | Multi-client synchronization test | Record after final video |
| F-11 | All P0 roles | Prevent state-changing actions when offline | P0 | Planned | #95, 4.6 | Connectivity-aware mutation guard | Offline-write prevention test | Record after final video |
| F-12 | Cashier | Search orders, inspect tables, and record supported payments | P1 | Planned | #95, 5.1 | Shared order, table, and payment endpoints | Payment idempotency test | Record only if implemented |
| F-13 | Manager | Authorize cancellations and assign ready deliveries | P1 | Planned | #95, 5.2 | Shared cancellation and assignment endpoints | State-rule and audit tests | Record only if implemented |
| F-14 | Inventory staff | Review and post a prepared purchase receipt | P1 | Planned | #95, 5.6 | Shared receipt command and movement history | Retry-safe receipt-posting test | Record only if implemented |
| F-15 | Admin | Search accounts, deactivate an eligible account, and view security events | P1 | Planned | #95, 5.5 | Permission-protected account and security endpoints | Final-admin protection tests | Record only if implemented |
| F-16 | Eligible staff | Receive privacy-safe actionable notifications | P1 | Planned | #95, 5.3–5.4 | Device registration and notification provider | Payload privacy and deep-link tests | Record only if implemented |
| F-17 | Team | Run release hardening, CI, accessibility, and physical-device validation | P2 | Planned | #95, 6.1–6.6 | Build pipeline and documented release procedure | CI and accessibility evidence | Optional appendix |

## Status rules

- **Implemented:** The feature has the required API behavior, automated evidence, linked GitHub work item, and exact current-video timestamp.
- **Planned:** The feature is specified but lacks one or more required proof items.
- **Out of Scope:** The feature is explicitly excluded from the first mobile release.

Do not change a status to Implemented because a design, mockup, issue, or API proposal exists.
