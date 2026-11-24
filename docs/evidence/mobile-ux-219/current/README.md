# Verification wrap-up (#219)

On 2026-11-22, the owner accepted the existing verification for issue #219 item
2.2 and explicitly waived repeating verification for the minor UI follow-ups.
Item 2.2 is complete on that acceptance basis. Evidence retains its actual source
revision; no additional native or TalkBack run is claimed. Interrupted or partial
attempts are not counted as passing runs.

## Retained passing native evidence

These runs cover application `fea0541`, before the subsequent direct-sign-in and
compact-header corrections. The owner accepts these recorded results for the
minor follow-ups without requiring another verification run.

| Check                               | Recorded result                                           | Evidence                                                                            |
| ----------------------------------- | --------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Standalone barista                  | 1/1; independent manager API verification                 | [JUnit](../native/reports/barista-standalone.xml)                                   |
| Complete role/workflow suite        | 10/10; independent API outcomes                           | [Summary](../native/reports/suite-summary.json)                                     |
| Focused fault/state suite           | 19/19; 43 native steps, state/history and no-write checks | [Summary](../native/reports/workflows/summary.json)                                 |
| Layout and keyboard matrix          | 16/16; four profiles and four roles                       | [Gallery](../native/gallery.html), [summary](../native/reports/layout-summary.json) |
| Ready confirmation and cancellation | 5/5; unchanged independent API snapshots                  | [Reference/normal/enlarged captures](../native/confirmation/README.md)              |

[Native provenance and limitations](../native/README.md) identify source/backend
revisions, original image hashes, measured targets and safe areas. Large layouts
use display overrides on the connected Samsung phone; no physical tablet test
is claimed. The existing ARM development shell used the recorded Metro source.

## Latest completed local checks

[Quality provenance](reports/quality.json) records pinned Node 26.5.0/npm 11.17.0:
344 component tests across 28 suites, seven forwarding-harness tests, TypeScript,
ESLint and formatting pass. Contract regeneration has zero drift, and Android
Hermes export passes. Strict OpenSpec validation and whitespace checks pass.

The frozen application revision was `3117ec2`; subsequent driver revision
`f55da46` changes only Maestro flows within the mobile app. Its application source
and router trees are identical to the frozen revision. A targeted inventory flow
also passed after distinguishing the quantity input from its persistent label,
including adjustment, saved balance, receipt posting and history assertions.

Fresh standalone barista progression passed on the latest runtime; the additional
consolidated native run was stopped at the owner's request. Latest-UI full native
and completed manual TalkBack checks were not rerun and are covered by the owner's
explicit acceptance of the existing evidence. Named reviewer approval under item
3.1 remains outstanding; no closure or archive is claimed.

[Device restoration](reports/device-restoration.json) records verification that
the original display/density, text size, appearance, timeout, autofill,
accessibility services and radios were restored. Temporary TalkBack speech
captions and notification-banner settings were restored as well.
