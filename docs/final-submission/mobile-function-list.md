# Staff Mobile App — Function List

The [26 November 2026 scope decision](../mobile-scope-206.md) selects Counter +
Preparation. **Planned** below means selected but not yet fully accepted under the
status rules; implementation may already exist. **Out of Scope** identifies retired
mobile surfaces, not removed WinUI/shared API functionality. Reports remain tied to
their recorded revisions. #206 covers current selected P1 acceptance, not final video.

| ID   | Role / permission                     | Function                                                                      | Priority | Status       | Task / evidence                                                                               | Video timestamp/segment                |
| ---- | ------------------------------------- | ----------------------------------------------------------------------------- | -------- | ------------ | --------------------------------------------------------------------------------------------- | -------------------------------------- |
| F-01 | All staff                             | Secure sign-in, session renewal, logout                                       | P0       | Planned      | #95 3.3–3.4; session/logout tests                                                             | Record after final video               |
| F-02 | Effective permissions                 | Today, Counter, Preparation, More; Account only in More                       | P0       | Planned      | #95 4.1; #221 navigation/access tests                                                         | Record after final video               |
| F-03 | `orders.fulfill`                      | Confirmed/preparing queue and ticket detail                                   | P0       | Planned      | #95 4.2; [#201 prior Android evidence](../evidence/mobile-workflows-201/README.md)            | Record after final video               |
| F-04 | `orders.fulfill`                      | Start preparation and mark ready after review                                 | P0       | Planned      | #95 4.2; #201 prior state/API evidence; current baseline coverage to confirm                  | Record after final video               |
| F-05 | Managers                              | Mobile discount approval/rejection inbox                                      | —        | Out of Scope | Retired Mobile 4.3; decisions remain in WinUI                                                 | Excluded                               |
| F-06 | Inventory staff                       | Mobile ingredient balances and low-stock list                                 | —        | Out of Scope | Retired Mobile 4.4; WinUI remains available                                                   | Excluded                               |
| F-07 | Inventory staff                       | Mobile stock adjustment/movement views                                        | —        | Out of Scope | Retired Mobile 4.4; #198 reports are historical                                               | Excluded                               |
| F-08 | Delivery staff                        | Mobile assigned deliveries/device navigation                                  | —        | Out of Scope | Retired Mobile 4.5; retired routes deny access                                                | Excluded                               |
| F-09 | Delivery staff                        | Mobile delivery start/delivered/failed outcomes                               | —        | Out of Scope | Retired Mobile 4.5; Counter assignment remains F-13                                           | Excluded                               |
| F-10 | Selected staff                        | Shared-state/event/manual refresh in Counter/Preparation                      | P0       | Planned      | #95 4.7/6.4; #202/#203 revision-bound evidence                                                | Record after final video               |
| F-11 | Selected staff                        | Block offline writes; preserve explicit retry safety                          | P0       | Planned      | #95 4.6; connectivity and Counter tests                                                       | Record after final video               |
| F-12 | `orders.create`, `payments.record`    | Order search/status, dine-in available-table selection and supported payments | P1       | Planned      | #95 5.1; #206; [#182 prior evidence](../evidence/mobile-order-operations-182/README.md); #221 | Record after final video               |
| F-13 | `orders.cancel`, `delivery.assign`    | Counter cancellation with reason and ready-delivery assignment                | P1       | Planned      | #95 5.2; #206; #182 prior evidence; #221 current Counter                                      | Record after final video               |
| F-14 | Inventory staff                       | Mobile prepared receipt review/posting                                        | —        | Out of Scope | Retired Mobile 5.6; WinUI retains receipts                                                    | Excluded                               |
| F-15 | Administrators                        | Mobile account search/deactivation/security events                            | —        | Out of Scope | Retired Mobile 5.5; personal Account remains available                                        | Excluded                               |
| F-16 | `orders.fulfill` / device owner       | Device lifecycle and privacy-safe urgent Preparation notifications/deep links | P1       | Planned      | #95 5.3–5.4; #206; #181 implementation and revision-bound evidence                            | Record only after current verification |
| F-17 | Team                                  | Android, accessibility, CI and release evidence                               | P2       | Planned      | #95 6.1–6.6; #206 is not final release acceptance                                             | Optional appendix                      |
| F-18 | `orders.create`, `orders.confirm`     | Counter menu/customization/cart, service modes, draft save/review/confirm     | P1       | Planned      | #95 5.1; #206; #221; [Counter workflow](../mobile-counter-workflow.md)                        | Record after current acceptance        |
| F-19 | `customers.manage`                    | Select an existing customer and saved address; edit delivery details          | P1       | Planned      | #95 5.1; #206; #221; [operation map](../evidence/issue-232-mobile-sales/README.md)            | Record after current acceptance        |
| F-20 | `discounts.request`, `loyalty.redeem` | Review/submit discount requests and loyalty redemption on a saved draft       | P1       | Planned      | #95 5.1; #206; #221; #232 mapping                                                             | Record after current acceptance        |
| F-21 | Counter staff                         | Standalone Tables / full occupancy overview                                   | —        | Out of Scope | Available-table selection remains in the dine-in ticket form                                  | Excluded                               |
| F-22 | Managers / inventory / couriers       | Approval, low-stock and courier notification destinations                     | —        | Out of Scope | Urgent Preparation destination remains F-16                                                   | Excluded                               |
| F-23 | Customer administrators               | Customer/address CRUD, loyalty history/settings administration                | —        | Out of Scope | Existing-customer/address selection remains F-19; WinUI retains administration                | Excluded                               |

## Status rules

- **Implemented:** Within current scope, required API behavior and accepted current-build evidence are linked, and the exact final-video timestamp exists.
- **Planned:** Within current scope but one or more required acceptance/evidence/video items remain incomplete; this does not imply absent code.
- **Out of Scope:** Explicitly excluded by the current mobile scope decision. Retired evidence is historical only.

Do not mark a capability Implemented because its API exists, a component test passes,
a historic task was checked, a scope change was requested, or #232 was closed. The new
scope does not claim @longtoZ approval, Android acceptance, release or Moodle completion.
