# Android live refresh and session renewal fixes — 16 November 2026

Both reported defects are fixed in `7f1162c6d9b5b971be59165cdc41a5cce1a30bd6` on [PR #214](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/214). Issues: [#203](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203), paired Windows/API [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202).

**Fixed native verification: PASS, seven of seven flow attempts.** Android refreshed inventory and an open receipt automatically after native WinUI writes. An unsaved adjustment survived a real API token renewal, then was submitted once. Android, WinUI and PostgreSQL agreed on 506 g and three correctly attributed movements. This supersedes the two defect observations in the [original failed run](../issue-202-shared-state/native-2026-11-16/README.md); its original failures and ledger remain unchanged.

## Changes and automated regression proof

Inventory events now invalidate the actual inventory balance, movement, receipt-list and receipt-detail query prefixes. They cancel older reads and refetch authoritative API state; event payloads are not used as business records. Unrelated sales and other-user caches remain unchanged.

Routine token rotation retains the existing user snapshot when ID, full name, role set and permission set are unchanged. This prevents an unnecessary permission-boundary remount from clearing forms. Changed staff details still create a new snapshot and revalidate permissions; revoked permissions immediately hide protected fields. Logout/account changes retain their clearing behavior.

[Regression summary](artifacts/unit-summary.json): before the fixes, the focused 43-test run had **eight failures and 35 passes**, reproducing six inventory-key cases, unchanged-session identity and form loss. Afterward the focused run passed 43/43; the complete mobile suite passed **235/235 across 27 suites**, zero failed/pending. TypeScript, ESLint and formatting passed. Both forwarding-harness suites passed **7/7**. [Before results](artifacts/unit-before.json), [complete passing results](artifacts/unit-after.json). The permission-revocation safety test uses the actual inventory workspace, session provider and permission boundary.

All six CI jobs passed on the fix commit: [run 43825751550](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/43825751550).

## Exact environment

| Component | Identity |
| --- | --- |
| Fixed Android JavaScript | `7f1162c6d9b5b971be59165cdc41a5cce1a30bd6`, served by Metro; [SHA-256 of both changed source files](artifacts/javascript-source-hashes.json) |
| Installed native Android shell | Existing CI debug APK, app `com.coffeeshop.staffmobile` 0.1.0/code 1, SHA-256 `03dc41ad6a555cf7bb55a29d4db703b49bf8b2e07c5024a08eeb4f097cb15529`; this is not a newly installed APK from the fix CI run |
| Android runtime | `emulator-5554`, sdk_gphone64_x86_64, Android 16/API 36, x86_64 |
| API and Release WinUI business source | Unchanged `5683fd14a56ef6d5a816288d769a894e7833970b` |
| Tools | Node 26.5.0, npm 11.17.0, Temurin 17.0.17, Maestro 2.12.1, Windows .NET SDK 10.0.404 |
| Shared API | `http://127.0.0.1:52302/api/v1`; WinUI trace listener 52300; Android `http://10.0.2.2:52301/api/v1`; both forward to the same API |
| PostgreSQL | Existing disposable container `coffeeshop-issue202-e2e-923ab71e`, PostgreSQL 18.7, database `issue202`, loopback 55402 |
| Fresh fixture | `I203 Live Beans 7146c9`, initially 500 g, threshold 500 g; receipt `I203-LIVE-7146c9`, +2 g at unit cost 1; [fixture IDs](artifacts/fixture.json) |
| Synthetic staff | `issue202_windows` and `issue202_android`: Managers; `issue202_admin`: observer; `issue202_cashier`: forbidden-operation control |

The database was reused, with a new independent fixture. The earlier I202 ledger was not reset or edited. The user started the prepared native verification services manually and retained the existing Windows sign-in. Both native sessions were left signed in. Credentials, signing material, raw Maestro logs and private configuration are excluded.

## Executed native cases

| Case | Result and evidence |
| --- | --- |
| Shared baseline | Android loaded 500 g: [JUnit](artifacts/fix-baseline/junit.xml), [screen](artifacts/fix-baseline/screen.png) |
| Native Windows adjustment, automatic Android refresh | WinUI saved -5 g. Android displayed 495 g inside the 15-second assertion window, with no Android refresh tap, navigation or restart: [JUnit](artifacts/fix-live-balance/junit.xml), [Android](artifacts/fix-live-balance/screen.png), [WinUI](artifacts/winui-live-495.jpg), [API state](artifacts/after-winui-live.json) |
| Active draft receipt | Android opened the new draft before the Windows action: [JUnit](artifacts/fix-receipt-open/junit.xml), [screen](artifacts/fix-receipt-open/screen.png) |
| Native Windows receipt post, automatic Android detail refresh | WinUI posted +2 g. Android's open detail became Posted and the Post button disappeared, without Android refresh/navigation: [JUnit](artifacts/fix-live-receipt/junit.xml), [screen](artifacts/fix-live-receipt/screen.png) |
| Unsaved form before renewal | Quantity 9 and reason `I203 retained through token renewal` were entered, with Save enabled: [JUnit](artifacts/fix-renewal-form-before/junit.xml), [screen](artifacts/fix-renewal-form-before/screen.png) |
| Unsaved form after real renewal | The form was left untouched across actual token expiry/renewal. Assertions then confirmed exact quantity, reason and enabled Save without navigation or typing: [JUnit](artifacts/fix-renewal-form-after/junit.xml), [screen](artifacts/fix-renewal-form-after/screen.png) |
| Deliberate submission after renewal | The existing retained input was saved once; Android showed `Adjustment saved by the server. New balance: 506 g.`: [JUnit](artifacts/fix-renewal-submit/junit.xml), [screen](artifacts/fix-renewal-submit/screen.png) |

The initial native refresh succeeded at **07:47:26.061 UTC**, and the next at **07:58:09.351 UTC**, **643.29 seconds** later. The form was prepared at 07:53:06 UTC and asserted after renewal at 08:00:16 UTC. [Renewal observation](artifacts/real-renewal.json), [Android HTTP trace](artifacts/android-requests.jsonl). This was a natural ten-minute API token expiry and SignalR reconnect; tokens were not edited and expiry was not shortened. Trace records contain time, client, method, path and status only.

## Final authoritative state

| Delta | Persisted actor | Reason |
| --- | --- | --- |
| -5 g | issue202_windows | I203 native live refresh |
| +2 g | issue202_windows | Purchase receipt I203-LIVE-7146c9 |
| +9 g | issue202_android | I203 retained through token renewal |

500 - 5 + 2 + 9 = **506 g**, exactly three movements and one movement for the posted receipt. [Independent API read](artifacts/final-state.json), [direct PostgreSQL read](artifacts/database-final.json), [executed SQL](artifacts/final-state.sql), [native WinUI 506 g](artifacts/winui-final-506.jpg), [Windows accessibility record](artifacts/winui-final-506.txt). The API read also confirmed Cashier 403 with protected ingredient data absent. [State before renewal](artifacts/before-renewal-state.json) had 497 g and two movements, proving no unsaved action was sent during renewal.

## Reproduction and scope

The exact executed [seven Maestro flows](flows/) are retained. They are ordered: baseline, live-balance, receipt-open, live-receipt, renewal-form-before, renewal-form-after, renewal-submit. Use a fresh synthetic fixture and its names through `MAESTRO_INGREDIENT` and `MAESTRO_RECEIPT`. Interleave the native WinUI -5 and receipt +2 actions as described above. Do not run the final two flows until a successful second native `/auth/refresh` is observed. Run each flow separately so the untouched interval is preserved:

```powershell
maestro --device emulator-5554 test --format junit --output result.xml flows/<case>.yaml
adb -s emulator-5554 exec-out screencap -p
```

The local runner invoked the same Maestro CLI through Java, required exit 0, one JUnit test, zero failures/skips, and saved an Android screenshot and result JSON after each attempt. The two read-only SQL setup attempts with incorrect local Docker invocation/role failed before the successful final read; they performed no database writes.

This run specifically rechecks the fixed live-refresh and renewal defects. Native offline/no-replay, reconnect, stale receipt 409, permission denial, transport feedback and explicit-retry evidence remain in the original report; those cases were not all rerun here. The original transport failure took more than two minutes to report, which remains a responsiveness limitation. Existing regression coverage still exercises failure handling and permission changes. Broader S4/P0 workflows are not claimed rerun. Required named reviewer approvals remain outstanding; this report does not approve or close #202/#203 or complete parent review gates.
