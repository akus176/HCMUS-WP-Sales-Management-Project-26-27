# Shared WinUI/Android end-to-end run — 16 November 2026

**Follow-up:** both discovered mobile defects were fixed and passed a [separate native verification](../../issue-203-mobile-fixes-2026-11-16/README.md), including automatic inventory/receipt updates and unsaved input across a real token renewal. The original failed results below are preserved unchanged.

Issues: [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202), Android [#203](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203). Implementation/evidence branch: draft [PR #214](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/214).

**Overall acceptance: FAILED.** Both native clients ran against one fresh API/database. Manual shared-state reads, mutations in both directions, real 403/409 responses, offline protection, reconnect, transport-error feedback, deliberate retry, persisted actors and the Windows API-only boundary were exercised. **Automatic inventory refresh failed:** Windows saved 95 g, the server published an inventory event, and connected Android continued showing 100 g until a deliberate refresh. No application code was changed to obtain these results. Keep #202/#203, Mobile 6.4 and the parent shared-state gates open.

## Builds and environment

| Component | Recorded identity |
| --- | --- |
| Business/API/WinUI source | `5683fd14a56ef6d5a816288d769a894e7833970b`; physical Windows export, Release build, zero warnings/errors |
| Android APK | `staff-mobile-android-debug` from [CI run 43722362913](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/43722362913), head `c017bb7c6108822d7112e25f98f857c283c309c4`; installed APK SHA-256 `03dc41ad6a555cf7bb55a29d4db703b49bf8b2e07c5024a08eeb4f097cb15529` |
| Android JavaScript | Same business source; no `apps/staff-mobile` or `src` differences between the two commits above; served by local Metro with the emulator profile |
| Android runtime | `sdk_gphone64_x86_64`, Android 16 / API 36, x86_64; app `com.coffeeshop.staffmobile` 0.1.0 / code 1 |
| Tools | Node 26.5.0, npm 11.17.0, Temurin 17.0.17, Maestro 2.12.1; Windows .NET SDK 10.0.404 |
| Shared API | `http://127.0.0.1:52302/api/v1`; readiness 200; same signing configuration and database throughout |
| Native client addresses | WinUI `http://127.0.0.1:52300/api/v1/`; Android `http://10.0.2.2:52301/api/v1`; separate tracing listeners forward to that same API, including SignalR upgrades |
| Database | Dedicated container `coffeeshop-issue202-e2e-923ab71e`, PostgreSQL 18.7, fresh migrated `issue202` database, loopback port 55402 |
| Seed | One ingredient, initially 100 g with a 100 g threshold; one synthetic supplier; draft receipts `I202-SHARED-001` (+10 g) and later `I202-SHARED-002` (+4 g) |
| Accounts | `issue202_windows` Manager; `issue202_android` Manager; `issue202_admin` Admin observer; `issue202_cashier` Cashier negative control |

[Build/environment hashes](artifacts/environment.json), [installed device/APK identity](artifacts/android-device.json), [fixture IDs](artifacts/fixture.json), [second receipt](artifacts/receipt2-fixture.json). Existing development PostgreSQL services were not reseeded or replaced. Credentials and raw Maestro/debug logs remain private.

The user signed into WinUI. Automatic approval review rejected starting Metro with only `blocked by policy`; the user started the prepared local development-server command manually. The app used normal API authentication and authorization. Neither client was deliberately signed out after verification.

## Native observations

| Case | Observed result | Evidence |
| --- | --- | --- |
| Shared starting state | WinUI and Android displayed the same ingredient at 100 g | [WinUI](artifacts/winui-baseline-100.jpg), [Android/JUnit](artifacts/baseline/junit.xml) |
| Windows → Android, automatic event refresh | **FAIL:** native WinUI adjustment -5 returned 200; WinUI/server showed 95 g; authenticated server observer received the inventory event; Android still showed 100 g after its 15-second assertion window | [WinUI 95](artifacts/winui-adjustment-95.jpg), [Android stale 100](artifacts/windows-to-android-live/screen.png), [failed JUnit](artifacts/windows-to-android-live/junit.xml), [event fields](artifacts/operational-events.jsonl) |
| Windows → Android, deliberate refresh | PASS: Refresh inventory loaded 95 g without replacing server state | [JUnit](artifacts/windows-to-android-refresh/junit.xml), [screen](artifacts/windows-to-android-refresh/screen.png) |
| Android → Windows adjustment | Native Android +2 returned 200; independent API showed 97 g and the Android actor; native WinUI refresh showed 97 g. A later +1 case also verified immediate success feedback | [WinUI 97](artifacts/winui-android-balance-97.jpg), [server state](artifacts/after-android-adjustment-state.json), [immediate-feedback JUnit](artifacts/android-adjustment-feedback/junit.xml) |
| Android receipt → stale Windows | Android posted receipt 001, 204. WinUI retained Draft, attempted Post, received a real 409 and displayed reload guidance. Deliberate stock-room refresh loaded 107 g and Posted; the Post action disappeared. Only one receipt movement exists | [Android/JUnit](artifacts/android-receipt-post/junit.xml), [Windows 409](artifacts/winui-stale-receipt-409.jpg), [reloaded Posted](artifacts/winui-receipt-reloaded.jpg), [server state](artifacts/after-receipt-conflict-state.json) |
| Windows receipt → stale Android | Android held receipt 002 as Draft. Windows posted it, 204. Android's stale Post received 409, displayed conflict feedback, refreshed to Posted and removed Post. Balance 112 g, one movement for this receipt | [Windows action](artifacts/winui-receipt2-posted.txt), [Android/JUnit](artifacts/android-receipt-conflict/junit.xml), [Android screen](artifacts/android-receipt-conflict/screen.png), [server state](artifacts/after-android-conflict-state.json) |
| Offline write protection | A valid nine-unit adjustment was prepared. Both emulator Wi-Fi and mobile data were disabled. Offline was visible, Save was disabled, tapping it produced no success or stock effect. Balance remained 112 g / five movements | [JUnit](artifacts/android-offline/junit.xml), [screen](artifacts/android-offline/screen.png), [server state](artifacts/while-android-offline-state.json) |
| Reconnect and refresh | Admin independently added 3 g while Android was offline. Original network settings were restored. Android refresh loaded 115 g; the prepared nine-unit write was not replayed. Six movements remained | [JUnit](artifacts/android-reconnect/junit.xml), [external change](artifacts/offline-external-change.json), [server state](artifacts/after-reconnect-state.json) |
| Stale native permission | Only the synthetic Android account's Manager role was temporarily removed, then restored in `finally`. Its native Save received 403, displayed denial feedback, and added no movement; 115 g / six movements remained | [JUnit](artifacts/android-forbidden-response/junit.xml), [screen](artifacts/android-forbidden-response/screen.png), [server state](artifacts/after-native-forbidden-state.json) |
| Transport failure | With an exactly checked nine-unit form, the owned API tracing listeners were stopped while the actual API/database remained running. Android eventually displayed unconfirmed-result feedback, retained the exact amount/reason and made no success claim. Database remained 214 g / seven movements. Feedback took over two minutes from submission; the earlier 20-second assertion was premature | [verified input](artifacts/android-clean-network-form/screen.png), [submit](artifacts/android-network-submit/result.json), [passing feedback JUnit](artifacts/android-network-feedback/junit.xml), [feedback screen](artifacts/android-network-feedback/screen.png), [database during outage](artifacts/database-during-verified-outage.json) |
| Recovery and explicit retry | Restoring transport alone left 214 g / seven movements. An expired-token attempt returned 401 and renewed the session without automatic mutation replay. A subsequent explicit, exactly checked nine-unit action returned 200 and produced 223 g / eight movements. WinUI refresh showed the same 223 g | [before retry](artifacts/verified-transport-restored-before-retry-state.json), [401 attempt](artifacts/android-verified-retry/junit.xml), [final native JUnit](artifacts/android-final-retry/junit.xml), [Android](artifacts/android-final-retry/screen.png), [WinUI](artifacts/winui-final-223.jpg) |
| Actors, duplicate effects and privacy | Direct PostgreSQL and independent authorized API reads agree; receipt movements occur once each; Windows/Android actors match their accounts; Cashier ingredient reads returned 403 without ingredient details; all 12 observed operational events contained only `id`, `type`, `updatedAt` | [database](artifacts/database-final.json), [API](artifacts/final-state.json), [consistency assertions](artifacts/server-assertions.json), [events](artifacts/operational-events.jsonl) |
| Windows network/build boundary | 2,792 process-owned samples, 02:31:11–03:31:16 UTC; all 166 connection observations targeted loopback:52300, none PostgreSQL:55402. Release dependency/assembly inspection passed | [network summary](artifacts/network-summary.json), [raw samples](artifacts/winui-network.jsonl), [Release check](artifacts/release-boundary.json) |

WinUI's receipt Post controls remained available immediately after 409; the observed recovery used **Inventory → Refresh stock room data → Receipts**. This run does not claim automatic Windows reload or immediate disabling of those stale controls. Some cross-account movement labels display `Staff`; the persisted actor IDs were checked independently.

## Full ledger and test-script correction

The final balance is **223 g**, starting at 100 g:

| Delta | Actor | Source |
| --- | --- | --- |
| -5 | Windows Manager | Native stock adjustment |
| +2 | Android Manager | Native stock adjustment |
| +10 | Android Manager | Receipt 001 |
| +1 | Android Manager | Immediate-feedback verification |
| +4 | Windows Manager | Receipt 002 |
| +3 | Admin observer | Independent change while Android was offline |
| +99 | Android Manager | **Test-script mistake:** input appended `9` to retained `9`, and also appended the reason text |
| +9 | Android Manager | Final checked explicit action |

The +99 request was a real, valid API submission generated by an erroneous test script. It was not an application retry of nine units. Its movement was **preserved**, with no compensating edit or hidden database reset. [Correction record](artifacts/script-entry-error.json), [snapshot after the mistake](artifacts/database-after-script-entry-error.json). Later input checks require the exact quantity and reason before submission.

[All 28 native flow attempts](artifacts/attempts.json) are retained: **18 passed, 10 failed**. This stateful exploratory run includes setup and recovery attempts. The individual JUnit reports remain authoritative; no failed report was relabelled as a pass.

The failures include the reproduced live-refresh defect; an initial adjustment assertion scrolled in the wrong direction; a delayed confirmation check after session renewal; selection of a retained earlier receipt; a setup/navigation failure and its dependent missing-Save attempt; the premature transport-feedback assertion; the +99 input mistake; renewal interrupting form preparation; and the expired-token retry. Corrected follow-ups and separate authoritative reads are linked above. This is not a clean all-passing suite.

## Defects and follow-up

1. **Confirmed live inventory refresh defect, #203.** `operationalEvents.ts` selects prefix `inventory`, but the visible inventory queries use `inventory-balances`, `inventory-movements`, `purchase-receipts` and `purchase-receipt`. The native failure matches the existing source finding. Correct the affected prefixes, add regression coverage using the actual keys, and repeat the native event case before accepting Mobile 4.7/6.4.
2. **Investigate input retention during session renewal.** During `android-verified-network-ready`, the trace records successful token renewal at 03:14:52 UTC while form entry was in progress; the form was absent by the failed Save lookup. [Command timing](artifacts/form-renewal-command-excerpt.txt), [screen](artifacts/android-verified-network-ready/screen.png), [API trace](artifacts/android-requests.jsonl). `PermissionBoundary` temporarily removes its children when the session snapshot changes, which is a plausible cause; that causal explanation is an inference from code and timing, not a dedicated isolated reproduction.
3. **Transport feedback latency:** the corrected case eventually passed, with input retained, after more than two minutes. The earlier progress message attributing the missing message to a disappearing form was premature. Keep the actual long-delay observation and passing feedback result.

The run covers the shared-state inventory/receipt subset for #202/#203. It does not rerun every P0/P1 workflow or replace the prior S4 barista, courier, payment and administration evidence. Approval by the named reviewers is still required. No approval, merge or issue closure is claimed.

## Reproduction

Use a fresh disposable database and the build/tool versions above. Follow the [mobile contributor runbook](../../../mobile-quality-and-release.md) for migration, provisioning and native setup. Create distinct synthetic Windows/Android Managers, an Admin observer and a Cashier. Prepare the ingredient/receipts with the quantities above; record newly generated IDs rather than using another environment's rows.

The small [HTTP tracing listeners](harness/trace-proxies.mjs), [native runner](harness/run-android.mjs), [independent API reader](harness/Read-SharedState.ps1), [database query](harness/final-state.sql), and [executed YAML flows](flows) record the procedure used here. In a physical Windows scratch directory, place the helpers under `e2e-private`, flows under `e2e-flows`, and public results under `e2e-results`. Supply a private `e2e-private/config.json` with `api`, `password`, `winui`, `mobile`, `admin`, `cashier`; add the owned `container` identity for the database probes. Never commit that file. The runner sends credentials through the child environment, retains raw output privately and publishes only reviewed screenshots, JUnit and status metadata.

```powershell
. "$env:LOCALAPPDATA\CoffeeShopMobileTools\Activate-MobileTesting.ps1"
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:52301/api/v1'
adb -s emulator-5554 reverse tcp:8081 tcp:8081
# Start Metro manually in apps/staff-mobile and leave it running.
# Start the actual API on 52302, then the tracing listeners on 52300/52301.
node e2e-private/trace-proxies.mjs "$PWD/e2e-results"
# In another activated terminal, run each flow at its documented UI/data stage:
node e2e-private/run-android.mjs "$PWD" baseline
```

Interleave native Windows actions with the Android cases in the observation table. For the live-refresh regression, leave Android on the loaded 100 g screen, submit -5 through WinUI, and run `windows-to-android-live` **before any manual refresh or route restart**. For conflicts, let one client retain Draft before the other posts that same receipt. Limit role fault injection to the synthetic Android account and restore its exact role row in `finally`. Restore the emulator's recorded network settings after the offline case. For transport loss, stop only the owned forwarding listeners, wait for the real native error, inspect authoritative state, restore transport, and make a deliberate checked retry.

Only the tracing method/path/status logs, approved event fields, fixture identities, reviewed native captures and public result metadata are included. Authorization headers, token-bearing query strings, payload bodies, passwords, signing keys, connection strings, raw Maestro logs and private configuration are excluded. A scan for this run's actual private values passed before publication.
