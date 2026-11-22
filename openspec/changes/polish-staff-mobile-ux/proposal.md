# Proposal

## Why

Staff need working records and actions to remain easy to reach while using Android quickly or with enlarged text. Issue #219 builds on the completed screen audit and shared-control improvements with a concrete visual target: the 2026-11-22 mobile concept art. The remaining implementation aligns the preparation queue and ticket detail with that target and verifies the result on Android.

## What Changes

- Use `docs/ui-concepts/mobile-2026-11-22/01-queue-ledger.png` for the Preparation queue and `03-ticket-focus.png` for ticket detail and ready confirmation. Retain `02-paper-ticket-stack.png` as an alternative reference, not another required queue mode.
- Match the mobile warm-paper palette, Kalam/Patrick Hand typography, compact connection/header treatment, ruled rows, highlighted add-ons, and clear primary actions. Keep all existing filters, pagination, refresh, and permission-aware entry/return paths.
- Address the running-app review with direct sign-in, a consistent branded header, readable permission-based workspace rows and open inventory rows. Keep preparation actions reachable in a reserved bottom region at normal scale and reflow them into the page at enlarged text; make Android Back dismiss confirmation and return to the retained queue.
- Preserve the completed audit, compact connection presentation, safe-area ownership, shared forms, Counter filters, administration navigation, and accurate mutation feedback. Do not reopen those as duplicate implementation subtasks.
- Extend meaningful component regressions and the existing **Maestro CLI** Android E2E flows for queue selection, ticket opening, confirmation cancellation, start/ready progression, and offline/stale/forbidden outcomes.
- Retain reproducible native screenshots, JUnit reports, independent API outcome checks and manual accessibility evidence. Previous audit/test results are a baseline, not proof of concept alignment.
- Keep the Corner Cafe mobile identity, existing routes and fonts, light-only palette, API contracts, permissions, and explicit-retry/idempotency behavior.

## Capabilities

### New Capabilities

- `staff-mobile-usability`: Concept-aligned operational presentation, focused navigation, accessible forms, accurate submission outcomes and auditable Maestro/native verification supplementing the unarchived `staff-mobile-app` capability.

### Modified Capabilities

None. There are no archived main specs; the parent mobile behavior and business contracts remain authoritative.

## Impact

Changes affect `apps/staff-mobile` presentation, component tests and existing Maestro flows, the concept pack in `docs/ui-concepts/mobile-2026-11-22`, and evidence in `docs/evidence/mobile-ux-219`. GitHub issue #219 is the single canonical implementation checklist; do not create `tasks.md`. Maestro already has flows and npm runners in the repository, so this work does not introduce a second E2E framework. No API, database, permission, application dependency, or production-release changes are required.
