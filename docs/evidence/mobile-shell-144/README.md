# Permission-aware mobile shell — #144

Date: 2026-11-05. Contributor: **@akus176**, at their explicit request, with Codex
implementation assistance. Ticket owner: @JakeConal. Required reviewer: @vt-Toan.
This is implementation/verification evidence, not reviewer approval or a completed
business-workflow demonstration.

## Implemented behavior

- Home modules and individual planned actions derive from API permission codes,
  never role labels. Tests compare the policy against all six real backend seed
  bundles and cover multi-role combinations, unknown codes and no-permission users.
- Signed-out deep links are removed by Expo Router's protected stack. Module routes
  additionally fetch current `/auth/me` permissions before mounting any feature
  children. Unknown/ambiguous module IDs, cross-account or malformed responses,
  revoked grants and failed checks never mount those children.
- Home and module route focus and app resume recheck access, including users with
  no initial module grants. Backgrounding hides module content;
  effect cleanup cancels obsolete checks and discards late results. Session loss
  hides protected content immediately. A network failure offers explicit retry.
- Module pages clearly label business actions as planned. They make no business
  queries or mutations. These are navigation/access boundaries for later #155,
  #156 and other workflow implementations.
- Reusable shell controls use the existing sign-in palette, scrollable content,
  scalable text, accessible names, at least 48-dp targets and focus/selection states.

## Executed verification

Toolchain: Node **26.5.0**, npm **11.17.0**; pinned project versions, no lockfile or
production dependency changes. The worker's default Node 24/npm 11.9 could not
install the app because of its strict engines; checks used npm exec to select the
required versions, without weakening engine checks.

| Check | Actual result |
| --- | --- |
| Clean `npm ci` for `contracts/mobile-p0` and `apps/staff-mobile` | Passed; existing upstream dependency deprecation/install-script notices retained |
| `npm run check` in `apps/staff-mobile` | TypeScript, ESLint with zero allowed warnings, Prettier, **104 tests in 12 suites passed**, zero failed/skipped |
| `npm run api:check` | Deterministic generation passed; committed schema unchanged |
| Release-profile `expo export --platform android` | Passed; Android Hermes bundle generated |
| Browser fixture at 390×844 and 800×1024 | Captured and inspected; no horizontal overflow, all primary targets at least 48×48 |
| Browser doubled-text simulation at 390×844 | Captured and inspected; all actions remain reachable by scrolling |
| Browser control-state/keyboard-focus check | Passed; selected state visible and focused border matched `#302820` |

Tests were written before implementation. Initial functional stub run: 17 failed,
1 passed. The completed full-suite run above includes the backend seed matrix,
real route integration, boundary lifecycle and shell-control assertions. The
role-matrix cases use both explicit minimal fixtures and the actual C# seed source.
A focused home test initially timed out during concurrent cold runs; it passed
in isolation and in the final complete suite without increasing the timeout.

### Reproduce mobile checks

Use the pinned Node/npm versions and follow `apps/staff-mobile/README.md` for
installation. From that directory:

```sh
npm run check
npm run api:check
EXPO_PUBLIC_APP_ENV=release \
EXPO_PUBLIC_API_BASE_URL=https://staff.example/api/v1 \
./node_modules/.bin/expo export --platform android --output-dir /tmp/mobile-shell-android
```

The export URL is a non-secret example used only for a build. No network service
or live account at that address was used or implied.

## Visual evidence and limits

- [Phone](phone.png), [tablet](tablet.png), [doubled text](phone-large-text.png),
  [control states and keyboard focus](control-states.png).
- [Machine-readable browser checks](browser-checks.json).

These are **browser fixtures of the real StaffHome and shell controls**, bundled
through React Native Web 0.21.2. Session data and router actions are substituted
with synthetic multi-role fixtures; the images are not authenticated Android or
production screenshots. Doubled CSS text is a layout simulation, not Android
Dynamic Type. API/deep-link protection is tested separately by real Expo Router
component tests and the real session transport with synthetic HTTP responses.

The fixture toolchain lives outside the application and adds no production
packages. To reproduce, install `react-native-web@0.21.2` and `esbuild@0.25.12`
in `/tmp/mobile-144-visual-tools`, then from repository root:

```sh
node docs/evidence/mobile-shell-144/build-visual-fixture.cjs
python -m http.server 8194 --directory /tmp/mobile-144-visual-tools/site
# In a separate terminal with Python Playwright and /usr/bin/chromium:
python docs/evidence/mobile-shell-144/capture-browser.py
```

The native Android SDK/emulator, screen-reader and physical keyboard checks have
**not** been executed in this workspace. Android bundle export does not establish
native layout, TalkBack or device session/storage behavior.

## Acceptance and closure

| #144 requirement | Evidence / remaining work |
| --- | --- |
| P0 roles see only permitted modules/actions | `permissions.test.ts`, `shell.test.tsx`, backend-seed matrix |
| Unauthorized/stale deep links reveal no protected content | `module-navigation.test.tsx`, boundary revocation, late-response and session-loss tests |
| Labels, selection/focus and usable actions | `shell-controls.test.tsx`, browser targets/focus and visual captures; native verification remains outstanding |
| Shared S1 design foundation integration | Small reusable shell controls supplied here; #140's broader foundation and final integration remain open |
| Independent required review | @vt-Toan must record a decision; no approval is claimed |

Keep #144 and its full OpenSpec task unchecked until the reviewer accepts the
remaining native/design integration evidence. Do not treat passing CI or this
contribution record as approval. No new account, secret, database connection,
role administration or business-workflow implementation was added.

## Independent review and fixes

A fresh code reviewer independently ran the pre-fix quality suite (99/99) and
identified two issues: home navigation used stale login permissions, and a
duplicate Android active notification could leave checking stuck. Both were
reproduced by failing tests, then fixed. Home now uses a fresh API-verified scope
and accepts zero-grant users without granting any module. Every active event
invalidates and restarts verification. New tests cover empty-home grants on
resume, identity hiding in background, duplicate active events and a retained
module route returning to focus after permission revocation. The final full
suite passed 104/104 with zero failures/skips. Required @vt-Toan review is still
separate from this implementation review.
