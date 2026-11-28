# Counter + Preparation mobile scope decision (#206)

Effective 26 November 2026, the owner selected the existing **Counter + Preparation**
design as the mobile product scope. This decision supersedes the earlier six-workspace
mobile target. It changes mobile scope only; shared API permissions, business rules,
WinUI workflows and historical evidence remain intact. Reviewer acceptance of this
scope and current Android evidence remains pending under #206 (@longtoZ).

## Selected workflows and owners

| Task                    | Current mobile requirement                                                                                                                                                                                                                                                              | Verification owner / evidence                                                                                                                 |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Mobile 4.1–4.2, 4.6–4.7 | Permission-derived Today, Counter, Preparation, More; More contains Account. Preparation queue/detail, start/ready confirmation, offline protection, refresh and shared-state updates.                                                                                                  | @vt-Toan; #201/#202/#203 and #221 are revision-bound prior evidence. Confirm coverage of the selected build before using the P0 prerequisite. |
| Mobile 5.1              | Counter menu/search, size/add-on customization, independent cart lines, takeaway/dine-in/delivery service modes, available-table picker, optional existing customer/saved-address lookup, save/review/confirm, discount requests, loyalty redemption, order search/status and payments. | @vt-Toan; #221 implementation; #232 operation mapping; current Android acceptance remains open in #206.                                       |
| Mobile 5.2              | Permission-gated cancellation with a reason and eligible-staff assignment of ready delivery orders, within Counter order details.                                                                                                                                                       | @vt-Toan; #182 prior evidence, #221 current navigation; verify selected build in #206.                                                        |
| Mobile 5.3              | Device registration, token rotation/ownership, invalid-token cleanup and logout removal; notification permission denial must not prevent normal use.                                                                                                                                    | @vt-Toan coordinates #181 evidence and current device/API verification in #206.                                                               |
| Mobile 5.4              | Privacy-safe urgent Preparation notifications with authentication and fresh permission checks. Unsupported or retired notification kinds show an unavailable result.                                                                                                                    | @vt-Toan; #181/#221 and notification tests are prior evidence; current Android opening/privacy proof remains open in #206.                    |

Delivery is a Counter service mode. Assigning a ready delivery is a Counter manager
action. Neither requires a mobile courier workspace. A discount request is not a
manager approval: approval/rejection is performed in WinUI, and mobile refreshes the
server's decision before confirming the ticket.

Existing customer selection, saved-address selection and loyalty redemption are in
scope. Customer creation/editing/deactivation, address management and loyalty-settings
administration remain in WinUI. Drafts and uncertain requests are session-scoped; this
scope does not promise durable offline carts or automatic write replay.

## Explicit exclusions

| Previous task or feature                                                           | Current disposition                                                                                     |
| ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Mobile 4.3: mobile discount approval/rejection inbox                               | Out of Scope; WinUI retains the workflow. Discount requests remain in Counter.                          |
| Mobile 4.4: stock balances, low stock, adjustment and movement screens             | Out of Scope; WinUI retains the workflow. Counter confirmation still uses server stock rules.           |
| Mobile 4.5: courier assignments, navigation and delivery outcomes                  | Out of Scope; WinUI/shared API retain the workflow. Retired mobile links must not expose customer data. |
| Mobile 5.5: staff account administration and security-event screens                | Out of Scope; Account contains the signed-in user's session, connection, device sign-out and account-wide sign-out controls only. |
| Mobile 5.6: purchase-receipt review/posting                                        | Out of Scope; WinUI retains receipt and stock movement workflows.                                       |
| Standalone Tables tab / whole-shop occupancy overview                              | Out of Scope; dine-in selects from available tables in the ticket form.                                 |
| Approval, low-stock or courier notification destinations                           | Out of Scope; only urgent Preparation destinations are selected for mobile.                             |
| Full customer/address administration, menu editing, reporting and loyalty settings | Out of Scope; existing-customer selection in Counter does not include these screens.                    |

Retired task IDs remain reserved for historical traceability. Their old checked boxes,
reports and screenshots do not establish completion of a current release requirement.
Do not delete or relabel historical captures as new evidence.

## #206 acceptance boundary

#206 selects Mobile 5.1–5.4 only. It must map the current build/API/device/seed/accounts,
actual commands and genuine Android results for those workflows. Counter additions
require create/confirm/customer/discount/loyalty review, validation, conflict and retry
coverage; a successful component or CI run does not substitute for shared-API Android
acceptance. An existing workflow with no reproduced gap should reuse matching evidence
rather than receive duplicate implementation.

The historical P0/shared-state approvals in #198/#201/#202/#203 remain valid for their
recorded revisions. Check that retained Preparation, session, permission, online-only
and shared-state behavior is covered for the selected build; link additional baseline
work to the existing P0/shared-state issues rather than expanding #206 into a P0 rerun.
The scope decision alone cannot make a missing baseline green.

Before #206 closes:

- Merge the scope/spec/function-list/contract-description reconciliation and link it to #95/#206/#232.
- Confirm the retained P0/shared-state prerequisite against revision-bound evidence; record blockers honestly.
- Obtain actual Android/shared-API results for Mobile 5.1–5.4, including device lifecycle and the urgent Preparation link.
- Verify permissions, required reasons, cancellation without writes, authoritative totals/balances, conflicts, stable retry identities and no offline replay.
- Record excluded workflows and unresolved defects explicitly; keep incomplete selected criteria unchecked.
- Obtain @longtoZ's explicit approval. Final video, release and Moodle submission remain separate gates.

No new local tests, Android run, push delivery, reviewer approval or final-demo completion
is claimed by this documentation change. CI validates code/contracts; native acceptance
remains an explicit follow-up under #206.
