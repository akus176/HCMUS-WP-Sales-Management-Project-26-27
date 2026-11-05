# Mobile quality evidence (#146)

OpenSpec change `add-staff-mobile-app`; this issue contributes to Mobile tasks
6.1 and 6.3. Those tasks cover more than this issue and remain incomplete.

## Baseline inventory

- `mobile-quality` already runs the complete Jest suite, type-check, lint,
  formatting, and an Android JavaScript bundle export.
- `mobile-p0-contract` regenerates the API contract and checks for drift.
- The suite already covers session rotation and cleanup, route permission
  checks, and offline mutation rejection. Further cases must exercise a
  distinct boundary rather than merely rerun existing tests.
- `.maestro/starter.yaml` is an existing non-sensitive native launch flow.
  Its [#138 evidence](../mobile-scaffold-138/README.md) is historical and is
  not evidence of a launch on the current checkout or runner.

The role modules include planned placeholders. Tests of navigation, fixtures,
or bundle generation do not prove that operational workflows are implemented.
