# Native mobile implementation verification — 2026-11-23

The approved native mobile design is now applied to the Expo staff application. The local Figma/mockup package is excluded from this commit series at the owner's request. Source manifests preserve the base revision and exact working-tree file hashes at capture time; the spacing follow-up has its own source record and screenshots.

## Implemented

- Bundled Inter Regular, Medium and SemiBold, licensed with the application; original vector icon paths from the mockup; warm off-white background, white records, cocoa actions and consistent spacing.
- Permission-derived bottom destinations, compact Today, More and Account. Workspace state survives tab changes; fresh permission checks precede revealing retained work and revoked workspaces are removed. Existing protected links remain guarded.
- Direct preparation and inventory record entry, bounded order cards, focused details, segmented controls, order filters and tabular money. Existing search, filters, paging and business behavior remain available.
- Keyboard-aware, scrollable review sheets for payment, preparation, stock adjustment, receipt posting, discounts, delivery outcomes and sign-out. Payment request identity, explicit retries, online guards and server-confirmed outcomes remain intact.
- Degraded connectivity and live-update notices sit inside scrollable content, within the safe area.

## Completed checks

Using Node **26.5.0** and npm **11.17.0**:

| Check | Result |
| --- | --- |
| Component and session tests | **351 passed / 29 suites** |
| Inventory and workflow forwarding harnesses | **7 passed** |
| TypeScript, ESLint and Prettier | Passed |
| Mobile contract generation and contract package checks | Passed, no generated contract drift |
| Android Hermes export | Passed |
| Strict OpenSpec validation and whitespace checks | Passed |

New regressions cover retained order search, permission revocation without another operational read, retained inventory detail/search, canceling a reviewed adjustment without a write, and canceling sign-out. Existing mutation, retry, stale-state and access checks remain in the suite. Existing Maestro flows were updated for the new entry and confirmation steps; those updates alone do not establish a full shared-API suite pass.

## Fresh Android review

The repository's existing native capture runner and Maestro **2.12.1** are used, with Android API **36.1**, Expo Go and the current application bundle. The `native-mockup` flow exercises Today → Orders → Stock → retained Orders search → order/payment sheet → More/Preparation → ready confirmation cancellation → stock adjustment cancellation → Account/sign-out cancellation.

The review uses a temporary, read-only local HTTP fixture with synthetic records. It is outside the application and is not bundled into the export. It rejects business writes. Its missing live-update hub produces the visible, accurate “Live updates unavailable” warning; Expo Go's developer control is also visible in captures.

Native profile results and sanitized request counts are recorded in `native-results.json`. Only captures from completed passing cases are published here. Failed and interrupted attempts remain under the application's ignored `.maestro/results` directory. Raw logs and typed credentials are excluded.

| Native case | Result | Captures |
| --- | --- | --- |
| 360 × 800dp, 100% text, light system appearance | Passed | [Today](p100/native-home.png), [Orders](p100/native-orders.png), [payment sheet](p100/native-payment-sheet.png), [preparation](p100/native-queue.png), [ready confirmation](p100/native-ready-sheet.png) |
| 360 × 800dp, 200% text, dark system appearance, light app | Passed | [Today](p200/native-home.png), [Orders](p200/native-orders.png), [payment sheet](p200/native-payment-sheet.png), [preparation](p200/native-queue.png), [ready confirmation](p200/native-ready-sheet.png) |

Both cases complete the full navigation/cancellation flow, with **20 screenshots** and **zero business writes** in the fresh fixture request log. This confirms the exercised UI actions do not submit changes. It does not replace independent real-service outcome checks. Screenshot names remain relative to Maestro's per-run output folder, as required by its path guard; all twenty expected screenshot commands passed.

Temporary review services and their port forwarding were stopped. The initial emulator display profile was restored to 1170 × 2676px, 480dpi, 100% text and light system appearance. Existing development services were left running.

## Limits

This is UI/navigation verification against synthetic reads, not a new full shared-API business acceptance run. The complete role/fault matrix, tablet layouts, manual TalkBack traversal, iOS review and named reviewer approval have not been newly completed. Earlier acceptance evidence retains its historical source revision and does not establish acceptance of this substantive redesign. No deployment or release approval is claimed.

`react-native-svg` is a new native dependency, pinned to Expo's bundled version. Expo Go was verified. Existing custom development clients need a fresh native build before they can render the new vector icons; no replacement APK or production build was installed in this session.
