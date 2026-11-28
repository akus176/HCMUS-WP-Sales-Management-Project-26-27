# Missing features audit — current source reconciliation

Checked against `main` at `82988f4` (2026-11-26). This is a snapshot of merged implementation, not a new feature specification. The [course requirements](https://app.notion.com/p/Windows-project-3e43c0d79f2680a8bba1ed55b35f71a7) require multiple users, roles, and server-side authorization. The active OpenSpec may require more than the course baseline.

## Current decisions

- **Stock room actions are deferred.** Issue [#230](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/230) remains the focused implementation issue; do not mark its actions complete.
- **Source code determines implementation status.** A checked issue box or closed issue is not proof that every operation in a broader OpenSpec requirement exists.
- **Staff lifecycle implementation is proposed in [PR #244](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/244).** It adds update, reactivation, role changes, final-administrator protection, and tests. PR #244 remains open; until it merges, the `main` snapshot below still has those gaps.
- **Historical omissions do not automatically become work.** Add code only when an active requirement or an explicit product decision calls for that behavior.

## Former priority gaps

| Item | Current implementation | Remaining work |
| --- | --- | --- |
| Administrator Google sign-in ([#227](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/227)) | API `/auth/google` verifies the configured identity link; WinUI offers a browser sign-in flow. Local credential fixtures remain supported where Google sign-in is disabled. | Deployment configuration and native acceptance evidence are separate from the presence of code. |
| Staff creation ([#228](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/228)) | `POST /users` and the WinUI Accounts form create a staff account with one selected role and an initial password. API search, detail, and eligible deactivation also exist. | On the audited `main` snapshot, account update, reactivation, and post-creation role changes are absent. The active [identity spec](../openspec/changes/build-coffee-shop-management-system/specs/identity-and-access/spec.md) requires them; their implementation and tests are in open PR #244. Do not present #228's creation scope as the whole of 10.6. |
| Dining tables ([#229](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/229), [#231](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/231)) | API and WinUI support table creation, management listing, editing, and deletion with state rules. A dedicated Tables page shows availability. | The old claim that tables require SQL seed edits is false. |
| Stock room ([#230](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/230)) | API and desktop adapter support threshold changes and ingredient/supplier activation. Supplier editing works. | The Stock room page never calls those five adapter methods; its ingredient dialog edit mode has no caller. Deferred until requested. |

## Account administration tracker reconciliation

On the audited `main` snapshot, the API and WinUI support staff **create, list, view, and deactivate**, but not **update, reactivate, or post-creation role assignment**. The active [identity spec](../openspec/changes/build-coffee-shop-management-system/specs/identity-and-access/spec.md) requires all of these. The main-branch [task tracker](../openspec/changes/build-coffee-shop-management-system/tasks.md) leaves 10.6 unchecked. PR #244 implements the full lifecycle and checks 10.6 after its PostgreSQL API tests and Windows E2E checks passed; it remains open and has not changed the audited main snapshot yet. The final-active-administrator deactivation guard already existed at that snapshot, but did not complete the other account operations.

## Historical inventory — absence does not establish priority

| Capability absent or limited | Current assessment |
| --- | --- |
| Payment refunds or voids | The API records payments but has no refund/void workflow. The loyalty spec describes reversal **if** a refund invalidates points; it does not by itself define a refund feature. No dedicated refund issue was found. |
| Reservations | Domain and persistence models exist, but no API or client workflow. No current reservation-delivery requirement or dedicated issue was found. |
| Export from Reports | The UI has no export action. [#117](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/117) treats unsupported export controls as concept-only, so export is not an approved gap to implement. |
| Dynamic role/permission bundle management | Roles are seeded; the course baseline requires role-based access, which the API enforces. This is separate from the unmet staff role-reassignment requirement above. |
| Catalog/customer/address reactivation; ingredient name/unit edits; supplier get-by-ID; purchase-receipt header edits or draft deletion; administrator device listing/revocation; manual loyalty adjustments | These operations are not exposed by the current API/UI. Their absence alone does not justify adding them to this delivery. Preserve historical references and stock/loyalty rules if any are later scoped. |
| Partial recipe editing | The API replaces a variant recipe as a whole. This is a supported edit operation, not a missing feature by itself. |
| Mobile operations | Counter order creation/confirmation, discount requests, loyalty redemption, existing-customer lookup, and Preparation start/ready actions are implemented. The current [mobile task tracker](../openspec/changes/add-staff-mobile-app/tasks.md) retires broader inventory, courier, administration, and receipt screens; lack of those screens is not a current implementation gap. |
| Hard deletion | The old blanket claim is false: table deletion and draft receipt-line deletion exist. Other historical records are intentionally preserved. |

This file records source-level findings. It does not claim a passing test suite, native UI acceptance, or a completed end-to-end demo.
