# Matched native reference states

Captured on 2026-11-22 on the connected Samsung SM-A155F, Android 16/API 36,
with Maestro 2.12.1. These are original Android screenshots, not generated art.
The installed ARM development shell serves the recorded JavaScript revision.
The separate layout, transaction/fault and TalkBack gates are recorded in the
[parent audit](../../README.md).

Open [the comparison viewer](../comparison.html) locally to switch between queue,
ready confirmation and cancelled confirmation. It places before/after captures
beside the selected concept. Original PNGs remain unedited. CSS clips measured
system chrome and scales uniformly by the device density; it never stretches a
screenshot. Android reports 1170×2664px, density 480dpi, status inset 87px and
navigation inset 45px: **390×844dp content**, at system text scale 1.0.

| State            | Before                                           | After                                             | Result                                                                              |
| ---------------- | ------------------------------------------------ | ------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Queue            | [Original](before-queue.png)                     | [Ledger](after-queue.png)                         | Live ruled rows, status badges, quantities/previews and explicit selection          |
| Selected action  | [Direct ticket buttons](before-selection.png)    | [One selected-ticket action](after-selection.png) | A-106 remains confirmed; selection does not start preparation                       |
| Ready review     | [Original confirmation](before-confirmation.png) | [Focused confirmation](after-confirmation.png)    | All quantities, sizes, modifiers and both confirmation actions remain visible       |
| Cancelled review | [Original dismissal](before-canceled.png)        | [Keep preparing](after-canceled.png)              | Confirmation closes and Mark ready returns; independent API state remains preparing |

Before app source: `a5a4f0d456a8a81064473a1a11c42e1d3fd65057` (completed shell
polish, before the ledger redesign). After app source:
`5876e5799ef03ebcf2ae5cd042b53bc332ff54de` (includes quantity previews and the
main merge). Later capture-runner commits do not change these application files.
The [manifest](manifest.json) records every image's source/API revisions, attempt,
timestamp, SHA-256 and measured geometry. JUnit device serials are replaced by
the model description; case names and outcomes are retained.

Four fresh reference cases pass, each with exactly one JUnit case and no failures
or skips: [before queue](before-queue.xml), [after queue](after-queue.xml),
[before detail](before-detail.xml), [after detail](after-detail.xml). The initial
852dp-height probes and the frame-settling failure are separate development
attempts and do not count as these accepted reference cases.

The same isolated visual database supplies both members of each pair. A-106 has
three real order lines: Cappuccino M with Oat milk/Extra shot, Cold Brew L and
Butter Croissant Standard. A-105 has two Flat Whites/two Banana Breads; A-108 has
Matcha Latte/Almond Croissant. Fixture identifiers are assigned only in the
disposable database; application code contains no reference ticket data.
A-106 stays confirmed for both queue captures. An explicit manager API command
then moves it to preparing for both detail captures. The
[before/after cancellation snapshots](cancellation-state.json) are equal, and
neither capture confirms ready.

Native queue bounds expose A-106 as selected. Fully visible filter/navigation
buttons measure 48dp high, all three ledger rows 118⅓dp, and the selected Open
action 56dp. A partially clipped pagination button at the scroll viewport edge
is not an intrinsic target measurement. The complete layout audit measures
other shared controls and enlarged-text profiles separately.

The implementation retains service row/line ordering, so A-105 and the item
order differ from the illustrative art. The current preparation response has
table context but no channel field; no-table tickets truthfully say
“Takeaway / delivery”. Previews show the first two supplied lines plus a remaining
count. Existing token rules govern the generated art's approximate metrics:
18sp body, at least 14sp metadata, 24dp page padding, scalable wrapping text,
48dp controls and 56dp primary actions. A visible tick/rail and native selected
state identify selection. Confirmation stays inline and is removed on cancel.
These differences retain live data and existing API behavior; no pixel-perfect
or named-reviewer approval claim is made.
