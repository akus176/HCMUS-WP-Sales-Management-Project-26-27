# Staff mobile usability audit (#219)

OpenSpec change: `polish-staff-mobile-ux`. Baseline source: `git rev-parse HEAD`
before the first implementation commit. This is a fresh source audit on 2026-11-19;
historical screenshots are identified separately and are not a new native run.

## Screen and role inventory

Navigation is permission-based, including users with several roles. Each entry
requires loading, empty where applicable, error/retry, forbidden/revoked access,
offline/read-only, busy/disabled and server-confirmed outcome checks.

| Entry / view | Role or permission | Route / local path |
| --- | --- | --- |
| Configuration error | All | Root configuration gate |
| Font loading / restoring session | All | Root layout |
| Welcome | Signed out | `/` |
| Sign-in / invalid or expired session | All | `/login` |
| Staff home / no permitted modules / access check failure | All authenticated roles | `/session` |
| Preparation queue / status filters / pages | `orders.fulfill` | `/modules/barista` |
| Preparation detail / start / ready / confirmation | `orders.fulfill` | Queue selection or urgent-order notification |
| Approval inbox | `discounts.approve` | `/modules/approvals` |
| Discount detail / approve / reject / no longer pending | `discounts.approve` | `/modules/approvals/[discountId]` or approval notification |
| Manager search / cancellation review / confirmation | `orders.create`, `orders.cancel` | `/modules/approvals/operations` |
| Ready delivery / staff selection / assignment review | `orders.create`, `delivery.assign` | Manager operations |
| Counter order search / statuses / table occupancy | `orders.create` | `/modules/cashier` |
| Order detail / lines / fulfillment / balance | `orders.create` | Order or occupied-table selection |
| Payment entry / review / confirmed / uncertain retry | `payments.record` | Counter order detail |
| Ingredient balances / search | `inventory.manage` | `/modules/inventory` |
| Low stock | `reports.lowstock` | Inventory section |
| Adjustment entry / validation / saved balance | `inventory.adjust` | Ingredient card |
| Prepared receipts / detail / posting confirmation | `inventory.receive` | Inventory receipts section |
| Movement history / ingredient history | `inventory.manage` | Inventory history section or ingredient card |
| Active/completed delivery lists / pagination | `delivery.update` | `/modules/delivery` |
| Delivery contact/address / navigation / transitions / failure reason | `delivery.update`, own assignment | `/modules/delivery/[orderId]` or assignment notification |
| Account search / active filter / deactivation | `users.manage` | `/modules/administration` |
| Account detail / close | `users.manage` | Account selection |
| Security events / filters | `users.manage` | Administration section |
| Low-stock record / missing record | `reports.lowstock` | `/notifications/low-stock/[recordId]` |
| Invalid, stale and unauthorized notification links | Per target permission | `/notifications/[kind]/[recordId]` |

Role paths: Barista → Preparation; Cashier → Counter; InventoryStaff → Inventory;
DeliveryStaff → Deliveries; Manager → allowed operational modules and Approvals;
Admin → Administration plus any effective operational permissions. Never infer
permissions from role names. Background/resume, token renewal, logout, stale
route history and changed grants also belong to the matrix.

## Reproducible findings

Priorities: high = delays core work or misstates a write; medium = navigation,
consistency or feedback friction. Source-confirmed behavior is separate from
native geometry/focus verification.

| ID | Severity | Screen / reproduction | Impact / evidence |
| --- | --- | --- | --- |
| UX01 | High | Open Preparation at 200% text on the recorded 360×800dp profile | Persistent online paragraph + Refresh, home button, instructions and filters precede tickets. Historical [baseline](../mobile-quality-186/phone-preparation-queue.png); current banner/page code has the same structure. |
| UX02 | High | Counter → search with several orders, inspect status choices | Seven full-width status buttons precede results (eight in manager view); source-confirmed. |
| UX03 | Medium | Administration with multiple results → open the first account | Detail is appended after all accounts; no explicit scroll/focus transition; source-confirmed. |
| UX04 | High | Attempt to deactivate the final active administrator | Failure is stored in the same string as success and rendered as informational feedback; source-confirmed. |
| UX05 | Medium | Compare sign-in with inventory and payment forms | Login duplicates theme/control styles; several fields lack persistent visible labels and section headings share page display sizing. |
| UX06 | High | Complete a write while a subsequent active query remains unresolved | Shared mutation helper awaits every active refresh before the screen success callback; source-confirmed, needs deferred-read regression. |
| UX07 | High | Lose the response to a discount decision after submission | Network-error copy says the decision was not sent, although the outcome is unknown; source-confirmed. |
| UX08 | Medium | Inspect root banner and ShellPage safe-area consumers | Both apply top insets; needs native geometry check after single ownership is implemented. |

## Verification matrix and baseline limits

- Native layouts: small phone (360×800dp), large tablet (800×1280dp); normal and
  200% text; Android light and dark system settings with app light-only behavior.
- Exercise every inventory row above, keyboard-visible forms, all primary actions,
  selected/expanded states, TalkBack entry/return focus, long names/addresses,
  safe areas, 48dp targets and spacing. Confirm contrast using shared palette tests.
- Preserve API confirmation, permission revalidation, payment IDs, explicit retry,
  no offline queue/replay, and form retention through routine token renewal.
- Before/after captures must identify revision, device, text scale and appearance.
  Existing #140/#186/#198/#201/#203 evidence is historical supporting context.
- At setup, `adb devices -l` returned no connected device. Docker's Linux daemon
  was unavailable and no API/Metro listener was present. Native results are not
  established by component tests or Android bundle export.

## Implementation and results

Implementation, exact automated commands/results, native evidence and remaining
issues will be recorded here as they become available. Reviewer: @longtoZ;
approval is outstanding. The issue remains open until all acceptance evidence
and approval exist.
