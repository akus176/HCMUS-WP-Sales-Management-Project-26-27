# Staff mobile usability audit (#219)

OpenSpec change: `polish-staff-mobile-ux`. Baseline source:
`5c9ed019398099cc59e3a007b6cf82a71208d25f`. This is a fresh source audit on 2026-11-19;
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
| UX09 | Medium | Fail the home permission check or launch with invalid configuration at enlarged text | Home permission feedback had no scroll container; configuration feedback used default platform colors. Both now use scrollable, themed fallback layouts. |

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

Shared-shell step (`03b3f7e`): TypeScript and ESLint pass; seven focused suites
pass 34/34 cases. Workflow step: Counter's status disclosure retains all filters,
account details replace the list with an explicit return path, Accounts and
Security events have separate selected views, and forms share visible labels.
Six affected workflow/accessibility suites passed 75/75 cases before the added
deactivation-error regression. Native selectors were updated for the account
detail return and security-event section.

Reviewer: @longtoZ; approval is outstanding. The issue remains open until all
acceptance evidence and approval exist. Native verification was explicitly left
pending at the user's request on 2026-11-19; see the remaining work below.

## Automated verification (current implementation)

Toolchain: Node 26.5.0, npm 11.17.0; downloaded Node executable checked against
the official distribution SHA-256 manifest. Existing dependency pins were retained.

| Command (repository root) | Result |
| --- | --- |
| `npm run check --prefix apps/staff-mobile` | PASS: TypeScript, ESLint (zero warnings), Prettier, 28 suites / 328 tests, 4 inventory and 3 workflow harness checks; no skipped tests |
| `npm run check --prefix contracts/mobile-p0` | PASS: deterministic OpenAPI/TypeScript regeneration, zero drift, contract TypeScript checks |
| `npm run smoke:android --prefix apps/staff-mobile` | PASS: 1362 modules, Android Hermes bundle and both font assets exported |
| `dotnet build src/CoffeeShop.Api -c Release --nologo` | PASS: zero warnings/errors; used only to prepare the disposable native-audit API |

The accessibility suite covers 26 scenes across four dimension/font-scale profiles
(104 cases): the original ten P0 scenes plus Counter search/filter/payment/review,
manager cancellation/assignment review, low stock, receipts/detail, movement
history, accounts/detail/security events, low-stock notifications and forbidden
access. It includes the real shared connection presentation. These assertions
check labels, selected states, target styles, scalable text and scroll containers;
they do not measure native geometry, keyboard reachability or TalkBack behavior.

Confirmed-write feedback now appears before post-write reads settle. While those
reads are pending, all mutation controls share a guard. Failed invalidated reads
keep writes guarded across mutation components until authoritative refresh or
session-cache cleanup; manual refresh also retries the retained failed queries.
The original server confirmation is retained. Unknown discount response copy no
longer claims the command was not sent. No transport retry or timeout policy was
changed. Deferred-read success/failure and sibling-mutation tests, unknown discount
outcome, uncertain-payment and token-renewal regressions pass: 46/46 cases in four
focused suites, with TypeScript and ESLint passing.

Home access loading/error states now remain inside the same scrollable page as
home content, without nesting a second page. The configuration fallback has its
own safe area, scroll container and explicit paper/error colors before fonts and
session providers initialize. Regression checks preserve both recovery actions,
configuration privacy and existing session/navigation behavior.

## Guidance applied

The [UI/UX Pro Max React Native guidance](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill/blob/main/src/ui-ux-pro-max/data/stacks/react-native.csv)
was consulted on 2026-11-19 alongside the issue's Android-specific 48dp requirement.
The implementation uses Pressable, explicit selected/expanded/disabled semantics,
stable record keys, scalable wrapping text, safe-area ownership and keyboard
avoidance. Framework details were checked against [React Native accessibility](https://reactnative.dev/docs/accessibilityinfo)
and [safe-area edges](https://rnsac.appandflow.com/docs/api/safe-area-view).
No library upgrade, font replacement or new native dependency was introduced.

## Native attempt and remaining acceptance

The task API was built and run on loopback port 52319 against the dedicated
`mobile-ux-219-postgres` container (PostgreSQL 18, loopback 55419), with seven
synthetic role accounts and fresh barista/order/discount/inventory/receipt/delivery
fixtures seeded through existing scripts. API readiness returned 200. This is
environment preparation, not proof of Android workflow completion.

The existing SDK drive lacked installation space. A separate temporary SDK and
API 36 x86_64 Google APIs image were installed; emulator data moved to a dedicated
task directory on D:. WHPX acceleration was available. Headless software/host
graphics attempts reached ADB intermittently, but did not yield a completed,
usable boot or a successful app installation. No fresh native screenshot, native
JUnit pass, TalkBack result or native before/after comparison is claimed.
Historical screenshots remain identified as historical. The user selected
**Leave native verification pending**. Task-owned emulator, Metro, API and database
processes were stopped afterwards; no existing shop database was used.

To finish acceptance with a working emulator or connected Android device:

1. Follow [the contributor runbook](../../mobile-quality-and-release.md) using
   a fresh disposable fixture. Match the native dependency versions to the app.
2. Capture baseline JavaScript from `5c9ed019398099cc59e3a007b6cf82a71208d25f`
   and the implemented revision under identical data/layout conditions. Prioritize
   Preparation, Counter filters, sign-in and account list/detail/error feedback.
3. Run the affected Maestro workflows, including the updated administration
   flow; retain sanitized JUnit and independent API outcome verification.
4. Exercise every inventory entry on 360×800dp and 800×1280dp layouts at 100% and
   200% text under light/dark Android system appearance. The app remains light-only.
5. Verify TalkBack labels, selection/expansion, detail-entry and return focus;
   keyboard-visible payment/cancellation/adjustment/failure forms; measured targets,
   spacing and safe areas. Native behavior must not be inferred from Jest styles.
6. Record the device, source revisions, exact commands, before/after images and
   any remaining issues, then obtain @longtoZ's review decision.

Long account/event/order lists still use scrollable mapped content. Virtualization
was not introduced without a measured runtime performance problem; no list
performance claim is made. Dark-palette work is intentionally outside this change.
The native checks and named approval remain unchecked in canonical issue #219.
