# Design

## Context

See proposal.md for motivation. The existing Expo Router shell has permission checks on route focus, online-only mutations, idempotent payment retry, scalable shared controls and a light paper palette. PR #221 already implements the audit's compact connection presentation, shared forms, safe-area ownership, Counter filter disclosure, account detail navigation, and accurate mutation feedback. Those are the completed baseline, not new duplicate subtasks.

The 2026-11-22 [mobile concept pack](../../../docs/ui-concepts/mobile-2026-11-22/README.md) supplies the remaining visual target. Use the Queue ledger for Preparation and Ticket focus for detail/ready confirmation. The Paper ticket stack remains an alternative exploration; do not implement both queue presentations or a mode switch. Other mobile workflows retain the completed shared polish and regression coverage, without inventing unillustrated concept screens.

## Approved revision (2026-11-23)

The editable mockup at `docs/ui-concepts/mobile-native-reorganization` is now the visual source of truth. The user explicitly requested implementation. Its Inter typography, original vector icons, white cards, 20dp page padding, 12dp corners and native tab navigation supersede conflicting visual rules in the earlier concept contract below. Retain those earlier sections as historical design rationale only where they remain compatible.

Use Inter Regular/Medium/SemiBold, background #faf8f4, white surfaces, ink #302820, cocoa #6d4936, border #e6e0d6 and selection #eee8df. Metadata is 13sp, body 16sp and headings 24sp. Keep text scaling and 48dp controls. Use the original mockup paths through Expo-compatible react-native-svg.

Derive tabs from effective permissions rather than role names. Broad access receives Today, Orders/another primary workspace, Stock/another permitted primary workspace and More. Narrow access receives Home, its permitted workspaces and Account. Cache mounted workspace state with React Activity, hide inactive content from accessibility, recheck permissions before revealing it and discard revoked trees. Normalize legacy module links only after their permission gate passes. Account owns connection details and confirmed sign-out. Normal online status does not occupy a masthead; degraded connection/live-update notices remain in scrollable page content.

Open records directly without mutations. Preserve bounded order results, complete search, filters, paging and detail-return state. Use keyboard-aware, safe-area bottom sheets for payment entry, stock adjustment, receipt posting, preparation and business confirmations. Canceled reviews send no business writes. Retry IDs, API validation and online guards remain unchanged.

## Goals / Non-Goals

**Goals:** align the preparation queue/detail with the selected planning references; expose working content sooner; preserve enlarged text and 48dp controls; make detail transitions and form feedback predictable; distinguish confirmed writes from refresh and transport uncertainty; verify the changed native experience with Maestro and matched-state captures.

**Non-Goals:** new business actions, role changes, the desktop serif/gold theme, an additional paper-stack queue mode, a dark palette, new E2E frameworks, generic transport retries or a production deployment. Per-item checkboxes/completion, swipe-only actions, drag-and-drop priority, undo, timers and analytics are not implied by the art.

## Decisions

1. Keep the existing routes and permission boundaries. Add reusable page/header/input/selection components. Local account details replace the list while keeping search and filter state mounted. Explicit heading focus and return position replace the current offscreen appended detail. A route for every local view was rejected because it unnecessarily expands protected routing and state restoration.
2. Own top/side insets once at the root; pages own the bottom inset. Use compact connection summaries, an accessible refresh action and expandable guidance within scrollable content. Normal online state describes network connectivity, not guaranteed API health. Keep keyboard avoidance and test the complete rendered shell.
3. Keep touch targets at least 48dp, text wrapping and system font scaling. Short selection groups wrap; the longer order filter group uses an expanded-state disclosure with the current selection visible. Retain programmatic names or update affected native flows explicitly.
4. Add labelled inputs and section heading hierarchy using the current semantic colors and fonts. Errors sit next to their form and are announced; disabled/loading state is both visual and semantic.
5. A successful write is confirmed independently from later reads. Screen success callbacks can run immediately after the confirmed response, while mutations remain guarded until refresh settles. Report refresh failures separately without erasing success or enabling actions from stale records. An unknown transport outcome never claims that a request was not sent. Keep explicit retries and payment IDs unchanged. Avoid unrelated transport timeouts.
6. Keep light-only configuration and verify under both Android system appearance settings. Compare normal/200% text on small and large native layouts. Component semantics cannot establish native geometry or TalkBack traversal.

### Concept contract

Authoritative references are `docs/ui-concepts/mobile-2026-11-22/01-queue-ledger.png`, `03-ticket-focus.png`, and the measurable rules below. The concept README records research, scope and provenance; `prompts.md` records the exact generation inputs. Images are 852×1846 raster pixels depicting a 390×844 logical-pixel content viewport. They are visual targets, not screenshots of working code. Native OS chrome is excluded from the content comparison; never stretch a screenshot to match the reference.

Keep existing `mobileTokens`: paper `#fff9ed`, pencil `#302820`, cocoa `#6d4936`, informational blue `#154d70`/`#e8f0f5`, butter highlight `#f5e6b4`, and correction red `#aa2c2c`. Use the bundled Kalam display and Patrick Hand body fonts, 18sp body and at least 14sp metadata at normal scale, 24dp page padding, the existing 4/8/16/24/32 spacing scale, 8dp control corners, at least 48×48dp action targets and 56dp primary actions. Status must have text as well as color. Use conventional labeled icons and minimal decoration; do not add stacked cards, paper tape or shadows to the ledger layout. Measurable accessibility/token rules govern generated-image ambiguities.

The queue has a compact Corner Cafe/header/connection area, a labeled Staff home return, Preparation heading, All active/Confirmed/Preparing controls, and open ruled ticket rows. Rows show live order number, textual status, item count, table/channel context and a short item preview from existing ticket lines. Selecting a row changes the selection indicator and the single `Open <order number>` primary action; selection does not mutate order state. Preserve a visible selected state, a disabled open action when nothing valid is selected, and clear selection if a refresh/filter/page removes the ticket. Preserve existing pagination as scrollable supporting controls even though the art shows only one page. Filter controls wrap at large text scale; the three controls need not remain on one line.

Detail has a labeled Preparation return, refresh, order identifier/state, table/item summary, and open item rows showing quantity, item name, size and add-ons. Highlight important add-ons without replacing their text. Confirmed tickets still offer Start preparing followed by Confirm start. Preparing tickets offer Mark ready followed by an inline confirmation area: `Mark this ticket ready?`, `This updates the whole ticket.`, `Confirm ready`, and `Keep preparing`. The last action dismisses confirmation without changing server state. Preserve list/filter/page/selection on return when the selected record remains valid. The ready-confirmation image is one state of the workflow, not an instruction to show confirmation permanently.

Reference ticket IDs, item names and counts are synthetic examples. Use equivalent seeded records for comparison, never hard-coded production data. No backend or domain change is needed. A successful command remains distinct from subsequent refresh, stale/forbidden records cannot be acted on, and offline/uncertain outcomes must never be queued or replayed.

### Usability correction after the running-app review

The user's review of the running app rejected the remaining heavy control treatment and friction outside the two reference screens. Apply the same paper/ink hierarchy to entry, staff home and inventory: open the working sign-in form directly at `/`, keep the existing `/login` route, use permission-derived ruled workspace rows with their existing accessible navigation names, and present ingredient records as open rows with concise visible action labels and full record-specific accessible labels. Use one compact branded connection header throughout the app, plain labeled return controls, and thin control outlines without changing geometry on press. Stack brand/status and filter choices where enlarged text needs more room.

At normal phone sizes, queue Open/Refresh and ticket state/confirmation controls belong in a bottom action region outside the scrolling records. This is an in-page region, not a modal overlay; it must reserve its own space. At more than 150% text or a viewport shorter than 560dp, actions reflow into the scrollable page so enlarged controls cannot cover the content. Pagination remains in the list and is only shown when more than one page exists. Android Back first dismisses a preparation confirmation without a write, then returns from ticket detail to the retained queue selection/filter/page. Busy submissions remain guarded.

These corrections supersede the previous oversized return buttons and all-scrolling action placement. Keep the visual references, domain transitions, permission checks, confirmation and no-replay protections unchanged. Revalidate affected layouts and selectors against the new source; earlier native acceptance does not establish acceptance of this correction.

### Compact header and function discovery follow-up

Place the Preparation heading and labeled Staff home return on one row at normal text, omit the redundant status subtitle, and size the three filters to their labels instead of stretching them across the page. Keep 48dp targets and stack navigation/filters when enlarged text needs room. Render the working heading only after fresh access verification; checking, denied and failed states retain return navigation.

Home workspace summaries enumerate the functions allowed by effective permissions, including cancellations/delivery assignment, low stock, purchase receipts, movement history and account/security operations. Preserve every workflow from `add-staff-mobile-app`; a restricted demo role must not be mistaken for removal of a page. The existing full desktop administration/reporting exclusion remains authoritative.

Counter exposes Orders and Tables as explicit selected views. Tables must be reachable without scrolling through order results; opening an occupied table and returning restores the Tables view. Switching back to Orders preserves its search and status filter. Manager order operations retain their existing search and cancellation/assignment flow.

The subsequent shared polish uses the compact, permission-checked title/return header for every workspace. Order, table, discount, delivery, inventory and staff/security records share open ruled rows. Shared input styling owns the thin outline, bundled body font and minimum 48dp height; feature-specific duplicates are removed. Record-opening links use concise visible labels with full record-specific accessible names, and return controls use the plain treatment. Business confirmations retain their existing emphasis and guards.

Counter and manager order search initially render ten matching orders. Show more orders adds the next ten and focuses the first newly revealed record. Search and status filtering apply to the complete server result before this rendering limit, and reset the visible batch. Returning from detail retains the expanded batch and existing view state. This reduces large-list rendering without adding an API contract or limiting which records can be found.

### E2E tool and completion gate

The owner acceptance recorded in [the verification wrap-up](../../../docs/evidence/mobile-ux-219/current/README.md) completes item 2.2 and waives repeating the existing verification for minor UI follow-ups. Preserve its source revisions and limitations. The commands below describe the original verification plan; they do not override that acceptance. Subsequent visual polish uses bounded visual/static checks and focused regressions for new rendering behavior, without claiming another complete native or TalkBack run. Item 3.1 remains the named review gate.

Use **Maestro CLI** with the existing Android YAML flows in `apps/staff-mobile/.maestro`, ADB, an installed development build and a disposable API fixture. Reuse the existing [quality runbook](../../../docs/mobile-quality-and-release.md), [workflow harness](../../../docs/mobile-workflow-verification-201.md), and their prerequisites. Record `maestro --version`, device/API level, app/source revision and fixture setup; the historical baseline used Maestro 2.12.1, not an instruction to silently upgrade. Installing tooling or committing a plan does not establish a passing Android run.

Extend `.maestro/barista.yaml` and affected `.maestro/workflows-focused` selectors/flows for labeled queue entry, all three filters, selected-ticket opening, size/add-on visibility, start confirmation, canceled ready confirmation (no write), confirmed ready transition, return navigation, and offline/stale/forbidden behavior. Use stable accessible labels or test IDs, not coordinate taps. Reuse the forwarding harness for forced outcomes and independent API assertions; retain it as the authority for proving no offline/canceled write and no automatic replay. Rerun existing payment, administration, inventory, delivery, notification/session and permission coverage when shared controls or labels change.

Reuse the native capture runner `apps/staff-mobile/scripts/verify-native-usability.ps1` and `.maestro/usability` flows added in `57b2801`; do not add a duplicate capture tool. Run the before/after stages against their recorded source revisions with a specified device serial and fresh result directories. Update affected selectors and extend the capture coverage to the 390×844dp reference content state and concept confirmation/cancel states, while retaining the existing small-phone/tablet profiles. The capture harness supplements behavioral/API assertions and manual TalkBack checks; its presence is not proof that those gates passed.

Run from the repository root after implementation:

```sh
npm run check --prefix apps/staff-mobile
npm run check --prefix contracts/mobile-p0
npm run smoke:android --prefix apps/staff-mobile
npm run test:e2e:barista --prefix apps/staff-mobile
npm run test:e2e:workflows --prefix apps/staff-mobile
npm run test:e2e:suite --prefix apps/staff-mobile
```

These runners require the separate environment/fixture setup documented by their runbooks; do not point them at a shop database. Keep fresh per-run JUnit results, sanitized screenshots/failure artifacts, source/device metadata and independent API verification under the existing issue evidence directory. Fail on failed, skipped or missing required flows; stale reports and partial reruns cannot establish a complete passing run. If a device or service is unavailable, record the blocker and keep the verification task open.

Capture the reference content state at 390×844dp and normal text; review side by side with the ledger/detail PNGs for hierarchy, typography, spacing, colors, selection and confirmation. Also exercise the existing small phone 360×800dp and tablet 800×1280dp profiles at 100%/200% text with light/dark system settings while the app remains light-only. Preserve manual TalkBack focus/announcements, measured targets, safe areas, and keyboard reachability checks. Maestro behavior passes and screenshots alone do not prove native accessibility or visual acceptance. Document material deviations and resolve them before requesting reviewer approval.

## Risks / Trade-offs

- Layout changes affect all routes → retain route/session regression tests and verify complete-shell safe areas, keyboard and enlarged text.
- Disclosure adds a tap to change an order filter → keep the selected filter visible and preserve every filter.
- Focus restoration can race native layout → use layout callbacks and stable host refs; do not steal focus during polling/refetch.
- A write may succeed while refresh fails → keep its confirmation and require authoritative refresh before dependent writes.
- Native tools or services may be unavailable → record exact limitations and leave native acceptance unchecked; never substitute browser screenshots for Android evidence.
- AI raster art has approximate font metrics → implement with the bundled fonts and explicit logical-pixel rules, then record matched-state differences rather than stretching the art or claiming pixel-perfect accessibility.

## Migration Plan

Retain the completed audit/shared-control/feedback work. Commit the concept pack and aligned plan, implement the ledger queue and focused detail, extend component/Maestro regressions, then perform one consolidated automated/native evidence pass and named review. GitHub issue #219 is the only task checklist: archive old completed items as baseline context instead of duplicating them or copying tasks into a local `tasks.md`. Previous test counts remain historical and do not complete the new concept/E2E work. No data migration is needed. Reverting the visual feature commits restores the prior UI. Issue #219 remains open until all new work, acceptance evidence and @longtoZ approval are recorded.

### Owner follow-up: pinned controls and cached workspace navigation (2026-11-23)

The owner requested pinned Stock controls, consistent fixed navigation across workspaces, and no repeated server requests merely from changing pages. Local workspaces share the already verified `/session` route; this supersedes the earlier per-tab fresh-request rule. Route entry, foregrounding, realtime reconnect and account-change events revalidate access. Changed grants discard cached user queries, while server authorization and online-only mutations remain authoritative. Query data stays fresh until explicit invalidation rather than becoming stale solely on remount. Manual refresh, mutation reconciliation and committed live events remain effective.

Use separate title, section and search header slots. Titles/section navigation remain fixed; search controls reflow into content above 150% text or below 560dp height. Keep existing reserved action docks. Render long receipt and movement results in batches of 20 with Show more, preserving every returned record.

### Owner follow-up: consistent header actions (2026-11-23)

Render each page's back action on the left of its title and its manual refresh action on the right, outside scrolling records. Use named 48dp icon buttons with visible focus, disabled and loading states. Detail headers override route-level titles/back actions while open and restore them on return. Sheets own their header and scroll/navigation state so manager order controls cannot replace actions on the Approvals page underneath. Keep search/filter reflow and existing refresh handlers, cache invalidation, mutation confirmations and permission checks.

### Owner follow-up: Preparation tab and Account-only More (2026-11-23)

Remove discount approval UI, home counts, protected routes and notification destinations from the mobile app. Preparation is a direct, permission-derived bottom-tab workspace; More contains only Account. Operational tabs no longer spill into More. Staff administration remains reachable from Account, and permission-checked cancellation/delivery assignment controls remain with order details. The dedicated manager operations route moves under cashier. These instructions supersede earlier mobile approval and overflow-navigation designs. Update automated coverage and retired-flow fixtures for GitHub CI; do not run local tests for this change, as explicitly requested by the owner.

### Owner follow-up: delivery-only mobile application (2026-11-23)

Narrow the app to Today, Deliveries and More → Account. Only `delivery.update` permits delivery work; broader staff permissions do not expose other mobile workspaces. Today shows active assignments and a next/continuing job using the same cached query as the active deliveries list. Counts are active-job counts, not unsupported daily performance metrics. Assignment details retain private-recipient isolation, directions, confirmed outcomes and a new call-customer action with launch failure feedback.

Retire Preparation, Orders/payments, Stock, manager operations and staff administration from mobile navigation and routes; reject every non-assignment notification destination. Retain shared legacy components/permission metadata for isolated regressions without importing those features into the app routes. Existing Windows/backend workflows remain outside this mobile scope. Keep Account available to signed-in staff without delivery access so they can leave the session. Preserve header placement, cached local navigation, foreground/access revalidation and online-only writes. CI owns validation for this follow-up at the owner's request; no new local test/native acceptance claim is made.
