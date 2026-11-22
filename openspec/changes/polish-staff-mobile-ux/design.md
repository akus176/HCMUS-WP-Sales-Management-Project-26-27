# Design

## Context

See proposal.md for motivation. The existing Expo Router shell has permission checks on route focus, online-only mutations, idempotent payment retry, scalable shared controls and a light paper palette. PR #221 already implements the audit's compact connection presentation, shared forms, safe-area ownership, Counter filter disclosure, account detail navigation, and accurate mutation feedback. Those are the completed baseline, not new duplicate subtasks.

The 2026-11-22 [mobile concept pack](../../../docs/ui-concepts/mobile-2026-11-22/README.md) supplies the remaining visual target. Use the Queue ledger for Preparation and Ticket focus for detail/ready confirmation. The Paper ticket stack remains an alternative exploration; do not implement both queue presentations or a mode switch. Other mobile workflows retain the completed shared polish and regression coverage, without inventing unillustrated concept screens.

## Goals / Non-Goals

**Goals:** align the preparation queue/detail with the selected planning references; expose working content sooner; preserve enlarged text and 48dp controls; make detail transitions and form feedback predictable; distinguish confirmed writes from refresh and transport uncertainty; verify the changed native experience with Maestro and matched-state captures.

**Non-Goals:** new business actions, role changes, bottom-tab navigation, the desktop serif/gold theme, an additional paper-stack queue mode, a dark palette, new application dependencies or E2E frameworks, generic transport retries or a production deployment. Per-item checkboxes/completion, swipe-only actions, drag-and-drop priority, undo, timers and analytics are not implied by the art.

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

### E2E tool and completion gate

Use **Maestro CLI** with the existing Android YAML flows in `apps/staff-mobile/.maestro`, ADB, an installed development build and a disposable API fixture. Reuse the existing [quality runbook](../../../docs/mobile-quality-and-release.md), [workflow harness](../../../docs/mobile-workflow-verification-201.md), and their prerequisites. Record `maestro --version`, device/API level, app/source revision and fixture setup; the historical baseline used Maestro 2.12.1, not an instruction to silently upgrade. No Maestro installation or Android run is claimed by this planning change.

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
