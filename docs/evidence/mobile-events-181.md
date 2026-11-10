# Mobile operational events and staff notifications

This change implements [issue #181](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/181). The issue's tasks Mobile 2.5, 4.7, 5.3 and 5.4 correspond by requirement to 2.5, 4.5, 5.1 and 5.2 in the [canonical OpenSpec checklist](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95).

## Verification

| Requirement | Evidence |
| --- | --- |
| Event refresh and reconnect | The mobile tests cover canceling stale reads, targeted invalidation, and refetching after reconnect without event replay. A live smoke test against the Windows API and disposable PostgreSQL used the SignalR JavaScript client and the mobile cache-refresh function. Arabica beans changed from 515 g to 516 g after an API adjustment and then to 518 g after reconnecting following an adjustment made while disconnected. The delivered event contained only `id`, `type` and `updatedAt`. |
| Device lifecycle | Tests cover permission denial, an out-of-order token read, registration, rotation, repeat registration after a lost response, ownership and logout removal. Local sign-out clears the session immediately. Remote removal is best effort if the API cannot be reached. |
| Provider and privacy | A deterministic provider test against PostgreSQL verifies approvals, assigned deliveries, low stock and confirmed orders awaiting preparation. Recipients are filtered by current permissions and courier ownership. The fixed preview and payload contain only generic text, a destination kind and an opaque record identifier. The transport destination token is separate from the notification data. Invalid-token cleanup is exercised by the deterministic provider. |
| Protected notification links | The mobile tests verify the route allowlist and opaque-ID validation, a signed-out link that makes no protected request, and a stale approval link that checks current permissions before fetching any approval. Record reads use the existing permission-protected API, including assignment ownership. |

Commands run against the change:

- `npm run check` in `apps/staff-mobile` with Node 26.5.0 and npm 11.17.0. Type checking, lint, formatting and all 183 tests passed.
- `EXPO_PUBLIC_APP_ENV=emulator EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:5232/api/v1 npm run smoke:android` in `apps/staff-mobile`. Android JavaScript bundle export passed.
- `dotnet test tests/CoffeeShop.Api.Tests -c Release --filter 'FullyQualifiedName~StaffPush|FullyQualifiedName~MobileDevices|FullyQualifiedName~Operational'` with `COFFEESHOP_TEST_POSTGRES` pointing at a disposable PostgreSQL instance. Seven tests passed with no skips.

## Development-device procedure

1. Configure a development Expo project and Android push credentials outside the repository. Use a native development build on an Android device with an API URL it can reach. The app reads the project ID from Expo configuration and uses the `staff-updates` notification channel. [Expo push setup](https://docs.expo.dev/push-notifications/push-notifications-setup/)
2. Enable `StaffPush__EnableExpo=true` only on the disposable API environment. Sign in with a test staff account, grant notification permission, and use another client to create an actionable change. Inspect the preview while the app is foregrounded and backgrounded, then open it and confirm the app loads the authorized record.
3. Repeat after signing out or removing the required permission. Opening an old notification should require authentication and must never reveal the protected record to the unauthorized account. Repeat with permission denied to verify the app remains usable through SignalR and manual refresh.

Expo relay delivery is disabled by default. The automated test used a deterministic provider and the Android check exported a bundle. Physical-device delivery, delayed Expo receipt processing and production push credentials are outside the verification reported here.
