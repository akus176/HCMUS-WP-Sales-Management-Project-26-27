# Corner Cafe mobile UI/UX concepts

Research and concept art prepared on 2026-11-22 for the existing Android staff app. [OpenSpec issue #219](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/219) uses the ledger queue and focused ticket view as its implementation targets. The paper-ticket stack remains an alternative exploration. Committing the art and plan does not establish that a rendered implementation has passed acceptance.

## Theme grounding

The repository contains two distinct visual systems:

- The current mobile [tokens](../../../apps/staff-mobile/src/shell/designTokens.ts) and [workflow design](../../../openspec/changes/add-staff-mobile-app/design.md) use warm paper, pencil-brown foreground, handwritten typography, blue information, yellow emphasis, and red correction.
- The desktop [luxury theme](../../luxury-theme.md) uses alabaster, charcoal, EB Garamond, Instrument Sans, fine gold rules, and square controls. Its approved [Service board](../service-board.png) additionally uses taped paper tickets.

These concepts use the existing mobile system because the requested surface is mobile. The desktop board supplies a restrained paper-material reference for the second image. Its serif/gold visual system is not substituted for the mobile tokens. This was the explicit default at generation time and is retained in the concept-aligned OpenSpec plan.

Actual references inspected and attached to every generation: [native mobile home](../../evidence/mobile-quality-186/phone-home.png) and [desktop Service board](../service-board.png). The native evidence was captured at **200% text scale**; its unusually large content is not evidence of a default-scale layout defect. The concepts target normal text scale. An [earlier normal-scale shell capture](../../evidence/mobile-shell-144/phone.png) was also inspected for layout context.

| Role | Existing value | Concept use |
| --- | --- | --- |
| Paper | `#fff9ed` | Main surface |
| Pencil | `#302820` | Primary text and strong selection |
| Cocoa | `#6d4936` | Primary actions and outlines |
| Information | `#154d70` / `#e8f0f5` | Connection and preparation status |
| Highlight | `#f5e6b4` | Confirmed tickets, add-ons, confirmation |
| Correction | `#aa2c2c` | Error/urgent meaning only |
| Display/body | Kalam / Patrick Hand | Existing handwritten type roles |
| Spacing | 4 / 8 / 16 / 24 / 32 | Existing spacing scale |
| Corners | 8 controls / 12 tickets | Existing mobile shapes |

## Research and design implications

This is public-source desk research and repository inspection, not interviews, observed staff behavior, or a usability study. The implications below are design recommendations, not measured improvements.

| Source | Finding | Application to these concepts |
| --- | --- | --- |
| [NN/g: Basic Patterns for Mobile Navigation](https://www.nngroup.com/articles/mobile-navigation-patterns/) — foundational article, published 2015-11-15; accessed 2026-11-22 | Navigation competes with task content for limited mobile space; visible labels aid discoverability. | Keep a labeled Staff home/Preparation back route. Do not expose every staff module in each barista screen. |
| [Square: Set up Square KDS](https://squareup.com/help/us/en/article/7944-get-started-with-square-kds-android) — accessed 2026-11-22 | Kitchen workflows distinguish preparation from broader expediting. Ticket layout, text size, and modifier presentation support scanning. | Give ticket identity, quantity, size, add-ons, and current state clear roles. Adapt the hierarchy to one mobile column; do not copy Square's tablet column layout or unsupported features. |
| [Android Developers: Make apps more accessible](https://developer.android.com/guide/topics/ui/accessibility/apps) — accessed 2026-11-22 | Android recommends touch targets of at least 48 × 48 dp, and 4.5:1 contrast for ordinary small text. | Specify at least 48 dp targets, 56 dp primary actions, explicit status text, and readable brown-on-paper content. |

## Concept art

The displayed image order is also the numbering below. Each is an independently generated screen, with a different layout or task emphasis inside the existing theme.

| Image | Composition | Tradeoff |
| --- | --- | --- |
| [1 — Queue ledger](01-queue-ledger.png) | Open ruled rows; three active tickets visible; one selected ticket action. | Strongest starting point for queue scanning; selected-row behavior would need explicit implementation. |
| [2 — Paper ticket stack](02-paper-ticket-stack.png) | Two aligned paper notes; more item detail per ticket; subtle tape and folded corners. | Closest material connection to the desktop board; fewer tickets fit in one viewport. |
| [3 — Ticket focus](03-ticket-focus.png) | One preparing ticket with prominent add-ons and an inline ready-confirmation area. | Suitable as the detail screen after either queue direction; not a replacement for browsing the queue. |

All files are 852 × 1846 px, approximately the requested **390 × 844 logical-pixel** aspect ratio at 2.18× raster scale. These are static AI-generated concept images, not interactive prototypes or pixel-accurate native layouts. Font metrics, touch dimensions, accessibility, colors, and dynamic text require verification in a later rendered implementation.

The [OpenSpec design](../../../openspec/changes/polish-staff-mobile-ux/design.md) targets the ledger queue and focused ticket detail. The paper-ticket queue is retained for comparison, without requiring another mode or expanding implementation scope. Exact logical-pixel, behavior and accessibility requirements are specified in that design; the AI raster images are not native layout measurements.

## Workflow fidelity and handoff

The [current preparation screen](../../../apps/staff-mobile/src/barista/PreparationScreen.tsx) supports confirmed/preparing filters, refresh, ticket detail, add-ons, and start/ready confirmation. The art stays within that workflow. It does not propose per-item completion, swipe-only actions, priority reassignment, estimated times, or a new ordering system.

The ledger introduces selection plus a selected-ticket action; the previous app opens each ticket directly. Issue #219 now tracks this interaction change, including stale-selection clearing and return-state preservation. Queue item-name previews reuse information already present in ticket lines. The alternative paper-stack queue keeps a distinct Open action before any state change.

The focused detail image shows the confirmation state, with proposed plain-language copy. Keep the existing server-confirmed transition and protections: disable writes offline or while busy, refresh after stale/unknown outcomes, and announce success only after confirmation from the service. Do not infer a new undo capability from the artwork.

For a later implementation, retain permission-derived navigation and reuse this hierarchy elsewhere: decision-first approvals, quantities/units first in inventory, and assignment/state first in delivery. These additional workflows are not illustrated or implemented in this concept set.

Preserve scrolling and reflow at 200% text scale; a confirmation area must not cover ticket content or the keyboard. Use a conventional icon library with labels, actual bundled fonts, accessible status announcements, and non-color state indicators. The raster grain and paper decoration must not reduce text clarity. These are implementation requirements, not checks proven by the concept art.

## Files and provenance

- [Queue ledger PNG](01-queue-ledger.png)
- [Paper ticket stack PNG](02-paper-ticket-stack.png)
- [Ticket focus PNG](03-ticket-focus.png)
- [Complete generation prompts and attached references](prompts.md)

Generated with the built-in Image Generation tool. Prompts use synthetic ticket data and the date anchor 2026-11-22. No application code was changed; no runtime tests were necessary for these documentation and image artifacts.
