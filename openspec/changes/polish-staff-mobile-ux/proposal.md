# Proposal

## Why

Staff need working records and actions to remain easy to reach while using Android quickly or with enlarged text. Issue #219 follows the existing mobile foundation with a complete screen audit and focused improvements to hierarchy, navigation, forms, and feedback.

## What Changes

- Reduce persistent connection chrome and give safe-area edges one owner.
- Reuse page headers, section typography, labelled inputs, and responsive selection controls.
- Collapse Counter filters while retaining the active selection; separate staff accounts and security events and make account detail navigation explicit.
- Give confirmed writes, refresh failures, and uncertain outcomes distinct feedback without changing permissions, API contracts, or retry/idempotency rules.
- Inventory all screens and states, extend regression/accessibility coverage, and retain reproducible Android before/after evidence.
- Keep the Corner Cafe identity, existing fonts, light-only palette, and installed dependencies.

## Capabilities

### New Capabilities

- `staff-mobile-usability`: Focused operational navigation, accessible forms, compact connection feedback, and accurate submission outcomes supplementing the unarchived `staff-mobile-app` capability.

### Modified Capabilities

None. There are no archived main specs; the parent mobile behavior and business contracts remain authoritative.

## Impact

Changes affect `apps/staff-mobile` shared controls, layout, feature presentation, tests and Maestro flows, plus audit evidence in `docs/evidence/mobile-ux-219`. GitHub issue #219 is the canonical checklist. No API, database, permission, dependency, or production-release changes are required.
