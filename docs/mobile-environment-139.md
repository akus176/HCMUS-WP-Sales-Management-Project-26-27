# Mobile environment and Android networking (#139)

This implements Mobile task 1.4 (canonical parent #95 item 1.3). The client uses
the shared ASP.NET Core API only. Environment values are public bundle contents;
they must never contain PATs, passwords, JWT signing keys, tokens or database
connection strings. Actual shop/device addresses belong in ignored local files.

## Configure before starting Expo

From `apps/staff-mobile`, copy `.env.example` to ignored `.env.local` and set
`EXPO_PUBLIC_APP_ENV` and `EXPO_PUBLIC_API_BASE_URL`. Alternatively set both in
the terminal before running Expo. Stop Metro and restart with `npm start -- --clear`
after changing values. Rebuild the native app whenever its HTTP/HTTPS policy changes.
The configuration is selected explicitly; there is no implicit production URL.

| Profile  | API base URL                                                   | Network policy                                                                               |
| -------- | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| emulator | `http://10.0.2.2:5232/api/v1`                                  | Android emulator host alias; local HTTP allowed                                              |
| device   | `http://<HOST-LAN-IP>:5232/api/v1`                             | Replace the placeholder locally with the API host's RFC1918 IPv4 address; local HTTP allowed |
| test     | `http://127.0.0.1:5232/api/v1` or your test server's HTTPS URL | Loopback/private HTTP for host-side tests; HTTPS for remote test servers                     |
| release  | Your public `https://<API-DNS-NAME>/api/v1`                    | HTTPS and public DNS required; cleartext disabled                                            |

HTTPS is accepted for development profiles as well. The device profile rejects
loopback and the emulator alias. The test profile's loopback example is for tests
running on the API host, not Android. Use emulator/device profiles on Android.
Values containing credentials, whitespace, backslashes, query strings, fragments,
extra paths or a missing `/api/v1` are rejected. Error messages never echo values.
The URL is normalized to one `/api/v1`; health uses the same origin's `/health`.

Production EAS builds (`EAS_BUILD_PROFILE=production`) must select release.
Every non-development JS bundle also requires release. Production configuration
must be supplied before export/build; it is not a deployment or runtime secret.
Do not use local self-signed certificates or certificate-validation bypasses.
The public-host validation rejects local hostnames/IP literals, but does not
replace deployment DNS/TLS checks. iOS development should use trusted HTTPS;
this ticket's native cleartext policy and executed connectivity evidence are Android.

## Start the real shared API

Follow [API setup](api-foundation.md), [staff identity](staff-identity.md) and
[Compose setup](docker-compose.md). Keep server-only database/JWT values in the
ignored host configuration. From the repository root, for an emulator-only test:

```powershell
# Set server-only connection/JWT values in this terminal as documented above.
dotnet run --project src/CoffeeShop.Api --no-launch-profile --urls http://127.0.0.1:5232
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5232/health
```

`/health` proves the API process responds; `/health/ready` additionally requires
PostgreSQL connectivity and applied migrations. A liveness-only networking test
does not claim database, login or business-workflow readiness. No staff account
is needed for either health endpoint.

## Android emulator

Start the emulator using the scaffold guide and confirm `adb devices -l` reports
one device. `10.0.2.2` routes to the host loopback; `localhost` inside Android
is the emulator itself. The standard Compose loopback binding also works here.
Keep Metro's port 8081 separate from the API's port 5232. `adb reverse tcp:8081
tcp:8081` is for Metro and does not configure the API.

```powershell
cd apps/staff-mobile
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1'
# Optional: set ADB_PATH to the SDK's adb.exe if it is not on PATH.
# Optional: set ANDROID_SERIAL when more than one Android device is connected.
npm run check:android-health
npm run android
```

The probe sends an HTTP GET from Android through `adb shell toybox nc`, requires
`200 Healthy`, and prints only the non-secret host and result. A missing device,
network timeout or wrong response fails the command without logging the body.
It uses the same validator as startup. It reads `.env.local` if present; terminal
environment values take precedence. The installed app's generated Android
manifest allows cleartext only for validated local HTTP configuration. A release
manifest explicitly sets `android:usesCleartextTraffic=false`.

## Physical Android device on a private LAN

Connect the device and API host to the same trusted LAN. Find the host's active
private IPv4 address locally using `Get-NetIPConfiguration`; do not commit it.
Bind Kestrel to that interface (or `http://0.0.0.0:5232` for a temporary local
test). Permit inbound TCP 5232 only on the intended private network/subnet.
Do not expose PostgreSQL, open a public firewall rule, or change the tracked
Compose port binding to all interfaces. For a phone, use a separately configured
Kestrel listener or trusted HTTPS development proxy. Compose publishes loopback
by default and is intentionally unreachable directly from another LAN device.

Set the device profile and replace `<HOST-LAN-IP>` locally. With USB debugging
authorized and the device listed by ADB, run the same health probe. For HTTPS,
open `https://<API-DNS-NAME>/health` in the device browser with a trusted
certificate and record the host, device, status and `Healthy` result; the
toybox probe deliberately supports local HTTP only. Keep private host/device
identifiers in local evidence or redact them before sharing.

Wi-Fi client isolation, VPN routes and Windows firewall rules can prevent LAN
access even when the host health check passes. Check those settings with the
network owner. A timeout means unreachable; `401` on a protected business route
does not mean the public health endpoint failed.

## Verification

`npm run check` includes valid/missing/malformed/unsafe URL cases and startup
tests proving invalid values cannot mount the session provider. Test and release
configuration must also pass Expo config generation before packaging. See
[executed #139 evidence](evidence/mobile-environment-139/README.md) for actual
commands/results. This work adds configuration and networking only.
