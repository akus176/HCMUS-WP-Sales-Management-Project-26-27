# Mobile design-system foundation (#140)

OpenSpec change `add-staff-mobile-app`, task Mobile 1.5. The shared implementation
is in `apps/staff-mobile/src/shell/designTokens.ts` and
`apps/staff-mobile/src/shell/controls.tsx`.

## Reusable foundation

`mobileTokens` defines semantic colors, body/display typography, spacing, control
and card shapes, and card elevation. The existing licensed Kalam Bold and Patrick
Hand Regular fonts are bundled under `apps/staff-mobile/assets/fonts/`; their OFL
licenses are included beside them. Expo loads both fonts before rendering the
authenticated shell. Controls continue to allow React Native system font scaling.

`ShellPage`, `ShellHeading`, `ShellText`, `ShellFeedback` and `ShellButton` are
shared across the staff shell and can be imported by S2 workflow screens. The page
uses a full-width scrollable content region capped at 720 dp for tablet reading;
text and controls wrap and the page scrolls rather than clipping content. The
controls provide loading, empty, error, warning, info, selected, disabled and
busy states with screen-reader roles, labels and state values.

## Contrast evidence

Ratios are calculated by `contrastRatio` using the WCAG relative-luminance
formula and asserted in `apps/staff-mobile/tests/design-system.test.tsx`. All
text combinations below exceed WCAG AA's 4.5:1 minimum for normal text.

| Use | Foreground | Background | Contrast |
| --- | --- | --- | ---: |
| Main text | `#302820` | `#fff9ed` | 13.81:1 |
| Primary action label | `#fff9ed` | `#6d4936` | 7.54:1 |
| Error | `#aa2c2c` | `#f6e7e5` | 5.63:1 |
| Warning | `#5c4300` | `#f5e6b4` | 7.47:1 |
| Informational | `#154d70` | `#e8f0f5` | 7.83:1 |
| Disabled action label | `#645343` | `#eee1ca` | 5.68:1 |

## Touch targets and component checks

Primary and secondary `ShellButton` controls set `minHeight` and `minWidth` to
48 React Native dp. Disabled and busy controls keep the same target size; tests
also assert their accessible disabled/busy state. Selected controls expose
`selected: true` and add a visible checkmark and contrasting selected treatment.

`design-system.test.tsx` covers the required loading, empty, error, warning,
selected and disabled states, accessible feedback roles/names, primary action
labels, contrast pairs, 48-dp targets, responsive width constraints, text wrapping
and system text scaling. `shell-controls.test.tsx` also checks focus and selection
feedback. Reproduce the project suite from `apps/staff-mobile` with
`npm run check` using Node 26.5.0 and npm 11.17.0.

Executed for this change:

- `npm run check`: passed type-check, lint, formatting, and all 112 tests across
  13 suites.
- `npm run api:check`: passed; the generated OpenAPI schema remained unchanged.
- Release-profile Android export with `expo export --platform android`: passed;
  Metro bundled the app and both font assets into the Android Hermes bundle.

Automated component/style checks do not replace on-device TalkBack, font-scale,
or touch testing. No Android emulator or physical-device accessibility session
was run for this change; those remain appropriate follow-up validation during S2
screen integration.
