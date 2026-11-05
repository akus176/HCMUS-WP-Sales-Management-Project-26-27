# Mobile connectivity and safe writes (#145)

OpenSpec change `add-staff-mobile-app`, task Mobile 4.6. The implementation lives
in `apps/staff-mobile/src/connectivity/` and is mounted by the staff app root.

## Behavior

- Expo Network supplies the device connectivity snapshot and change listener.
  Unknown/disconnected states fail closed for writes.
- The app shows checking, online, reconnecting, and offline feedback. The online
  and offline states provide a manual data refresh or connection check.
- TanStack Query caches server results only in memory. A reconnect and manual
  refresh invalidate active queries so the next visible state comes from the API.
- State-changing API calls use `useSafeMutation`. It exposes `canMutate` and
  `isWriteDisabled` for action controls, uses `networkMode: 'always'` with retry
  disabled, and rejects before invoking the request while offline or reconnecting.
  That prevents TanStack Query from pausing and replaying a write later.
- Successful writes invalidate active server queries. A `409` invalidates them
  before the user retries; validation and forbidden errors remain visible and do
  not create optimistic local state.

## Verification

`apps/staff-mobile/tests/connectivity.test.tsx` checks offline, online, reconnect,
manual refresh, disabled actions, no queued/replayed writes, and server-authoritative
recovery after validation (`422`), forbidden (`403`), and conflict (`409`) errors.
The conflict case retries deliberately after the latest server value is refreshed.

Reproduce with Node 26.5.0 and npm 11.17.0 from `apps/staff-mobile`:

```sh
npm run check
npm run api:check
EXPO_PUBLIC_APP_ENV=release \
EXPO_PUBLIC_API_BASE_URL=https://staff.example/api/v1 \
  ./node_modules/.bin/expo export --platform android --output-dir /tmp/mobile-connectivity-android
```

Verified for this change:

| Check               | Result                                                                                       |
| ------------------- | -------------------------------------------------------------------------------------------- |
| `npm run check`     | Passed: TypeScript, ESLint, Prettier, and 14 Jest suites (121 tests).                        |
| `npm run api:check` | Passed: OpenAPI 7.10.1 regenerated the schema with no diff.                                  |
| Android Expo export | Passed: release-configured Android bundle exported to `/tmp/mobile-connectivity-145-export`. |
| `git diff --check`  | Passed: no whitespace errors.                                                                |

Component tests use a controlled network-state adapter; the Android export verifies
the Expo Network native module is included in the app bundle. No physical-device
airplane-mode/TalkBack session was run. No workflow-specific mutations, persistent
business store, background sync, or offline write queue was added.
