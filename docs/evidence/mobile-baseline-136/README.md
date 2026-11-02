# S0 baseline evidence — 2 November 2026

Source: `9ab252752e2d0aac1ec8c1b938ec67f0d547ea8a` (current remote main during verification).
See the [baseline](../../mobile-delivery-baseline.md) for commands, source references,
dependency owners, and explicit environment/planning blockers.

| Check | Actual result |
| --- | --- |
| `dotnet test tests/CoffeeShop.Api.Tests --configuration Release` | 1 passed, 0 failed, 0 skipped |
| `dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~InitialMigrationTests` | 5 passed, 0 failed, 0 skipped; model/script checks, no database connection |
| API Development startup at `http://127.0.0.1:5232` | Successful; stopped after verification |
| `GET /health` | 200, `Healthy`; no database health check registered |
| `GET /openapi/v1.json` | 200; [raw response](openapi.json), zero paths/schemas/security schemes |
| Repeated OpenAPI capture | Identical on two fetches and after stopping/restarting the process at the same origin |
| `GET /api/v1` | 404; no P0 business route is implemented |
| API Development bind at `http://0.0.0.0:5232` | Host request through its own LAN adapter returns 200, `Healthy`; listener stopped afterwards |
| Remote Android/physical-device request | Not performed: Android toolchain/AVD unavailable; no device available; network profile Public, firewall unchanged |
| Local PostgreSQL | No service/process discovered; `pg_isready -h 127.0.0.1 -p 5432` reports no response; no database provisioned or modified |
| Existing PostgreSQL CI evidence | [Baseline main CI](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/41399593589) succeeded, including PostgreSQL 18 integration job |

[api-probe.json](api-probe.json) and [network-probe.json](network-probe.json) retain the
sanitized observed responses. The committed OpenAPI response is evidence of the empty
surface, not a generated client input or a promised future contract. Host addresses,
device identifiers, credentials, environment values containing secrets, and business
records are excluded. The raw OpenAPI response contains only the non-secret loopback origin.

The tests exercise unchanged source. No new regression tests are needed for this
documentation-only baseline. Android launch, native cleartext/TLS policy, secure
sessions, P0 API authorization, local live PostgreSQL, and simultaneous Windows/mobile
operation remain unverified. Required reviewer approval is separate from these checks.
