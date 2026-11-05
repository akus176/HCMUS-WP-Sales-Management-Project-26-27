# Mobile environment verification (#139)

Executed on 5 November 2026 (Asia/Saigon), from branch
`feat(mobile)/s1-environment-network-config` based on `c7960a2`.

## Automated checks

Pinned Node 26.5.0 / npm 11.17.0 were installed outside the repository.

- `npm ci --no-audit --no-fund`: passed, 1,311 packages installed.
- `npm run check`: strict TypeScript, ESLint (zero warnings), Prettier and
  Jest passed: **8 suites, 75 tests, zero failures**.
- A clean-cache `npm test -- --no-cache` passed all 72 tests present before
  three additional invalid-host regression cases were added; the final full
  check above includes all 75.
- `openspec validate add-staff-mobile-app`: passed.
- `git diff --check`: passed.
- `npx expo config --type introspect --json` with emulator HTTP configuration:
  generated Android `android:usesCleartextTraffic=true`.
- The same introspection with release HTTPS configuration and
  `EAS_BUILD_PROFILE=production`: generated cleartext `false`.
- Expo public config generation rejects missing configuration with the fixed
  safe message, and rejects an emulator/HTTP profile when the EAS build profile
  is production.
- `npx expo export --platform android --output-dir .expo/environment-139-export`
  with the release profile and public HTTPS example: passed (1,202 modules);
  output is ignored. This is JavaScript/Hermes export evidence, not a new native
  APK installation or production deployment.

The environment tests cover all four profiles; missing, malformed and unsafe
values; credential/query/fragment redaction; release downgrade prevention;
device loopback errors; local/public host boundaries; and immutable normalized
API/health URLs. The startup tests verify invalid configuration cannot mount
session children and valid configuration allows startup. The existing session,
secure-storage, login, Router and navigation tests continue to pass.

## Android-to-shared-API connectivity

- Host: Windows, the actual repository `CoffeeShop.Api` process, .NET SDK
  10.0.403; Kestrel listener `http://127.0.0.1:5232`.
- Android: AVD `Medium_Phone_API_36.1`, model `sdk_gphone64_x86_64`,
  Android 16 / API 36, ADB serial `emulator-5554`.
- The first software graphics mode failed initialization. The existing AVD
  booted successfully using `-gpu swiftshader_indirect -feature -Vulkan`
  with headless/no-audio/no-snapshot options and available WHPX acceleration.
  No Windows security setting or global SDK configuration was changed.
- Host `Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5232/health`:
  `200 Healthy`.
- Set the public emulator profile/URL, ADB_PATH to the installed SDK's adb.exe
  and ANDROID_SERIAL to emulator-5554, then run from apps/staff-mobile:

```powershell
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1'
npm run check:android-health
```

Observed result:

```text
Android emulator-5554: GET http://10.0.2.2:5232/health -> 200 Healthy
```

The request runs through Android's real network stack using ADB/toybox, with
a timeout and exact response validation. The probe keeps its input pipe open
briefly for the reply and normalizes Android shell CRLF translation. It logs
only the non-secret host/result, never an arbitrary HTTP response body.

This run intentionally verifies API liveness/network reachability only. The
API was given disposable in-memory JWT configuration and an unreachable
development database connection; no database readiness, authentication or
business workflow success is claimed. No staff credentials were used.

## Scope and handoff

See [the setup guide](../../mobile-environment-139.md) for all profiles, emulator
loopback, trusted physical-device LAN, native HTTP policy, HTTPS and troubleshooting.
The recorded emulator result satisfies the ticket's emulator/device connectivity
criterion; no physical phone or iOS run is claimed. No PAT, real secret, database
password, private shop LAN address, generated native project or tool binary is
tracked. The test's private addresses are synthetic validation inputs.

Mobile task 1.4 is implemented; its canonical parent #95 item is 1.3.
The legacy local tasks.md is unchanged. #139 remains open for @vt-Toan's review;
implementation and test evidence are not represented as reviewer approval.
