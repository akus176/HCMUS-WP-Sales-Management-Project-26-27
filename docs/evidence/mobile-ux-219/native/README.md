# Connected Android evidence (#219)

This directory records the implementation at `fea0541`, before the subsequent
direct-sign-in and compact-header corrections. These results do not establish
acceptance of those later changes. Its mobile source tree is
`60bb317547f56ab382aadae04590488b9ab44798`; subsequent evidence commits do not
change that application tree. The API is built from `5876e57`, with binary and
device/tool provenance in [native-run.json](reports/native-run.json).

## Complete runs at the recorded revision

| Run                            | Result                                                   | Evidence                                                                                                          |
| ------------------------------ | -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Standalone barista progression | 1/1, independently verified Ready                        | [JUnit](reports/barista-standalone.xml), [original states](standalone)                                            |
| Full native suite              | 10/10, independent API outcomes pass                     | [Summary](reports/suite-summary.json), [API checks](reports/independent-api-checks.txt)                           |
| Focused fault suite            | 19/19, 43 native steps, independent state/history checks | [Summary and snapshots](reports/workflows/summary.json), [original screenshots](workflows)                        |
| Layout/keyboard matrix         | 16/16 in one complete fresh run                          | [Summary](reports/layout-summary.json), [gallery](gallery.html), [108 original image hashes](updated-images.json) |

All four stages below belong to `acceptance-05`, with application revision
`fea0541`; see [the pipeline record](reports/acceptance-pipeline.json). Earlier
failed/partial attempts are not combined into these results. The PowerShell 5.1
host ran the same public full-suite script directly because `pwsh` is unavailable;
no assertions or flows were omitted. Metro was restarted with its cache cleared
for each API target, using the test profile and USB loopback.

The focused suite uses real upstream conflicts, missing records and permission
removal. Its snapshots and request counts verify that canceled ready confirmation
changes neither state nor history and sends zero business POSTs. Offline cases
also verify no write and no automatic replay after reconnection.

## Visual and geometry review

[Open the layout gallery locally](gallery.html) to compare the retained baseline
states with the current captures. Images retain their aspect ratios and include
Android chrome. The original baseline is `5c9ed01`; individual baseline provenance
and whether its originating workflow passed are explicit in
[baseline-images.json](baseline-images.json). Baseline visual states from a failed
harness attempt do not establish a passing baseline workflow. The additional
p200/t100 baseline run passes all four manager/admin capture cases; their
[summary](reports/additional-baseline-summary.json) is retained separately from
current acceptance. There are five baseline visual states in each profile.

[The matched concept comparison](comparison.html) uses controlled data and a
measured 390×844dp content viewport. Its [reference notes](reference/README.md)
record source/reference revisions, unchanged cancellation snapshots, native
insets, target sizes and deliberate differences required by live data and service
ordering. That viewer clips measured OS chrome with CSS; the original PNGs are
never cropped, stretched or repainted.

| Profile | Logical window | Font setting | Android appearance |
| ------- | -------------- | ------------ | ------------------ |
| p100    | 360×800dp      | 1.0          | Light              |
| p200    | 360×800dp      | 2.0          | Dark               |
| t100    | 800×1280dp     | 1.0          | Dark               |
| t200    | 800×1280dp     | 2.0          | Light              |

The app remains light-only. System keyboard appearance remains Android's own.
The matrix exercises manager operations, administration, inventory and delivery
in each profile, retaining normal/enlarged labels, scroll/review states and visible
sign-in, payment, cancellation, adjustment and failure-reason keyboards.

[Native target measurements](measured-targets.json) cover 121 controls in the
captured end-state hierarchies. All 120 fully visible targets are at least 48dp
in both dimensions. One bounds rectangle is clipped at a scroll-viewport edge;
it is identified explicitly and is not treated as its control's intrinsic size.
Reference capture measurements separately verify the 56dp Preparation primary
action and selected queue row. These are sampled native measurements, supplemented
by the shared-control and accessibility regressions, rather than a claim that
every control was individually measured in every intermediate state.

## Confirmation and remaining acceptance work

[Five additional confirmation cases](confirmation/README.md) pass at `fea0541`,
including the reference viewport and normal/enlarged phone/large layouts.
Independent API snapshots remain unchanged after cancellation. These captures
also predate the later UI corrections. The owner accepted the existing
verification for item 2.2 and waived additional verification of the minor
follow-ups, including the outstanding manual TalkBack work; see the
[acceptance record](../current/README.md). No new run is claimed. Named reviewer
approval remains outstanding.

The device is a physical Samsung SM-A155F, Android 16/API 36. Large layouts use
display/density overrides on this phone; no physical tablet test is claimed.
The installed ARM development shell is reused with unchanged native dependencies
and the recorded Metro JavaScript revision. The APIs use disposable PostgreSQL
databases and synthetic accounts. Raw Maestro command logs and authentication
material are not published.

The evidence file index uses raw SHA-256 for PNGs and UTF-8 with LF line endings
for text, so text hashes survive Git newline conversion. Each index entry records
its hash encoding.
